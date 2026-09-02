using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.DeliveryFlow;

public sealed record FlowNodeState(string Id, string Title, string Subtitle, string Category, int Count, bool Active);

public sealed record FlowItemDto(
    Guid Id,
    string Recipient,
    string Channel,
    string? ProviderId,
    string Status,
    int AttemptCount,
    double? LatencyMs,
    string? ErrorHuman,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? CorrelationId,
    string? Category);

public sealed record FlowEventDto(
    DateTimeOffset At,
    string Message,
    string Severity, // info | success | warn | error
    Guid? NotificationId,
    string? Recipient);

public sealed record NotificationFlowSnapshot(
    int Queued,
    int Sending,
    int Delivered,
    int Failed,
    double? AvgLatencyMs,
    IReadOnlyList<FlowNodeState> Nodes,
    IReadOnlyList<FlowItemDto> Items,
    IReadOnlyList<FlowEventDto> Events);

public interface INotificationFlowService
{
    Task<NotificationFlowSnapshot> GetSnapshotAsync(string? tenantId, int take, CancellationToken ct = default);
}

public sealed class NotificationFlowService(NotificationDbContext db) : INotificationFlowService
{
    static readonly DeliveryStatus[] QueuedStatuses =
    [
        DeliveryStatus.Queued,
        DeliveryStatus.Scheduled
    ];

    static readonly DeliveryStatus[] SendingStatuses =
    [
        DeliveryStatus.Processing,
        DeliveryStatus.Sent
    ];

    static readonly DeliveryStatus[] DeliveredStatuses =
    [
        DeliveryStatus.Delivered
    ];

    static readonly DeliveryStatus[] FailedStatuses =
    [
        DeliveryStatus.Failed,
        DeliveryStatus.DeadLetter,
        DeliveryStatus.Cancelled,
        DeliveryStatus.Suppressed
    ];

    public async Task<NotificationFlowSnapshot> GetSnapshotAsync(string? tenantId, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var q = db.NotificationStatuses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(tenantId))
            q = q.Where(x => x.TenantId == tenantId);

        // Recent window for counts (24h) keeps the board focused
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var recent = q.Where(x => x.UpdatedAt >= since);

        var queued = await recent.CountAsync(x => QueuedStatuses.Contains(x.Status), ct);
        var sending = await recent.CountAsync(x => SendingStatuses.Contains(x.Status), ct);
        var delivered = await recent.CountAsync(x => DeliveredStatuses.Contains(x.Status), ct);
        var failed = await recent.CountAsync(x => FailedStatuses.Contains(x.Status), ct);

        var rows = await q.OrderByDescending(x => x.UpdatedAt).Take(take).ToListAsync(ct);

        var latencies = rows
            .Where(x => DeliveredStatuses.Contains(x.Status) || FailedStatuses.Contains(x.Status))
            .Select(x => (x.UpdatedAt - x.CreatedAt).TotalMilliseconds)
            .Where(ms => ms >= 0 && ms < 86_400_000)
            .ToList();
        double? avgLatency = latencies.Count == 0 ? null : latencies.Average();

