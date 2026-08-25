using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.Analytics;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Engagement;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Queue;
using NotificationHub.Core.RateLimiting;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Security;
using NotificationHub.Core.Scheduling;
using NotificationHub.Core.Segmentation;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Webhooks;
using NotificationHub.Core.Workflow;
using NotificationHub.Core.Workflow.Handlers;
using NotificationHub.Core.Expressions;
using NotificationHub.Core.Validation;
using NotificationHub.Core.Activity;
using NotificationHub.Core.Auth;
using NotificationHub.Core.Observability;
using NotificationHub.Core.I18n;
using NotificationHub.Core.Campaigns;
using NotificationHub.Core.Cdp;
using NotificationHub.Core.Environments;
using NotificationHub.Core.Sync;
using NotificationHub.Core.Layouts;
using NotificationHub.Core.Devices;
using NotificationHub.Core.Topics;
using NotificationHub.Core.Throttle;
using NotificationHub.Core.Digest;
using NotificationHub.Core.Inbox;
using NotificationHub.Host.Middleware;
using NotificationHub.Plugins.Chat.Slack;
using NotificationHub.Plugins.Chat.WhatsApp;
using NotificationHub.Plugins.Email.SendGrid;
using NotificationHub.Plugins.Email.Smtp;
using NotificationHub.Plugins.InApp;
using NotificationHub.Plugins.Push.Fcm;
using NotificationHub.Plugins.Sms.Kavenegar;
using NotificationHub.Plugins.Sms.SmsIr;
using NotificationHub.Plugins.Push.Expo;
using NotificationHub.Plugins.Email.Ses;
using NotificationHub.Plugins.Email.Resend;
using NotificationHub.Plugins.Sms.Twilio;
using NotificationHub.Plugins.Chat.Teams;
using NotificationHub.Plugins.Chat.Discord;
using NotificationHub.Plugins.Chat.Telegram;

var builder = WebApplication.CreateBuilder(args);

// SEC-26: limit request body size (DoS)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2 * 1024 * 1024); // 2 MB

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("webhooks", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});

// SEC-16 CORS — only when origins are configured
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddPolicy("AppCors", p =>
        p.WithOrigins(corsOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "X-Api-Key", "X-Correlation-ID", "Authorization")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
}

// Prefer known proxies' X-Forwarded-For when Admin IP allowlist is used
// SEC-25: only trust configured proxies (empty = do not trust arbitrary X-Forwarded-For)
builder.Services.Configure<Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    var proxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
    foreach (var px in proxies)
    {
        if (System.Net.IPAddress.TryParse(px, out var ip))
            options.KnownProxies.Add(ip);
    }
    // If no proxies configured, still clear defaults but RequireHeaderSymmetry stays false.
    // Operators MUST set ForwardedHeaders:KnownProxies in production behind a load balancer.
});

var cs = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' missing");

builder.Services.AddDbContext<NotificationDbContext>(opt =>
    opt.UseNpgsql(cs, n => { n.EnableRetryOnFailure(3); n.CommandTimeout(30); }));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<ProviderOptions>(builder.Configuration.GetSection("Providers"));
builder.Services.Configure<ProviderHealthOptions>(builder.Configuration.GetSection(ProviderHealthOptions.SectionName));
builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));
builder.Services.AddSingleton<IProviderHealthTracker, CircuitBreakerProviderHealthTracker>();
builder.Services.AddSingleton<IProviderRouter, HealthAwareProviderRouter>();
builder.Services.AddScoped<IOutbox, EfOutbox>();
builder.Services.AddScoped<IInbox, EfInbox>();
builder.Services.AddHostedService<OutboxRelayWorker>();
builder.Services.Configure<MessagingHealthOptions>(builder.Configuration.GetSection(MessagingHealthOptions.SectionName));
builder.Services.AddScoped<IMessagingHealthService, MessagingHealthService>();
builder.Services.AddHostedService<MessagingHealthMonitorWorker>();
builder.Services.AddScoped<IApiKeyStore, PostgresApiKeyStore>();
builder.Services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddScoped<ApiKeyBootstrapper>();
builder.Services.Configure<CostOptions>(builder.Configuration.GetSection(CostOptions.SectionName));

// Transport consumer/publisher (singleton connection)
builder.Services.AddSingleton<RabbitMqNotificationQueue>();
// API enqueue path = transactional outbox (scoped with DbContext)
builder.Services.AddScoped<INotificationQueue, OutboxNotificationQueue>();
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddScoped<ITemplateStore, PostgresTemplateStore>();
builder.Services.AddSingleton<ITemplateRenderer, PlaceholderTemplateRenderer>();
builder.Services.AddScoped<ITemplateEngine, TemplateEngine>();
builder.Services.AddScoped<TemplateSeeder>();
// SEC-24: Redis rate limiter when ConnectionStrings:Redis is set; otherwise in-memory
var redisCs = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisCs))
{
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
        StackExchange.Redis.ConnectionMultiplexer.Connect(redisCs));
    builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();
    builder.Services.AddSingleton<IInboxEventBus, RedisInboxEventBus>();
}
else
{
    builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
    builder.Services.AddSingleton<IInboxEventBus, InMemoryInboxEventBus>();
}

