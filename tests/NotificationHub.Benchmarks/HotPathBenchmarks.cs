using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Common;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Webhooks;

namespace NotificationHub.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class HotPathBenchmarks
{
    private NotificationOrchestrator _orch = null!;
    private PlaceholderTemplateRenderer _renderer = null!;
    private NotificationRequest _base = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase("bench-" + Guid.NewGuid()).Options;
        var db = new NotificationDbContext(options);
        db.Database.EnsureCreated();

        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        var health = new InMemoryProviderHealthTracker(Options.Create(new ProviderHealthOptions()));
        var providerOptions = Options.Create(new ProviderOptions { PreferredEmailProvider = "email-sendgrid", PreferredSmsProvider = "sms-kavenegar" });
        var healthOptions = Options.Create(new ProviderHealthOptions());
        var router = new HealthAwareProviderRouter(loader, health, providerOptions, healthOptions, NullLogger<HealthAwareProviderRouter>.Instance);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var cachedPrefs = new CachingPreferenceService(new PreferenceService(db), cache);

        _orch = new NotificationOrchestrator(
            loader,
            new TemplateEngine(new EmptyTemplateStore(), new PlaceholderTemplateRenderer(), NullLogger<TemplateEngine>.Instance),
            new PostgresNotificationStatusStore(db),
            cachedPrefs,
            new ConsentService(db),
            new EfOutbox(db),
            new AuditService(db),
            new NoopWebhooks(),
            router,
            health,
            NullLogger<NotificationOrchestrator>.Instance);

        _renderer = new PlaceholderTemplateRenderer();
        _base = new NotificationRequest
        {
            Recipient = "bench@example.com", Channel = "email", TemplateKey = "welcome",
            Data = new Dictionary<string, object?> { ["name"] = "Bench" }
        };
    }

    [Benchmark]
    public Guid ServerId_New() => ServerIds.New();

    [Benchmark]
    public string Template_Render_Placeholder()
        => _renderer.Render("Hello {{name}}, welcome to {{product}}!",
            new Dictionary<string, object?> { ["name"] = "Ada", ["product"] = "Hub" });

    [Benchmark(Baseline = true)]
    public async Task AcceptAsync_ColdRecipient()
    {
        var req = _base with { Recipient = $"u{Guid.NewGuid():N}@ex.com" };
        await _orch.AcceptAsync(req);
    }

    private sealed class NoopWebhooks : IWebhookDispatcher
    {
        public Task DispatchAsync(string eventName, object payload, string? tenantId = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyTemplateStore : ITemplateStore
    {
        public Task SaveAsync(TemplateDefinition template, CancellationToken ct = default) => Task.CompletedTask;
        public Task<TemplateDefinition?> FindAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default) => Task.FromResult<TemplateDefinition?>(null);
        public Task<IReadOnlyList<TemplateDefinition>> ListAsync(string? tenantId = null, string? channel = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateDefinition>>([]);
        public Task<bool> DeleteAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default) => Task.FromResult(false);
    }
}
