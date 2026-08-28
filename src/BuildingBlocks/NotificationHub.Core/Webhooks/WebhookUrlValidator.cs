using System.Net;
using System.Net.Sockets;

namespace NotificationHub.Core.Webhooks;

/// <summary>
/// Blocks SSRF targets: non-HTTPS, localhost, private/link-local/metadata ranges (SEC-04).
/// </summary>
public static class WebhookUrlValidator
{
    public static bool IsSafe(string? url, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "URL is required";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            error = "URL must be absolute";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Only https webhook URLs are allowed";
            return false;
        }

        if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            error = "Loopback hosts are not allowed";
            return false;
        }

        // Block literal IP hosts that are private / link-local / metadata
        if (IPAddress.TryParse(uri.Host, out var ip) && IsBlockedAddress(ip))
        {
            error = "Private or link-local IP addresses are not allowed";
            return false;
        }

        // Resolve hostname and reject if any A/AAAA is private (basic DNS rebinding mitigation)
        try
        {
            var addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
            if (addresses.Length == 0)
            {
                error = "Host could not be resolved";
                return false;
            }

            foreach (var addr in addresses)
            {
                if (IsBlockedAddress(addr))
                {
                    error = "Resolved address is private or link-local";
                    return false;
                }
            }
        }
        catch (SocketException)
        {
            error = "Host could not be resolved";
            return false;
        }

        return true;
    }

    public static bool IsBlockedAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
                return true;
            // Unique local fc00::/7
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc)
                return true;
            // IPv4-mapped
            if (ip.IsIPv4MappedToIPv6)
                return IsBlockedAddress(ip.MapToIPv4());
            return false;
        }

        var b = ip.GetAddressBytes();
        // 0.0.0.0/8
        if (b[0] == 0) return true;
        // 10.0.0.0/8
        if (b[0] == 10) return true;
        // 127.0.0.0/8
        if (b[0] == 127) return true;
        // 169.254.0.0/16 (link-local + cloud metadata 169.254.169.254)
        if (b[0] == 169 && b[1] == 254) return true;
        // 172.16.0.0/12
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        // 192.168.0.0/16
        if (b[0] == 192 && b[1] == 168) return true;
        // 100.64.0.0/10 (CGNAT)
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
        // 192.0.0.0/24, 192.0.2.0/24 (TEST-NET), 198.18.0.0/15, 198.51.100.0/24, 203.0.113.0/24
        if (b[0] == 192 && b[1] == 0) return true;
        if (b[0] == 198 && (b[1] == 18 || b[1] == 19 || b[1] == 51)) return true;
        if (b[0] == 203 && b[1] == 0 && b[2] == 113) return true;
        // Multicast / reserved
        if (b[0] >= 224) return true;

        return false;
    }
}
