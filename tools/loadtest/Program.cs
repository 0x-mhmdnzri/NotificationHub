using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;

var argsMap = ParseArgs(args);
var baseUrl = argsMap.GetValueOrDefault("--baseUrl", "http://localhost:8080").TrimEnd('/');
var apiKey = argsMap.GetValueOrDefault("--apiKey", "dev-secret-key-change-me");
var total = int.Parse(argsMap.GetValueOrDefault("--total", "1000"));
var concurrency = int.Parse(argsMap.GetValueOrDefault("--concurrency", "50"));
var channel = argsMap.GetValueOrDefault("--channel", "email");
var warmup = int.Parse(argsMap.GetValueOrDefault("--warmup", "50"));

Console.WriteLine($"Stress target={baseUrl} total={total} concurrency={concurrency} warmup={warmup}");
using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

for (var i = 0; i < warmup; i++) await SendOnce(http, channel, i);

var latencies = new ConcurrentBag<long>();
var statusCodes = new ConcurrentDictionary<int, int>();
var errors = new ConcurrentBag<string>();
var sw = Stopwatch.StartNew();

await Parallel.ForEachAsync(Enumerable.Range(0, total), new ParallelOptions { MaxDegreeOfParallelism = concurrency },
    async (i, ct) =>
    {
        var itemSw = Stopwatch.StartNew();
        try
        {
            var code = await SendOnce(http, channel, i, ct);
            itemSw.Stop();
            latencies.Add(itemSw.ElapsedMilliseconds);
            statusCodes.AddOrUpdate(code, 1, (_, c) => c + 1);
        }
        catch (Exception ex)
        {
            itemSw.Stop();
            latencies.Add(itemSw.ElapsedMilliseconds);
            statusCodes.AddOrUpdate(0, 1, (_, c) => c + 1);
            errors.Add(ex.Message);
        }
    });
sw.Stop();
var sorted = latencies.OrderBy(x => x).ToArray();
Console.WriteLine();
Console.WriteLine("=== Stress / latency results ===");
Console.WriteLine($"Elapsed: {sw.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"Throughput: {(total / Math.Max(0.001, sw.Elapsed.TotalSeconds)):F1} req/s");
Console.WriteLine($"Latency ms  p50={Pct(sorted, 0.50)} p75={Pct(sorted, 0.75)} p90={Pct(sorted, 0.90)} p95={Pct(sorted, 0.95)} p99={Pct(sorted, 0.99)} max={sorted.LastOrDefault()}");
Console.WriteLine("Status codes:");
foreach (var kv in statusCodes.OrderBy(k => k.Key)) Console.WriteLine($"  {kv.Key}: {kv.Value}");
if (!errors.IsEmpty) { Console.WriteLine("Sample errors:"); foreach (var e in errors.Distinct().Take(8)) Console.WriteLine("  - " + e); }
Console.WriteLine("Histogram (ms):");
foreach (var (label, lo, hi) in new (string, long, long)[] { ("0-5", 0, 5), ("5-10", 5, 10), ("10-25", 10, 25), ("25-50", 25, 50), ("50-100", 50, 100), ("100-250", 100, 250), ("250+", 250, long.MaxValue) })
{
    var n = sorted.Count(x => x >= lo && x < hi);
    var bar = new string('#', Math.Min(40, n * 40 / Math.Max(1, sorted.Length)));
    Console.WriteLine($"  {label,8}: {n,5} {bar}");
}
Environment.Exit(statusCodes.Where(k => k.Key is < 200 or >= 300 and not 202).Sum(k => k.Value) > total / 10 ? 1 : 0);

static async Task<int> SendOnce(HttpClient http, string channel, int i, CancellationToken ct = default)
{
    var payload = new
    {
        recipient = channel == "sms" ? $"+98912{i:D7}" : $"user{i}@example.com",
        channel,
        templateKey = "welcome",
        data = new Dictionary<string, object?> { ["name"] = $"User{i}" },
        idempotencyKey = $"st-{Guid.NewGuid():N}"
    };
    using var resp = await http.PostAsJsonAsync("/api/v1/notifications", payload, ct);
    return (int)resp.StatusCode;
}
static long Pct(long[] sorted, double p) { if (sorted.Length == 0) return 0; var idx = (int)Math.Clamp(Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1); return sorted[idx]; }
static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    { if (!args[i].StartsWith("--")) continue; var key = args[i]; var val = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true"; map[key] = val; }
    return map;
}
