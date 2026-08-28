using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;
using NotificationHub.Domain.Templates.Events;

namespace NotificationHub.Domain.Templates;

/// <summary>
/// Aggregate root for a notification template (key + locale versioning).
/// Invariant: subject/body non-empty; version increments on content change.
/// </summary>
public sealed class NotificationTemplate : AggregateRoot<TemplateId>
{
    public TemplateKey Key { get; private set; } = null!;
    public TenantId? TenantId { get; private set; }
    public string Locale { get; private set; } = "en";
    public string Channel { get; private set; } = "email";
    public string Subject { get; private set; } = "";
    public string Body { get; private set; } = "";
    public string? HtmlBody { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private NotificationTemplate() { }

    public static NotificationTemplate Create(
        TemplateId id,
        TemplateKey key,
        string channel,
        string subject,
        string body,
        string? htmlBody,
        string locale,
        TenantId? tenantId,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException("Template subject cannot be empty.");
        if (string.IsNullOrWhiteSpace(body) && string.IsNullOrWhiteSpace(htmlBody))
            throw new DomainException("Template body cannot be empty.");

        var t = new NotificationTemplate
        {
            Id = id,
            Key = key,
            Channel = channel.Trim().ToLowerInvariant(),
            Subject = subject.Trim(),
            Body = body ?? "",
            HtmlBody = htmlBody,
            Locale = string.IsNullOrWhiteSpace(locale) ? "en" : locale.Trim(),
            TenantId = tenantId,
            Version = 1,
            UpdatedAtUtc = nowUtc
        };
        t.Raise(new TemplateSaved(id, key, tenantId?.Value, 1, nowUtc));
        return t;
    }

    public static NotificationTemplate Rehydrate(
        TemplateId id,
        TemplateKey key,
        string channel,
        string subject,
        string body,
        string? htmlBody,
        string locale,
        TenantId? tenantId,
        int version,
        DateTimeOffset updatedAtUtc)
    {
        return new NotificationTemplate
        {
            Id = id,
            Key = key,
            Channel = channel,
            Subject = subject,
            Body = body,
            HtmlBody = htmlBody,
            Locale = locale,
            TenantId = tenantId,
            Version = version,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    public void UpdateContent(string subject, string body, string? htmlBody, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException("Template subject cannot be empty.");
        if (string.IsNullOrWhiteSpace(body) && string.IsNullOrWhiteSpace(htmlBody))
            throw new DomainException("Template body cannot be empty.");

        Subject = subject.Trim();
        Body = body ?? "";
        HtmlBody = htmlBody;
        Version++;
        UpdatedAtUtc = nowUtc;
        Raise(new TemplateSaved(Id, Key, TenantId?.Value, Version, nowUtc));
    }
}

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByKeyAsync(TemplateKey key, string? tenantId, string? locale, CancellationToken ct = default);
    Task AddAsync(NotificationTemplate template, CancellationToken ct = default);
    Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default);
}
