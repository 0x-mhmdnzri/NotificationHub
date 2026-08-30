using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Sdk;

/// <summary>F23 — typed .NET client for NotificationHub HTTP API.</summary>
public sealed class NotificationHubClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public NotificationHubClient(string baseUrl, string apiKey, HttpClient? http = null)
    {
        if (http is null)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
            _ownsHttp = true;
        }
        else
        {
            _http = http;
            _ownsHttp = false;
            _http.BaseAddress ??= new Uri(baseUrl.TrimEnd('/') + "/");
        }
        _http.DefaultRequestHeaders.Remove("X-Api-Key");
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<JsonElement> SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/v1/notifications", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }

    public async Task<JsonElement> GetStatusAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"api/v1/notifications/{id}", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }

    public async Task IdentifyAsync(CdpIdentifyRequest request, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/v1/cdp/identify", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<JsonElement> TrackAsync(CdpTrackRequest request, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/v1/cdp/track", request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }

    public async Task<JsonElement> GetInboxAsync(string userId, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"api/v1/inbox/{Uri.EscapeDataString(userId)}", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }

    public async Task RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/v1/devices", request, ct);
        resp.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}
