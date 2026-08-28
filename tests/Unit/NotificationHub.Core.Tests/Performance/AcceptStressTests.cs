using System.Diagnostics;
using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Performance;

public class AcceptStressTests
{
    [Fact]
    public async Task TC_PERF_010_Accept_1000_Sequential_ReportsLatency()
    {
        await using var db = TestFixtures.CreateDbContext();
        var orch = TestFixtures.CreateOrchestrator(db);
        const int n = 1000;
        var samples = new long[n];
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < n; i++)
        {
            var item = Stopwatch.StartNew();
            var (ok, status) = await orch.AcceptAsync(new NotificationRequest
            {
                Recipient = $"u{i}@ex.com",
                Channel = "email",
                TemplateKey = "welcome"
            });
            item.Stop();
            ok.Should().BeTrue();
            status.Status.Should().Be(DeliveryStatus.Queued);
            samples[i] = item.ElapsedMilliseconds;
        }
        sw.Stop();
        Array.Sort(samples);
        var p50 = samples[(int)(n * 0.50)];
        var p95 = samples[(int)(n * 0.95)];
        var p99 = samples[(int)(n * 0.99)];
        var rps = n / sw.Elapsed.TotalSeconds;
        // Log for CI visibility
        Console.WriteLine($"Accept stress n={n} elapsed={sw.Elapsed.TotalMilliseconds:F0}ms rps={rps:F0} p50={p50}ms p95={p95}ms p99={p99}ms");
        // Soft assertion: path should stay well under 50ms p95 on in-memory DB
        p95.Should().BeLessThan(150);
        rps.Should().BeGreaterThan(50);
    }
}
