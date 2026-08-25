using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Webhooks;

public sealed class WebhookDispatcher : IWebhookDispatcher
{
    private readonly NotificationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(NotificationDbContext db, IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger)
    {
        _db = db; _httpClientFactory = httpClientFactory; _logger = logger;
    }

    public async Task DispatchAsync(string eventName, object payload, string? tenantId = null, CancellationToken ct = default)
    {
        var query = _db.WebhookSubscriptions.AsNoTracking().Where(x => x.IsActive);
        if (tenantId is not null) query = query.Where(x => x.TenantId == null || x.TenantId == tenantId);

        var subs = await query.ToListAsync(ct);
        var client = _httpClientFactory.CreateClient("webhooks");

        foreach (var sub in subs)
        {
            if (!WebhookUrlValidator.IsSafe(sub.Url, out var urlError))
            {
                _logger.LogWarning("Skipping webhook {Id}: unsafe URL ({Error})", sub.Id, urlError);
                continue;
            }

            var events = JsonSerializer.Deserialize<string[]>(sub.EventsJson) ?? [];
            if (events.Length > 0 && !events.Contains(eventName, StringComparer.OrdinalIgnoreCase))
                continue;

            try
            {
                var body = JsonSerializer.Serialize(new { @event = eventName, data = payload, timestamp = DateTimeOffset.UtcNow });
                using var req = new HttpRequestMessage(HttpMethod.Post, sub.Url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                if (!string.IsNullOrEmpty(sub.Secret))
                {
                    var sig = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(sub.Secret), Encoding.UTF8.GetBytes(body)));
                    req.Headers.TryAddWithoutValidation("X-Signature", sig);
                }
                var resp = await client.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                    _logger.LogWarning("Webhook {Url} returned {Status}", sub.Url, resp.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook dispatch failed to {Url}", sub.Url);
            }
        }
    }
}