builder.Services.AddScoped<INotificationStatusStore, PostgresNotificationStatusStore>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
builder.Services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();
builder.Services.AddScoped<IWorkflowTimeline, WorkflowTimeline>();
builder.Services.AddSingleton<IExpressionEvaluator, SimpleExpressionEvaluator>();
builder.Services.AddScoped<IWorkflowStepHandler, DelayStepHandler>();
builder.Services.AddScoped<IWorkflowStepHandler, ConditionStepHandler>();
builder.Services.AddScoped<IWorkflowStepHandler, BranchStepHandler>();
builder.Services.AddScoped<IWorkflowStepHandler, SendStepHandler>();
builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();
builder.Services.AddScoped<ISegmentService, SegmentService>();
builder.Services.AddScoped<IEngagementService, EngagementService>();
builder.Services.AddScoped<IInboxFeedService, InboxFeedService>();
builder.Services.AddScoped<IDigestService, DigestService>();
builder.Services.AddHostedService<DigestFlushWorker>();
builder.Services.AddScoped<IThrottleService, ThrottleService>();
builder.Services.AddScoped<ITopicService, TopicService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddSingleton<IEnvironmentContext, EnvironmentContext>();
builder.Services.AddScoped<ICdpService, CdpService>();
builder.Services.AddScoped<IBroadcastService, BroadcastService>();
builder.Services.AddScoped<ILocalizationCatalog, LocalizationCatalog>();
builder.Services.AddSingleton<IMetricsService, InMemoryMetricsService>();
builder.Services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.SectionName));
builder.Services.AddScoped<ILayoutService, LayoutService>();
builder.Services.AddScoped<ICrossChannelReadSync, CrossChannelReadSync>();
builder.Services.AddScoped<IWorkflowStepHandler, HttpStepHandler>();
builder.Services.AddHttpClient("workflow-http", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IConsentService, ConsentService>();
builder.Services.AddScoped<IComplianceService, ComplianceService>();
builder.Services.AddScoped<IRetentionService, RetentionService>();
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddHostedService<RetentionBackgroundWorker>();
builder.Services.AddScoped<NotificationOrchestrator>();

builder.Services.AddHostedService<NotificationBackgroundWorker>();
builder.Services.AddHostedService<ScheduledNotificationWorker>();
builder.Services.AddHostedService<WorkflowBackgroundWorker>();

builder.Services.AddSingleton<IPlugin, SendGridEmailPlugin>();
builder.Services.AddSingleton<IPlugin, SmtpEmailPlugin>();
builder.Services.AddSingleton<IPlugin, ResendEmailPlugin>();
builder.Services.AddSingleton<IPlugin, SesEmailPlugin>();
builder.Services.AddSingleton<IPlugin, KavenegarSmsPlugin>();
builder.Services.AddSingleton<IPlugin, SmsIrPlugin>();
builder.Services.AddSingleton<IPlugin, TwilioSmsPlugin>();
builder.Services.AddSingleton<IPlugin, InAppPlugin>();
builder.Services.AddSingleton<IPlugin, SlackPlugin>();
builder.Services.AddSingleton<IPlugin, WhatsAppPlugin>();
builder.Services.AddSingleton<IPlugin, TelegramPlugin>();
builder.Services.AddSingleton<IPlugin, DiscordPlugin>();
builder.Services.AddSingleton<IPlugin, TeamsPlugin>();
builder.Services.AddSingleton<IPlugin, FcmPushPlugin>();
builder.Services.AddSingleton<IPlugin, ExpoPushPlugin>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
    await Phase1Schema.EnsureAsync(db, scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Phase1Schema"));
    await Phase2Schema.EnsureAsync(db, scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Phase2Schema"));
    await Phase4Schema.EnsureAsync(db, scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Phase4Schema"));
    var seeder = scope.ServiceProvider.GetRequiredService<TemplateSeeder>();
    await seeder.SeedDefaultsAsync();
    var keyBootstrap = scope.ServiceProvider.GetRequiredService<ApiKeyBootstrapper>();
    await keyBootstrap.EnsureBootstrapKeyAsync();
}

app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (corsOrigins.Length > 0)
    app.UseCors("AppCors");

app.UseMiddleware<AdminIpAllowlistMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var loader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var ctx = new SimplePluginContext(scope.ServiceProvider, config, loggerFactory.CreateLogger("PluginContext"));
    foreach (var plugin in scope.ServiceProvider.GetServices<IPlugin>())
    {
        await plugin.InitializeAsync(ctx);
        await plugin.StartAsync();
        loader.Register(plugin);
    }
}

app.MapPost("/api/v1/notifications", async (
    NotificationRequest request,
    HttpContext http,
    NotificationOrchestrator orch,
    INotificationQueue queue,
    IRateLimiter rl,
    IThrottleService throttle,
    IConfiguration config,
    NotificationDbContext db,
    CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Sender) is { } denied) return denied;
    var tenantId = http.ResolveTenantId(request.TenantId);
    request = request with { TenantId = tenantId };
    var limit = config.GetValue("RateLimiting:PerMinute", 60);
    if (!await rl.IsAllowedAsync($"tenant:{tenantId ?? "default"}:{request.Channel ?? "any"}", limit, ct))
        return Results.StatusCode(429);
    if (!RequestValidators.TryValidate(request, out var valErr))
        return Results.BadRequest(new { error = valErr });
    var (thOk, thReason) = await throttle.CheckAndIncrementAsync(request.Recipient, request.Channel, tenantId, ct);
    if (!thOk) return Results.Json(new { error = thReason }, statusCode: 429);

    // Single transaction: status (Queued) + outbox row commit together (no dual-write window).
    // Execution strategy required when EnableRetryOnFailure is on and we use explicit transactions.
    IResult? result = null;
    var strategy = db.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (accepted, status) = await orch.AcceptAsync(request, ct);
        if (!accepted)
        {
            await tx.RollbackAsync(ct);
            result = Results.Conflict(status);
            return;
        }

        if (status.Status == DeliveryStatus.Suppressed)
        {
            await tx.CommitAsync(ct);
            result = Results.Ok(new { id = status.NotificationId, status = status.Status.ToString(), reason = status.ErrorMessage });
            return;
        }

        if (status.Status != DeliveryStatus.Scheduled)
        {
            // Stages outbox entity only; SaveChanges persists it inside this transaction.
            await queue.EnqueueAsync(request, ct);
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        result = Results.Accepted($"/api/v1/notifications/{status.NotificationId}", new { id = status.NotificationId, status = status.Status.ToString() });
    });

    return result!;
}).WithName("SendNotification").WithOpenApi();

