using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

// NotificationHub lightweight HTTP load test for accept-path + queue pressure.
// Usage:
//   dotnet run --project tools/loadtest -- --baseUrl http://localhost:8080 --apiKey dev-secret-key-change-me --total 1000 --concurrency 50

var argsMap = ParseArgs(args);
var baseUrl = argsMap.GetValueOrDefault("--baseUrl", "http://localhost:8080").TrimEnd('/');
var apiKey = argsMap.GetValueOrDefault("--apiKey", "dev-secret-key-change-me");
var total = int.Parse(argsMap.GetValueOrDefault("--total", "500"));
var concurrency = int.Parse(argsMap.GetValueOrDefault("--concurrency", "25"));
var channel = argsMap.GetValueOrDefault("--channel", "email");
var template = argsMap.GetValueOrDefault("--template", "welcome");
var healthEvery = int.Parse(argsMap.GetValueOrDefault("--healthEvery", "100"));

Console.WriteLine($"LoadTest target={baseUrl} total={total} concurrency={concurrency} channel={channel}");

using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

var latencies = new ConcurrentBag<long>();
var statusCodes = new ConcurrentDictionary<int, int>();
var errors = new ConcurrentBag<string>();
var sw = Stopwatch.StartNew();
var completed = 0;

await Parallel.ForEachAsync(
    Enumerable.Range(0, total),
    new ParallelOptions { MaxDegreeOfParallelism = concurrency },
    async (i, ct) =>
    {
        var payload = new
        {
            recipient = channel == "sms" ? $"+98912{i:D7}" : $"user{i}@example.com",
            channel,
            templateKey = template,
            data = new Dictionary<string, object?> { ["name"] = $"User{i}" },
            idempotencyKey = $"lt-{Guid.NewGuid():N}"
        };

        var itemSw = Stopwatch.StartNew();
        try
        {
            using var resp = await http.PostAsJsonAsync("/api/v1/notifications", payload, ct);
            itemSw.Stop();
            latencies.Add(itemSw.ElapsedMilliseconds);
            statusCodes.AddOrUpdate((int)resp.StatusCode, 1, (_, c) => c + 1);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                errors.Add($"{(int)resp.StatusCode}: {Trim(body, 120)}");
            }
        }
        catch (Exception ex)
        {
            itemSw.Stop();
            latencies.Add(itemSw.ElapsedMilliseconds);
            statusCodes.AddOrUpdate(0, 1, (_, c) => c + 1);
            errors.Add(ex.Message);
        }

        var done = Interlocked.Increment(ref completed);
        if (healthEvery > 0 && done % healthEvery == 0)
        {
            try
            {
                var health = await http.GetFromJsonAsync<JsonElement>("/api/v1/admin/messaging/health", ct);
                Console.WriteLine(
                    $"progress={done}/{total} outboxPending={GetInt(health, "outboxPendingCount")} " +
                    $"dlq={GetInt(health, "dlqDepth")} workQueue={GetInt(health, "workQueueDepth")} " +
                    $"oldestAgeSec={GetDouble(health, "oldestPendingAgeSeconds"):F0}");
            }
            catch
            {
                // health endpoint may require admin role; ignore sampling failures
            }
        }
    });

sw.Stop();
var sorted = latencies.OrderBy(x => x).ToArray();
Console.WriteLine();
Console.WriteLine("=== Results ===");
Console.WriteLine($"Elapsed: {sw.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"Throughput: {(total / Math.Max(0.001, sw.Elapsed.TotalSeconds)):F1} req/s");
Console.WriteLine($"Latency p50={Percentile(sorted, 0.50)}ms p95={Percentile(sorted, 0.95)}ms p99={Percentile(sorted, 0.99)}ms");
Console.WriteLine("Status codes:");
foreach (var kv in statusCodes.OrderBy(k => k.Key))
    Console.WriteLine($"  {kv.Key}: {kv.Value}");
if (!errors.IsEmpty)
{
    Console.WriteLine("Sample errors:");
    foreach (var e in errors.Distinct().Take(10))
        Console.WriteLine($"  - {e}");
}

// Prefetch tuning hint from observed profile
Console.WriteLine();
Console.WriteLine("=== Prefetch tuning hint ===");
Console.WriteLine("If workQueueDepth grows while CPU/provider are idle → increase PrefetchCount carefully (e.g. 10→20→50).");
Console.WriteLine("If provider rate-limits / latency spikes → decrease PrefetchCount (e.g. 50→20→10) to reduce in-flight pressure.");
Console.WriteLine("Rule of thumb: Prefetch ≈ desired_in_flight_per_consumer; total in-flight ≈ Prefetch × consumer_instances.");
Environment.Exit(statusCodes.Where(k => k.Key is < 200 or >= 300).Sum(k => k.Value) > 0 ? 1 : 0);

static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        var key = args[i];
        var val = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
        map[key] = val;
    }
    return map;
}

static long Percentile(long[] sorted, double p)
{
    if (sorted.Length == 0) return 0;
    var idx = (int)Math.Clamp(Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1);
    return sorted[idx];
}

static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "...";

static int GetInt(JsonElement el, string name)
{
    if (el.ValueKind != JsonValueKind.Object) return -1;
    foreach (var prop in el.EnumerateObject())
        if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) && prop.Value.TryGetInt32(out var v))
            return v;
    return -1;
}

static double GetDouble(JsonElement el, string name)
{
    if (el.ValueKind != JsonValueKind.Object) return -1;
    foreach (var prop in el.EnumerateObject())
        if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) && prop.Value.TryGetDouble(out var v))
            return v;
    return -1;
}
