using System.Net;

namespace NotificationHub.Core.Webhooks;

/// <summary>Open-redirect protection for click-tracking redirects (SEC-07).</summary>
public static class RedirectUrlValidator
{
    public static bool IsSafe(string? url, out string? error, out Uri? target)
    {
        error = null;
        target = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Valid absolute url query parameter required";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            error = "Valid absolute url query parameter required";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            error = "Only http/https redirect targets allowed";
            return false;
        }

        if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            error = "Loopback redirect targets are not allowed";
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var ip) && WebhookUrlValidator.IsBlockedAddress(ip))
        {
            error = "Private or link-local redirect targets are not allowed";
            return false;
        }

        // Block credential-in-URL and overly long targets
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Credentials in redirect URL are not allowed";
            return false;
        }

        if (url.Length > 2048)
        {
            error = "Redirect URL too long";
            return false;
        }

        target = uri;
        return true;
    }
}