app.MapPost("/api/v1/notifications/sync", async (NotificationRequest request, HttpContext http, NotificationOrchestrator orch, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Sender) is { } denied) return denied;
    var tenantId = http.ResolveTenantId(request.TenantId);
    request = request with { TenantId = tenantId };
    var limit = config.GetValue("RateLimiting:PerMinute", 60);
    if (!await rl.IsAllowedAsync($"tenant:{tenantId ?? "default"}:{request.Channel ?? "any"}", limit, ct))
        return Results.StatusCode(429);
    if (!RequestValidators.TryValidate(request, out var valErr))
        return Results.BadRequest(new { error = valErr });
    var (accepted, status) = await orch.AcceptAsync(request, ct);
    if (!accepted) return Results.Conflict(status);
    if (status.Status == DeliveryStatus.Suppressed)
        return Results.Ok(new { status = "Suppressed", reason = status.ErrorMessage });
    var delivery = await orch.ProcessAsync(request, ct);
    return delivery.Success ? Results.Ok(delivery) : Results.BadRequest(delivery);
}).WithName("SendNotificationSync").WithOpenApi();

app.MapGet("/api/v1/notifications/{id:guid}", async (Guid id, HttpContext http, INotificationStatusStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var s = await store.GetAsync(id, ct);
    if (s is null) return Results.NotFound();
    if (!http.CanAccessTenant(s.TenantId)) return Results.NotFound();
    return Results.Ok(s);
}).WithName("GetNotificationStatus").WithOpenApi();

app.MapGet("/api/v1/plugins", (HttpContext http, PluginLoader loader) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(loader.LoadedPlugins.Select(p => new { p.Id, p.Name, Version = p.Version.ToString(), Capabilities = p.Capabilities }));
}).WithName("ListPlugins").WithOpenApi();

app.MapPost("/api/v1/templates", async (TemplateDefinition t, HttpContext http, ITemplateEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    t = t with { TenantId = http.ResolveTenantId(t.TenantId) };
    if (!RequestValidators.TryValidate(t, out var valErr))
        return Results.BadRequest(new { error = valErr });
    await engine.RegisterTemplateAsync(t, ct);
    return Results.Created($"/api/v1/templates/{t.Key}", t);
}).WithName("RegisterTemplate").WithOpenApi();

app.MapGet("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, HttpContext http, ITemplateEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var t = await engine.GetTemplateAsync(key, channel, locale ?? "en", tid, ct);
    return t is null ? Results.NotFound() : Results.Ok(t);
}).WithName("GetTemplate").WithOpenApi();

app.MapGet("/api/v1/templates", async (string? tenantId, string? channel, HttpContext http, ITemplateStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await store.ListAsync(tid, channel, ct));
}).WithName("ListTemplates").WithOpenApi();

