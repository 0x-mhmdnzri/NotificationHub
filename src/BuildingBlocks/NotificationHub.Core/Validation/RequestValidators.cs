using System.Text.RegularExpressions;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Validation;

/// <summary>Central input validation for public API DTOs (SEC-12).</summary>
public static partial class RequestValidators
{
    private static readonly HashSet<string> AllowedChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "sms", "push", "inapp", "slack", "whatsapp"
    };

    public static bool TryValidate(NotificationRequest request, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.Recipient) || request.Recipient.Length > 512)
        {
            error = "Recipient is required and must be <= 512 characters";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.TemplateKey) || request.TemplateKey.Length > 128
            || !KeyPattern().IsMatch(request.TemplateKey))
        {
            error = "TemplateKey is required, max 128 chars, alphanumeric/._- only";
            return false;
        }

        if (!string.IsNullOrEmpty(request.Channel) && !AllowedChannels.Contains(request.Channel))
        {
            error = $"Channel must be one of: {string.Join(", ", AllowedChannels)}";
            return false;
        }

        if (request.Channels is { Length: > 0 })
        {
            if (request.Channels.Length > 8)
            {
                error = "At most 8 channels allowed";
                return false;
            }
            foreach (var ch in request.Channels)
            {
                if (!AllowedChannels.Contains(ch))
                {
                    error = $"Invalid channel '{ch}'";
                    return false;
                }
            }
        }

        if (request.Data.Count > 64)
        {
            error = "Data dictionary supports at most 64 entries";
            return false;
        }

        if (request.IdempotencyKey is { Length: > 256 })
        {
            error = "IdempotencyKey max length is 256";
            return false;
        }

        if (request.TenantId is { Length: > 128 })
        {
            error = "TenantId max length is 128";
            return false;
        }

        if (request.Attachments is { Count: > 5 })
        {
            error = "At most 5 attachments allowed";
            return false;
        }

        if (request.Attachments is not null)
        {
            const int maxBytes = 2 * 1024 * 1024; // 2 MB each
            foreach (var a in request.Attachments)
            {
                if (string.IsNullOrWhiteSpace(a.FileName) || a.FileName.Length > 255)
                {
                    error = "Attachment FileName required and max 255 chars";
                    return false;
                }
                if (a.Content.Length > maxBytes)
                {
                    error = "Each attachment must be <= 2MB";
                    return false;
                }
            }
        }

        return true;
    }

    public static bool TryValidate(TemplateDefinition t, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(t.Key) || t.Key.Length > 128 || !KeyPattern().IsMatch(t.Key))
        {
            error = "Key is required, max 128, alphanumeric/._-";
            return false;
        }
        if (string.IsNullOrWhiteSpace(t.Channel) || !AllowedChannels.Contains(t.Channel))
        {
            error = "Valid Channel is required";
            return false;
        }
        if (string.IsNullOrWhiteSpace(t.Subject) || t.Subject.Length > 512)
        {
            error = "Subject is required and max 512 chars";
            return false;
        }
        if (string.IsNullOrWhiteSpace(t.Body) || t.Body.Length > 200_000)
        {
            error = "Body is required and max 200000 chars";
            return false;
        }
        if (t.Locale is { Length: > 16 })
        {
            error = "Locale max 16 chars";
            return false;
        }
        return true;
    }

    public static bool TryValidate(WebhookSubscription sub, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(sub.Url) || sub.Url.Length > 2048)
        {
            error = "Url is required and max 2048 chars";
            return false;
        }
        if (sub.Events is { Length: > 32 })
        {
            error = "At most 32 event types";
            return false;
        }
        if (sub.Secret is { Length: > 256 })
        {
            error = "Secret max 256 chars";
            return false;
        }
        return true;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