        var pluginGroups = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ProviderId) ? "default" : x.ProviderId!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var primaryPlugin = pluginGroups.FirstOrDefault() ?? "plugin";

        var items = rows.Select(MapItem).ToList();
        var events = rows.SelectMany(ToEvents).OrderByDescending(e => e.At).Take(40).ToList();

        var activeQueue = queued > 0 || sending > 0;
        var nodes = new List<FlowNodeState>
        {
            new("app", "NotificationHub", "source · your app", "trigger", items.Count, true),
            new("plugin", primaryPlugin, pluginGroups.Count > 1 ? $"{pluginGroups.Count} providers" : "delivery plugin", "api", rows.Count, true),
            new("queue", "Queue", $"{queued} waiting", "prep", queued, activeQueue),
            new("dispatch", "Dispatch", $"{sending} in flight", "ai", sending, sending > 0),
            new("delivered", "Delivered", $"{delivered} recipients", "success", delivered, delivered > 0),
            new("failed", "Failed", $"{failed} with errors", "retry", failed, failed > 0),
        };

        return new NotificationFlowSnapshot(queued, sending, delivered, failed, avgLatency, nodes, items, events);
    }

    static FlowItemDto MapItem(NotificationStatusEntity n)
    {
        double? latency = null;
        if (n.UpdatedAt > n.CreatedAt)
            latency = (n.UpdatedAt - n.CreatedAt).TotalMilliseconds;
        return new FlowItemDto(
            n.Id,
            MaskRecipient(n.Recipient),
            n.Channel,
            n.ProviderId,
            n.Status.ToString(),
            n.AttemptCount,
            latency,
            HumanizeError(n.ErrorCode, n.ErrorMessage),
            n.CreatedAt,
            n.UpdatedAt,
            n.CorrelationId,
            n.Category);
    }

    static IEnumerable<FlowEventDto> ToEvents(NotificationStatusEntity n)
    {
        var status = n.Status.ToString();
        var who = MaskRecipient(n.Recipient);
        var channel = string.IsNullOrWhiteSpace(n.Channel) ? "message" : n.Channel;

        yield return new FlowEventDto(
            n.CreatedAt,
            $"Accepted {channel} for {who}",
            "info",
            n.Id,
            who);

        if (QueuedStatuses.Contains(n.Status))
        {
            yield return new FlowEventDto(
                n.UpdatedAt,
                $"Waiting in queue · attempt {Math.Max(1, n.AttemptCount)}",
                "warn",
                n.Id,
                who);
        }
        else if (SendingStatuses.Contains(n.Status))
        {
            yield return new FlowEventDto(
                n.UpdatedAt,
                $"Sending via {n.ProviderId ?? "provider"}…",
                "info",
                n.Id,
                who);
        }
        else if (DeliveredStatuses.Contains(n.Status))
        {
            var lag = (n.UpdatedAt - n.CreatedAt).TotalSeconds;
            yield return new FlowEventDto(
                n.UpdatedAt,
                lag < 1
                    ? $"Delivered to {who}"
                    : $"Delivered to {who} · {FormatLag(lag)} after accept",
                "success",
                n.Id,
                who);
        }
        else if (FailedStatuses.Contains(n.Status))
        {
            var human = HumanizeError(n.ErrorCode, n.ErrorMessage) ?? status;
            yield return new FlowEventDto(
                n.UpdatedAt,
                $"Could not deliver to {who}: {human}",
                "error",
                n.Id,
                who);
        }
    }

    public static string MaskRecipient(string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient)) return "—";
        var r = recipient.Trim();
        if (r.Contains('@'))
        {
            var parts = r.Split('@');
            var local = parts[0];
            var domain = parts.Length > 1 ? parts[1] : "";
            var show = local.Length <= 2 ? local[0] + "•" : local[..2] + "•••";
            return $"{show}@{domain}";
        }
        // phone-like
        var digits = new string(r.Where(char.IsDigit).ToArray());
        if (digits.Length >= 6)
            return $"+{digits[..Math.Min(3, digits.Length)]}•••{digits[^3..]}";
        if (r.Length <= 4) return r;
        return r[..2] + "•••" + r[^2..];
    }

    public static string? HumanizeError(string? code, string? message)
    {
        var raw = $"{code} {message}".Trim();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("timeout") || lower.Contains("timed out"))
            return "The provider took too long to respond";
        if (lower.Contains("invalid") && (lower.Contains("number") || lower.Contains("recipient") || lower.Contains("phone")))
            return "Recipient number looks invalid";
        if (lower.Contains("opt") && lower.Contains("out"))
            return "Recipient has opted out of this channel";
        if (lower.Contains("quota") || lower.Contains("rate") || lower.Contains("throttle"))
            return "Provider rate limit — will retry";
        if (lower.Contains("auth") || lower.Contains("unauthorized") || lower.Contains("forbidden"))
            return "Provider rejected our credentials";
        if (lower.Contains("template"))
            return "Template could not be rendered";
        if (lower.Contains("network") || lower.Contains("connection"))
            return "Network error talking to the provider";
        // strip noisy technical prefixes
        var clean = message?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) clean = code;
        if (clean is { Length: > 120 }) clean = clean[..117] + "…";
        return clean;
    }

    static string FormatLag(double seconds)
    {
        if (seconds < 60) return $"{seconds:0.#}s";
        if (seconds < 3600) return $"{seconds / 60:0.#}m";
        return $"{seconds / 3600:0.#}h";
    }
}