app.MapDelete("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, HttpContext http, ITemplateStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var ok = await store.DeleteAsync(key, channel, locale ?? "en", tid, ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).WithName("DeleteTemplate").WithOpenApi();

app.MapPost("/api/v1/templates/preview", async (NotificationRequest request, HttpContext http, ITemplateEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    request = request with { TenantId = http.ResolveTenantId(request.TenantId) };
    if (!RequestValidators.TryValidate(request, out var valErr))
        return Results.BadRequest(new { error = valErr });
    return Results.Ok(await engine.RenderAsync(request, ct));
}).WithName("PreviewTemplate").WithOpenApi();

app.MapGet("/api/v1/preferences/{userId}", async (string userId, string? tenantId, HttpContext http, IPreferenceService prefs, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var p = await prefs.GetAsync(userId, tid, ct);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).WithName("GetPreferences").WithOpenApi();

app.MapPut("/api/v1/preferences", async (UserPreference pref, HttpContext http, IPreferenceService prefs, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    pref = pref with { TenantId = http.ResolveTenantId(pref.TenantId) };
    await prefs.SaveAsync(pref, ct);
    return Results.NoContent();
}).WithName("SavePreferences").WithOpenApi();

app.MapPost("/api/v1/webhooks", async (WebhookSubscription sub, HttpContext http, NotificationDbContext db, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    if (!RequestValidators.TryValidate(sub, out var valErr))
        return Results.BadRequest(new { error = valErr });
    if (!WebhookUrlValidator.IsSafe(sub.Url, out var urlError))
        return Results.BadRequest(new { error = urlError });
    var tenantId = http.ResolveTenantId(sub.TenantId);
    db.WebhookSubscriptions.Add(new WebhookSubscriptionEntity
    {
        Id = sub.Id == Guid.Empty ? Guid.NewGuid() : sub.Id,
        Url = sub.Url,
        Secret = sub.Secret,
        EventsJson = System.Text.Json.JsonSerializer.Serialize(sub.Events),
        TenantId = tenantId,
        IsActive = sub.IsActive
    });
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/webhooks/{sub.Id}", sub with { TenantId = tenantId });
}).WithName("RegisterWebhook").WithOpenApi();

app.MapGet("/api/v1/audit", async (Guid? notificationId, string? tenantId, int take, HttpContext http, NotificationDbContext db, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied) return denied;
    take = take <= 0 ? 50 : Math.Min(take, 200);
    var tid = http.ResolveTenantId(tenantId);
    var q = db.AuditEntries.AsNoTracking().OrderByDescending(x => x.CreatedAt).AsQueryable();
    if (notificationId.HasValue) q = q.Where(x => x.NotificationId == notificationId);
    if (!string.IsNullOrEmpty(tid)) q = q.Where(x => x.TenantId == tid);
    else if (!http.GetAuthContext()!.IsAdmin) q = q.Where(x => x.TenantId == null);
    return Results.Ok(await q.Take(take).ToListAsync(ct));
}).WithName("GetAudit").WithOpenApi();

// Phase 3 APIs
app.MapPost("/api/v1/workflows", async (WorkflowDefinition def, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    def = def with { TenantId = http.ResolveTenantId(def.TenantId) };
    return Results.Created($"/api/v1/workflows/{def.Key}", await engine.SaveAsync(def, ct));
}).WithName("SaveWorkflow").WithOpenApi();

app.MapGet("/api/v1/workflows/{key}", async (string key, string? tenantId, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var w = await engine.GetAsync(key, tid, ct);
    return w is null ? Results.NotFound() : Results.Ok(w);
}).WithName("GetWorkflow").WithOpenApi();

app.MapPost("/api/v1/workflows/start", async (WorkflowStartRequest request, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    request = request with { TenantId = http.ResolveTenantId(request.TenantId) };
    var id = await engine.StartAsync(request, ct);
    return Results.Accepted($"/api/v1/workflows/runs/{id}", new { runId = id });
}).WithName("StartWorkflow").WithOpenApi();

app.MapGet("/api/v1/workflows/runs/{runId:guid}", async (Guid runId, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var run = await engine.GetRunAsync(runId, ct);
    if (run is null) return Results.NotFound();
    if (!http.CanAccessTenant(run.TenantId)) return Results.NotFound();
    return Results.Ok(run);
}).WithName("GetWorkflowRun").WithOpenApi();

app.MapGet("/api/v1/workflows/runs/{runId:guid}/timeline", async (Guid runId, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var run = await engine.GetRunAsync(runId, ct);
    if (run is null || !http.CanAccessTenant(run.TenantId)) return Results.NotFound();
    return Results.Ok(await engine.GetTimelineAsync(runId, ct));
}).WithName("GetWorkflowTimeline").WithOpenApi();

app.MapPost("/api/v1/workflows/runs/{runId:guid}/cancel", async (Guid runId, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var run = await engine.GetRunAsync(runId, ct);
    if (run is null || !http.CanAccessTenant(run.TenantId)) return Results.NotFound();
    var ok = await engine.CancelAsync(runId, ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).WithName("CancelWorkflowRun").WithOpenApi();



app.MapPost("/api/v1/segments", async (SegmentDefinition seg, HttpContext http, ISegmentService segments, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    seg = seg with { TenantId = http.ResolveTenantId(seg.TenantId) };
    return Results.Created($"/api/v1/segments/{seg.Key}", await segments.SaveAsync(seg, ct));
}).WithName("SaveSegment").WithOpenApi();

app.MapPost("/api/v1/segments/{key}/match", async (string key, Dictionary<string, object?> attributes, string? tenantId, HttpContext http, ISegmentService segments, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(new { matched = await segments.MatchesAsync(key, attributes, tid, ct) });
}).WithName("MatchSegment").WithOpenApi();


// Engagement ingest (authenticated)
app.MapPost("/api/v1/engagements", async (EngagementIngestRequest request, HttpContext http, IEngagementService engagement, INotificationStatusStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    if (request.NotificationId is null || request.NotificationId == Guid.Empty)
        return Results.BadRequest(new { error = "NotificationId is required" });
    var status = await store.GetAsync(request.NotificationId.Value, ct);
    if (status is null || !http.CanAccessTenant(status.TenantId))
        return Results.NotFound();
    var tenantId = http.ResolveTenantId(request.TenantId) ?? status.TenantId;
    var evt = await engagement.TrackAsync(new EngagementEvent
    {
        NotificationId = request.NotificationId,
        TenantId = tenantId,
        EventType = request.EventType,
        Recipient = request.Recipient,
        Channel = request.Channel ?? "email",
        Url = request.Url,
        ProviderId = request.ProviderId,
        UserAgent = http.Request.Headers.UserAgent.ToString(),
        IpAddress = http.Connection.RemoteIpAddress?.ToString(),
        MetadataJson = request.Metadata is null ? null : System.Text.Json.JsonSerializer.Serialize(request.Metadata)
    }, requireExistingNotification: true, ct);
    if (evt is null) return Results.NotFound();
    if (string.Equals(request.EventType, EngagementEventTypes.Open, StringComparison.OrdinalIgnoreCase)
        && request.NotificationId is Guid nid)
    {
        var sync = http.RequestServices.GetRequiredService<ICrossChannelReadSync>();
        await sync.SyncReadAsync(nid, request.Recipient, tenantId, ct);
    }
    return Results.Accepted($"/api/v1/notifications/{request.NotificationId}/engagements", evt);
}).WithName("TrackEngagement").WithOpenApi();

app.MapGet("/api/v1/notifications/{id:guid}/engagements", async (Guid id, HttpContext http, IEngagementService engagement, INotificationStatusStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied) return denied;
    var status = await store.GetAsync(id, ct);
    if (status is null || !http.CanAccessTenant(status.TenantId)) return Results.NotFound();
    return Results.Ok(await engagement.ListByNotificationAsync(id, ct));
}).WithName("ListEngagements").WithOpenApi();

// Public tracking endpoints (no API key) — open pixel + click redirect
app.MapGet("/t/o/{notificationId:guid}", async (Guid notificationId, HttpContext http, IEngagementService engagement, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var trackLimit = config.GetValue("RateLimiting:TrackingPerMinute", 120);
    if (!await rl.IsAllowedAsync($"track:ip:{ip}:{notificationId}", trackLimit, ct))
        return Results.StatusCode(429);

    // SEC-22: only persist when notification exists (silent no-op otherwise)
    _ = await engagement.TrackAsync(new EngagementEvent
    {
        NotificationId = notificationId,
        EventType = EngagementEventTypes.Open,
        Channel = "email",
        UserAgent = http.Request.Headers.UserAgent.ToString(),
        IpAddress = http.Connection.RemoteIpAddress?.ToString()
    }, requireExistingNotification: true, ct);

    // 1x1 transparent GIF
    var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
    return Results.File(gif, "image/gif");
}).WithName("TrackOpenPixel").ExcludeFromDescription();

app.MapGet("/t/c/{notificationId:guid}", async (Guid notificationId, string url, HttpContext http, IEngagementService engagement, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var trackLimit = config.GetValue("RateLimiting:TrackingPerMinute", 120);
    if (!await rl.IsAllowedAsync($"track:ip:{ip}:{notificationId}", trackLimit, ct))
        return Results.StatusCode(429);

    if (!RedirectUrlValidator.IsSafe(url, out var redirectError, out var target) || target is null)
        return Results.BadRequest(new { error = redirectError });

    _ = await engagement.TrackAsync(new EngagementEvent
    {
        NotificationId = notificationId,
        EventType = EngagementEventTypes.Click,
        Channel = "email",
        Url = url,
        UserAgent = http.Request.Headers.UserAgent.ToString(),
        IpAddress = http.Connection.RemoteIpAddress?.ToString()
    }, requireExistingNotification: true, ct);

    return Results.Redirect(target.ToString());
}).WithName("TrackClickRedirect").ExcludeFromDescription();

app.MapGet("/api/v1/analytics/summary", async (DateTimeOffset? from, DateTimeOffset? to, string? tenantId, HttpContext http, IAnalyticsService analytics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await analytics.GetSummaryAsync(from, to, tid, ct));
}).WithName("AnalyticsSummary").WithOpenApi();


app.MapPost("/api/v1/consents", async (ConsentRecord record, HttpContext http, IConsentService consents, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var tenantId = http.ResolveTenantId(record.TenantId);
    var auth = http.GetAuthContext();
    var saved = await consents.RecordAsync(record with
    {
        TenantId = tenantId,
        Actor = auth?.KeyName,
        Source = string.IsNullOrWhiteSpace(record.Source) ? "api" : record.Source
    }, ct);
    return Results.Created($"/api/v1/consents/{saved.SubjectId}", saved);
}).WithName("RecordConsent").WithOpenApi();

app.MapGet("/api/v1/consents/{subjectId}", async (string subjectId, string? tenantId, HttpContext http, IConsentService consents, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await consents.ListAsync(subjectId, tid, ct));
}).WithName("ListConsents").WithOpenApi();

