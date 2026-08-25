
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Chat.Telegram;

/// <summary>F15 — Telegram Bot API sendMessage (HTML parse mode).</summary>
public sealed class TelegramPlugin : IChannelPlugin
{
    private string? _token;
    private HttpClient? _http;
    private ILogger? _logger;

    public string Id => "chat-telegram";
    public Version Version => new(1, 0, 0);
    public string Name => "Telegram Bot API";
    public string Channel => "telegram";
    public PluginCapability[] Capabilities => [new("channel", "telegram"), new("provider", "telegram")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _token = context.Configuration["Plugins:Telegram:BotToken"];
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_token), string.IsNullOrWhiteSpace(_token) ? "Missing BotToken" : "OK"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_token))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "Telegram BotToken not configured" };

        var chatId = notification.Recipient;
        if (string.IsNullOrWhiteSpace(chatId))
            return new DeliveryResult { Success = false, ErrorCode = "INVALID_RECIPIENT", ErrorMessage = "chat_id required" };

        var text = string.IsNullOrWhiteSpace(notification.Subject)
            ? notification.Body
            : $"<b>{EscapeHtml(notification.Subject)}</b>\n{EscapeHtml(notification.Body)}";
        if (text.Length > 4096) text = text[..4096];

        try
        {
            var url = $"https://api.telegram.org/bot{_token}/sendMessage";
            using var resp = await _http.PostAsJsonAsync(url, new
            {
                chat_id = chatId,
                text,
                parse_mode = "HTML",
                disable_web_page_preview = true
            }, cancellationToken);

            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Telegram send failed {Status}: {Body}", resp.StatusCode, body.Length > 200 ? body[..200] : body);
                // 429 flood control
                if ((int)resp.StatusCode == 429)
                    return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "FLOOD", ErrorMessage = body };
                return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = $"http_{(int)resp.StatusCode}", ErrorMessage = body.Length > 300 ? body[..300] : body };
            }

            string? msgId = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("result", out var r) && r.TryGetProperty("message_id", out var mid))
                    msgId = mid.GetRawText();
            }
            catch { /* ignore */ }

            return new DeliveryResult { Success = true, ProviderId = Id, ProviderMessageId = msgId ?? Guid.NewGuid().ToString("N") };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Telegram send exception");
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "TELEGRAM_ERROR", ErrorMessage = ex.Message };
        }
    }

    internal static string EscapeHtml(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

