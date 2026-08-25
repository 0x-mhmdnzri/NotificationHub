using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Email.Ses;

/// <summary>F17/F34 — Amazon SES v2 SendEmail with AWS SigV4.</summary>
public sealed class SesEmailPlugin : IChannelPlugin
{
    private string? _accessKey, _secretKey, _region, _from;
    private HttpClient? _http;
    private ILogger? _logger;

    public string Id => "email-ses";
    public Version Version => new(1, 1, 0);
    public string Name => "Amazon SES";
    public string Channel => "email";
    public PluginCapability[] Capabilities => [new("channel", "email"), new("provider", "ses")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _accessKey = context.Configuration["Plugins:Ses:AccessKeyId"];
        _secretKey = context.Configuration["Plugins:Ses:SecretAccessKey"];
        _region = context.Configuration["Plugins:Ses:Region"] ?? "us-east-1";
        _from = context.Configuration["Plugins:Ses:From"];
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(
            !string.IsNullOrWhiteSpace(_accessKey) && !string.IsNullOrWhiteSpace(_secretKey) && !string.IsNullOrWhiteSpace(_from),
            "OK"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_accessKey) || string.IsNullOrWhiteSpace(_secretKey) || string.IsNullOrWhiteSpace(_from))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "SES not configured" };

        try
        {
            var host = $"email.{_region}.amazonaws.com";
            var path = "/v2/email/outbound-emails";
            var payload = new
            {
                FromEmailAddress = _from,
                Destination = new { ToAddresses = new[] { notification.Recipient } },
                Content = new
                {
                    Simple = new
                    {
                        Subject = new { Data = notification.Subject ?? "(no subject)" },
                        Body = new
                        {
                            Text = new { Data = notification.Body },
                            Html = string.IsNullOrEmpty(notification.HtmlBody) ? null : new { Data = notification.HtmlBody }
                        }
                    }
                }
            };
            var body = JsonSerializer.Serialize(payload);
            var now = DateTime.UtcNow;
            var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var contentHash = ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

            var canonicalHeaders =
                $"content-type:application/json\n" +
                $"host:{host}\n" +
                $"x-amz-date:{amzDate}\n";
            var signedHeaders = "content-type;host;x-amz-date";
            var canonicalRequest = $"POST\n{path}\n\n{canonicalHeaders}\n{signedHeaders}\n{contentHash}";
            var credentialScope = $"{dateStamp}/{_region}/ses/aws4_request";
            var stringToSign =
                $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
            var signingKey = GetSignatureKey(_secretKey!, dateStamp, _region!, "ses");
            var signature = ToHex(HmacSha256(signingKey, stringToSign));
            var auth =
                $"AWS4-HMAC-SHA256 Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{host}{path}");
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            req.Headers.TryAddWithoutValidation("Authorization", auth);

            using var resp = await _http.SendAsync(req, cancellationToken);
            var respBody = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (resp.IsSuccessStatusCode)
                return new DeliveryResult { Success = true, ProviderId = Id, ProviderMessageId = Guid.NewGuid().ToString("N") };

            return new DeliveryResult
            {
                Success = false,
                ProviderId = Id,
                ErrorCode = $"http_{(int)resp.StatusCode}",
                ErrorMessage = respBody.Length > 400 ? respBody[..400] : respBody
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SES send failed");
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "SES_ERROR", ErrorMessage = ex.Message };
        }
    }

    public static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + key), dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    public static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    public static string ToHex(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();
}