app.MapPost("/api/v1/consents/evaluate", async (string subjectId, string purpose, string? channel, string? tenantId, HttpContext http, IConsentService consents, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await consents.EvaluateAsync(subjectId, purpose, channel, tid, ct));
}).WithName("EvaluateConsent").WithOpenApi();

app.MapGet("/api/v1/admin/messaging/health", async (HttpContext http, IMessagingHealthService health, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    return Results.Ok(await health.CheckAsync(ct));
}).WithName("GetMessagingHealth").WithOpenApi();

app.MapPost("/api/v1/admin/retention/sweep", async (HttpContext http, IRetentionService retention, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    return Results.Ok(await retention.SweepAsync(ct));
}).WithName("RunRetentionSweep").WithOpenApi();

app.MapGet("/api/v1/compliance/export/{userId}", async (string userId, string? tenantId, HttpContext http, IComplianceService compliance, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await compliance.ExportUserAsync(userId, tid, ct));
}).WithName("ComplianceExport").WithOpenApi();

app.MapDelete("/api/v1/compliance/users/{userId}", async (string userId, string? tenantId, HttpContext http, IComplianceService compliance, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    await compliance.DeleteUserAsync(userId, tid, ct);
    return Results.NoContent();
}).WithName("ComplianceDelete").WithOpenApi();

app.MapGet("/api/v1/inapp/{userId}", async (string userId, string? tenantId, bool unreadOnly, HttpContext http, NotificationDbContext db, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var q = db.InAppMessages.AsNoTracking().Where(x => x.UserId == userId);
    if (!string.IsNullOrEmpty(tid)) q = q.Where(x => x.TenantId == tid);
    if (unreadOnly) q = q.Where(x => !x.IsRead);
    return Results.Ok(await q.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct));
}).WithName("ListInApp").WithOpenApi();

app.MapPost("/api/v1/inapp/{id:guid}/read", async (Guid id, HttpContext http, NotificationDbContext db, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var msg = await db.InAppMessages.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (msg is null || !http.CanAccessTenant(msg.TenantId)) return Results.NotFound();
    msg.IsRead = true;
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}).WithName("MarkInAppRead").WithOpenApi();

app.MapGet("/api/v1/providers/health", (HttpContext http, IProviderHealthTracker health) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(health.GetAll());
}).WithName("GetProviderHealth").WithOpenApi();


app.MapPost("/api/v1/admin/api-keys", async (CreateApiKeyRequest request, HttpContext http, IApiKeyStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    // Non-global admin tenant binding: if caller has tenant, force it
    var auth = http.GetAuthContext()!;
    var tenantId = auth.TenantId ?? request.TenantId;
    if (!auth.IsAdmin) return Results.Forbid();
    var keyId = Guid.NewGuid();
    var plain = ApiKeyHasher.GeneratePlainKey(keyId);
    var hash = ApiKeyHasher.Hash(plain);
    var created = await store.CreateAsync(request with { TenantId = tenantId }, plain, hash, ct);
    return Results.Created($"/api/v1/admin/api-keys/{created.Id}", created);
}).WithName("CreateApiKey").WithOpenApi();

