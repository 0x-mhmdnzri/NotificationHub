using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow.Handlers;

/// <summary>F14 — HTTP enrichment step. Config JSON: { "url", "method", "headers", "bodyTemplate", "next" }.</summary>
public sealed class HttpStepHandler : IWorkflowStepHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpStepHandler> _logger;

    public HttpStepHandler(IHttpClientFactory httpClientFactory, ILogger<HttpStepHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string StepType => "http";

    public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, WorkflowRunEntity run, WorkflowDefinition definition, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(step.ConfigJson) ? "{}" : step.ConfigJson);
            var root = doc.RootElement;
            var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return new StepExecutionResult(null, null, false, true, "Invalid or missing url in http step", "http_error", "bad url");
            }

            // SSRF basics: block loopback
            if (uri.IsLoopback)
                return new StepExecutionResult(null, null, false, true, "Loopback URL not allowed", "http_error", "ssrf");

            var method = root.TryGetProperty("method", out var m) ? m.GetString() ?? "GET" : "GET";
            var client = _httpClientFactory.CreateClient("workflow-http");
            using var req = new HttpRequestMessage(new HttpMethod(method), uri);

            if (root.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object)
            {
                foreach (var h in headers.EnumerateObject())
                    req.Headers.TryAddWithoutValidation(h.Name, h.Value.GetString());
            }

            if (root.TryGetProperty("bodyTemplate", out var body) && method is not "GET" and not "HEAD")
            {
                var raw = body.GetString() ?? "{}";
                req.Content = new StringContent(raw, Encoding.UTF8, "application/json");
            }

            using var resp = await client.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (text.Length > 32_000)
                text = text[..32_000];

            var next = root.TryGetProperty("next", out var n) ? n.GetString() : null;
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP step {Status} for run {Run}", resp.StatusCode, run.Id);
                return new StepExecutionResult(next, null, false, true, $"HTTP {(int)resp.StatusCode}", "http_error", text);
            }

            // Store response snippet into run context if available
            return new StepExecutionResult(next, null, string.IsNullOrEmpty(next), false, null, "http_ok", text[..Math.Min(500, text.Length)]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP step failed");
            return new StepExecutionResult(null, null, false, true, ex.Message, "http_error", ex.Message);
        }
    }
}