app.MapGet("/api/v1/admin/api-keys", async (HttpContext http, IApiKeyStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var auth = http.GetAuthContext()!;
    var list = await store.ListAsync(auth.IsAdmin ? null : auth.TenantId, ct);
    return Results.Ok(list);
}).WithName("ListApiKeys").WithOpenApi();

app.MapDelete("/api/v1/admin/api-keys/{id:guid}", async (Guid id, HttpContext http, IApiKeyStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var ok = await store.RevokeAsync(id, ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).WithName("RevokeApiKey").WithOpenApi();


app.MapPost("/api/v1/admin/plugins/reload", async (HttpContext http, PluginLoader loader, IConfiguration config, ILoggerFactory logFactory, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var dir = config["Plugins:Directory"];
    if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        return Results.BadRequest(new { error = "Plugins:Directory not configured or missing" });
    var ctx = new SimplePluginContext(http.RequestServices, config, logFactory.CreateLogger("PluginReload"));
    await loader.ReloadDirectoryAsync(dir, ctx, ct);
    return Results.Ok(new { loaded = loader.LoadedPlugins.Count });
}).WithName("ReloadPlugins").WithOpenApi();

app.MapGet("/api/v1/admin/providers", (HttpContext http, PluginLoader loader) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    return Results.Ok(loader.LoadedPlugins.OfType<IChannelPlugin>().Select(p => new { p.Id, p.Name, p.Channel, Version = p.Version.ToString(), Capabilities = p.Capabilities }));
}).WithName("AdminProviders").WithOpenApi();

app.MapGet("/api/v1/admin/monitoring", async (HttpContext http, IAnalyticsService analytics, PluginLoader loader, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var summary = await analytics.GetSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), null, null, ct);
    return Results.Ok(new
    {
        last24h = summary,
        plugins = loader.LoadedPlugins.Select(p => new { p.Id, p.Name })
    });
}).WithName("AdminMonitoring").WithOpenApi();


// ===== Phase 1: Inbox (F01) =====
app.MapGet("/api/v1/inbox/{userId}", async (string userId, string? tenantId, bool includeArchived, int take, HttpContext http, IInboxFeedService inbox, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await inbox.GetFeedAsync(userId, tid, includeArchived, take <= 0 ? 50 : take, ct));
}).WithName("GetInboxFeed").WithOpenApi();

app.MapPost("/api/v1/inbox/{userId}", async (string userId, InboxItem body, HttpContext http, IInboxFeedService inbox, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var tid = http.ResolveTenantId(body.TenantId);
    var item = await inbox.PushAsync(body with { UserId = userId, TenantId = tid }, ct);
    return Results.Created($"/api/v1/inbox/{userId}", item);
}).WithName("PushInboxItem").WithOpenApi();

app.MapPost("/api/v1/inbox/{userId}/read-all", async (string userId, string? tenantId, HttpContext http, IInboxFeedService inbox, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var n = await inbox.MarkAllReadAsync(userId, tid, ct);
    return Results.Ok(new { marked = n });
}).WithName("MarkInboxAllRead").WithOpenApi();

app.MapPost("/api/v1/inbox/items/{id:guid}/read", async (Guid id, string userId, string? tenantId, HttpContext http, IInboxFeedService inbox, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return await inbox.MarkReadAsync(id, userId, tid, ct) ? Results.NoContent() : Results.NotFound();
}).WithName("MarkInboxRead").WithOpenApi();

app.MapPost("/api/v1/inbox/items/{id:guid}/archive", async (Guid id, string userId, string? tenantId, HttpContext http, IInboxFeedService inbox, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return await inbox.ArchiveAsync(id, userId, tid, ct) ? Results.NoContent() : Results.NotFound();
}).WithName("ArchiveInboxItem").WithOpenApi();

app.MapGet("/api/v1/inbox/{userId}/stream", async (string userId, string? tenantId, HttpContext http, IInboxFeedService inbox, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    http.Response.Headers.ContentType = "text/event-stream";
    await foreach (var item in inbox.StreamAsync(userId, tid, ct))
    {
        var json = System.Text.Json.JsonSerializer.Serialize(item);
        await http.Response.WriteAsync($"data: {json}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
}).WithName("InboxSseStream").ExcludeFromDescription();

// ===== F02 Digest =====
app.MapPost("/api/v1/digest/policies", async (DigestPolicy policy, HttpContext http, IDigestService digest, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    policy = policy with { TenantId = http.ResolveTenantId(policy.TenantId) };
    return Results.Ok(await digest.SavePolicyAsync(policy, ct));
}).WithName("SaveDigestPolicy").WithOpenApi();

app.MapPost("/api/v1/digest/buffer", async (string policyKey, string recipient, string? tenantId, Dictionary<string, object?> payload, HttpContext http, IDigestService digest, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    await digest.BufferAsync(policyKey, recipient, tid, payload, ct);
    return Results.Accepted(null, new { buffered = true });
}).WithName("BufferDigest").WithOpenApi();

app.MapPost("/api/v1/admin/digest/flush", async (HttpContext http, IDigestService digest, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    return Results.Ok(new { flushed = await digest.FlushDueAsync(ct) });
}).WithName("FlushDigest").WithOpenApi();

// ===== F03 Throttle =====
app.MapPost("/api/v1/throttle/policies", async (ThrottlePolicy policy, HttpContext http, IThrottleService throttle, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    policy = policy with { TenantId = http.ResolveTenantId(policy.TenantId) };
    return Results.Ok(await throttle.SavePolicyAsync(policy, ct));
}).WithName("SaveThrottlePolicy").WithOpenApi();

// ===== F04 Topics =====
app.MapPost("/api/v1/topics", async (TopicDefinition topic, HttpContext http, ITopicService topics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    topic = topic with { TenantId = http.ResolveTenantId(topic.TenantId) };
    return Results.Created($"/api/v1/topics/{topic.Key}", await topics.SaveTopicAsync(topic, ct));
}).WithName("SaveTopic").WithOpenApi();

app.MapGet("/api/v1/topics", async (string? tenantId, HttpContext http, ITopicService topics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(await topics.ListTopicsAsync(http.ResolveTenantId(tenantId), ct));
}).WithName("ListTopics").WithOpenApi();

app.MapPost("/api/v1/topics/{key}/subscribe", async (string key, string subscriberId, string? channel, string? address, string? tenantId, HttpContext http, ITopicService topics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    await topics.SubscribeAsync(key, subscriberId, tid, channel, address, ct);
    return Results.NoContent();
}).WithName("SubscribeTopic").WithOpenApi();

app.MapDelete("/api/v1/topics/{key}/subscribers/{subscriberId}", async (string key, string subscriberId, string? tenantId, HttpContext http, ITopicService topics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    await topics.UnsubscribeAsync(key, subscriberId, http.ResolveTenantId(tenantId), ct);
    return Results.NoContent();
}).WithName("UnsubscribeTopic").WithOpenApi();

app.MapGet("/api/v1/topics/{key}/subscribers", async (string key, string? tenantId, HttpContext http, ITopicService topics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(await topics.ListSubscribersAsync(key, http.ResolveTenantId(tenantId), ct));
}).WithName("ListTopicSubscribers").WithOpenApi();

app.MapPost("/api/v1/topics/broadcast", async (TopicBroadcastRequest req, HttpContext http, ITopicService topics, NotificationOrchestrator orch, INotificationQueue queue, NotificationDbContext db, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var tid = http.ResolveTenantId(req.TenantId);
    var subs = await topics.ListSubscribersAsync(req.TopicKey, tid, ct);
    var accepted = 0;
    foreach (var s in subs)
    {
        var recipient = s.Address ?? s.SubscriberId;
        var channel = req.Channel ?? s.Channel ?? "email";
        var nreq = new NotificationRequest
        {
            Recipient = recipient,
            Channel = channel,
            TemplateKey = req.TemplateKey,
            Data = req.Data,
            TenantId = tid,
            Category = $"topic:{req.TopicKey}"
        };
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var (ok, status) = await orch.AcceptAsync(nreq, ct);
            if (!ok) { await tx.RollbackAsync(ct); return; }
            if (status.Status == DeliveryStatus.Queued)
            {
                await queue.EnqueueAsync(nreq, ct);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            Interlocked.Increment(ref accepted);
        });
    }
    return Results.Accepted(null, new { topic = req.TopicKey, accepted, subscribers = subs.Count });
}).WithName("BroadcastTopic").WithOpenApi();

// ===== F05 Devices =====
app.MapPost("/api/v1/devices", async (RegisterDeviceRequest req, HttpContext http, IDeviceService devices, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    req = req with { TenantId = http.ResolveTenantId(req.TenantId) };
    try
    {
        var d = await devices.RegisterAsync(req, ct);
        return Results.Created($"/api/v1/devices/{d.Id}", d);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("RegisterDevice").WithOpenApi();

app.MapGet("/api/v1/devices/{userId}", async (string userId, string? tenantId, HttpContext http, IDeviceService devices, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(await devices.ListAsync(userId, http.ResolveTenantId(tenantId), ct));
}).WithName("ListDevices").WithOpenApi();

app.MapDelete("/api/v1/devices/{userId}", async (string userId, string token, string? tenantId, HttpContext http, IDeviceService devices, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var ok = await devices.UnregisterAsync(userId, token, http.ResolveTenantId(tenantId), ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).WithName("UnregisterDevice").WithOpenApi();

// ===== F06 Activity =====
app.MapGet("/api/v1/admin/activity", async (string? tenantId, int take, HttpContext http, IActivityService activity, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await activity.ListAsync(tid, take <= 0 ? 50 : take, ct));
}).WithName("AdminActivity").WithOpenApi();



// ===== Phase 2 F07–F14 =====
app.MapGet("/api/v1/workflows/{key}/export", async (string key, string? tenantId, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var w = await engine.GetAsync(key, tid, ct);
    if (w is null) return Results.NotFound();
    return Results.Text(WorkflowDsl.Export(w), "application/json");
}).WithName("ExportWorkflow").WithOpenApi();

app.MapPost("/api/v1/workflows/import", async (HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    using var reader = new StreamReader(http.Request.Body);
    var json = await reader.ReadToEndAsync(ct);
    try
    {
        var doc = WorkflowDsl.Import(json);
        var def = doc.Definition with { TenantId = http.ResolveTenantId(doc.Definition.TenantId) };
        WorkflowDsl.Validate(def);
        return Results.Ok(await engine.SaveAsync(def, ct));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("ImportWorkflow").WithOpenApi();

app.MapPost("/api/v1/layouts", async (LayoutDefinition layout, HttpContext http, ILayoutService layouts, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    layout = layout with { TenantId = http.ResolveTenantId(layout.TenantId) };
    try { return Results.Ok(await layouts.SaveLayoutAsync(layout, ct)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("SaveLayout").WithOpenApi();

app.MapPost("/api/v1/partials", async (PartialDefinition partial, HttpContext http, ILayoutService layouts, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    partial = partial with { TenantId = http.ResolveTenantId(partial.TenantId) };
    try { return Results.Ok(await layouts.SavePartialAsync(partial, ct)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("SavePartial").WithOpenApi();

app.MapPost("/api/v1/layouts/render", async (string body, string? layoutKey, string? tenantId, Dictionary<string, object?>? data, HttpContext http, ILayoutService layouts, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    var html = await layouts.RenderHtmlAsync(body, layoutKey, http.ResolveTenantId(tenantId), data ?? new(), ct);
    return Results.Content(html, "text/html");
}).WithName("RenderLayout").WithOpenApi();

app.MapGet("/api/v1/preferences/{userId}/embed", async (string userId, string? tenantId, HttpContext http, IPreferenceService prefs, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(await prefs.GetEmbedModelAsync(userId, http.ResolveTenantId(tenantId), ct));
}).WithName("PreferenceEmbed").WithOpenApi();

app.MapPost("/api/v1/notifications/{id:guid}/sync-read", async (Guid id, string? userId, string? tenantId, HttpContext http, ICrossChannelReadSync sync, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var n = await sync.SyncReadAsync(id, userId, http.ResolveTenantId(tenantId), ct);
    return Results.Ok(new { marked = n });
}).WithName("SyncCrossChannelRead").WithOpenApi();



// ===== Phase 4 F22–F30 =====
app.MapGet("/api/v1/environment", (HttpContext http, IEnvironmentContext env) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(new { env.Name, env.IsProduction, env.AllowDangerousOperations });
}).WithName("GetEnvironment").WithOpenApi();

app.MapPost("/api/v1/cdp/identify", async (CdpIdentifyRequest req, HttpContext http, ICdpService cdp, IMetricsService metrics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    req = req with { TenantId = http.ResolveTenantId(req.TenantId) };
    var profile = await cdp.IdentifyAsync(req, ct);
    metrics.Increment("cdp.identify");
    return Results.Ok(profile);
}).WithName("IdentifyCdp").WithOpenApi();

app.MapPost("/api/v1/cdp/track", async (CdpTrackRequest req, HttpContext http, ICdpService cdp, IMetricsService metrics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    req = req with { TenantId = http.ResolveTenantId(req.TenantId) };
    var (profile, runId, _) = await cdp.TrackAsync(req, ct);
    metrics.Increment("cdp.track", 1, ("event", req.Event));
    return Results.Ok(new { profile, workflowRunId = runId });
}).WithName("TrackCdp").WithOpenApi();

app.MapGet("/api/v1/cdp/profiles/{userId}", async (string userId, string? tenantId, HttpContext http, ICdpService cdp, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var p = await cdp.GetProfileAsync(userId, http.ResolveTenantId(tenantId), ct);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).WithName("GetCdpProfile").WithOpenApi();

app.MapPost("/api/v1/campaigns/broadcast", async (BroadcastRequest req, HttpContext http, IBroadcastService broadcast, IMetricsService metrics, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    req = req with { TenantId = http.ResolveTenantId(req.TenantId) };
    var result = await broadcast.SendAsync(req, ct);
    metrics.Increment("campaign.broadcast.accepted", result.Accepted);
    metrics.Increment("campaign.broadcast.failed", result.Failed);
    return Results.Accepted(null, result);
}).WithName("BroadcastCampaign").WithOpenApi();

app.MapPost("/api/v1/i18n", async (string key, string locale, string value, string? tenantId, HttpContext http, ILocalizationCatalog i18n, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    await i18n.SetAsync(key, locale, value, http.ResolveTenantId(tenantId), ct);
    return Results.NoContent();
}).WithName("SetLocalization").WithOpenApi();

app.MapGet("/api/v1/i18n/{locale}", async (string locale, string? tenantId, HttpContext http, ILocalizationCatalog i18n, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(await i18n.GetAllAsync(locale, http.ResolveTenantId(tenantId), ct));
}).WithName("GetLocalization").WithOpenApi();

app.MapGet("/api/v1/admin/metrics", (HttpContext http, IMetricsService metrics) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    return Results.Ok(metrics.Snapshot());
}).WithName("AdminMetrics").WithOpenApi();

app.MapGet("/api/v1/admin/auth/oidc", (HttpContext http, Microsoft.Extensions.Options.IOptions<OidcOptions> oidc) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var o = oidc.Value;
    return Results.Ok(new { o.Enabled, o.Authority, o.ClientId, o.Audience, o.Scopes, hasSecret = !string.IsNullOrEmpty(o.ClientSecret) });
}).WithName("OidcConfig").WithOpenApi();

app.MapPost("/api/v1/workflows/code-first", async (WorkflowDefinition def, HttpContext http, IWorkflowEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    try
    {
        WorkflowDsl.Validate(def);
        def = def with { TenantId = http.ResolveTenantId(def.TenantId) };
        return Results.Ok(await engine.SaveAsync(def, ct));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("SaveCodeFirstWorkflow").WithOpenApi();


// SEC-28: public health is minimal; detailed checks under admin messaging health
app.MapGet("/health", (HttpContext http) =>
    Results.Ok(new { status = "ok", correlationId = http.GetCorrelationId() }));

app.MapGet("/health/ready", async (HttpContext http, NotificationDbContext db, CancellationToken ct) =>
{
    // Still public but used by orchestrators; keep detail minimal
    var up = await db.Database.CanConnectAsync(ct);
    return up
        ? Results.Ok(new { status = "ready", correlationId = http.GetCorrelationId() })
        : Results.Json(new { status = "not_ready", correlationId = http.GetCorrelationId() }, statusCode: 503);
});

app.Run();

file sealed class SimplePluginContext : IPluginContext
{
    public SimplePluginContext(IServiceProvider s, IConfiguration c, ILogger l) { Services = s; Configuration = c; Logger = l; }
    public IServiceProvider Services { get; }
    public IConfiguration Configuration { get; }
    public ILogger Logger { get; }
}
