using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.Analytics;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Engagement;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Common;
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
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
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
    var ctx = new NotificationHub.Host.SimplePluginContext(scope.ServiceProvider, config, loggerFactory.CreateLogger("PluginContext"));
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
}).WithName("SendNotification");

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
}).WithName("SendNotificationSync");

app.MapGet("/api/v1/notifications/{id:guid}", async (Guid id, HttpContext http, INotificationStatusStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var s = await store.GetAsync(id, ct);
    if (s is null) return Results.NotFound();
    if (!http.CanAccessTenant(s.TenantId)) return Results.NotFound();
    return Results.Ok(s);
}).WithName("GetNotificationStatus");

app.MapGet("/api/v1/plugins", (HttpContext http, PluginLoader loader) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(loader.LoadedPlugins.Select(p => new { p.Id, p.Name, Version = p.Version.ToString(), Capabilities = p.Capabilities }));
}).WithName("ListPlugins");

app.MapPost("/api/v1/templates", async (TemplateDefinition t, HttpContext http, ITemplateEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    t = t with { TenantId = http.ResolveTenantId(t.TenantId) };
    if (!RequestValidators.TryValidate(t, out var valErr))
        return Results.BadRequest(new { error = valErr });
    await engine.RegisterTemplateAsync(t, ct);
    return Results.Created($"/api/v1/templates/{t.Key}", t);
}).WithName("RegisterTemplate");

app.MapGet("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, HttpContext http, ITemplateEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var t = await engine.GetTemplateAsync(key, channel, locale ?? "en", tid, ct);
    return t is null ? Results.NotFound() : Results.Ok(t);
}).WithName("GetTemplate");

app.MapGet("/api/v1/templates", async (string? tenantId, string? channel, HttpContext http, ITemplateStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    return Results.Ok(await store.ListAsync(tid, channel, ct));
}).WithName("ListTemplates");

app.MapDelete("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, HttpContext http, ITemplateStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var ok = await store.DeleteAsync(key, channel, locale ?? "en", tid, ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).WithName("DeleteTemplate");

app.MapPost("/api/v1/templates/preview", async (NotificationRequest request, HttpContext http, ITemplateEngine engine, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    request = request with { TenantId = http.ResolveTenantId(request.TenantId) };
    if (!RequestValidators.TryValidate(request, out var valErr))
        return Results.BadRequest(new { error = valErr });
    return Results.Ok(await engine.RenderAsync(request, ct));
}).WithName("PreviewTemplate");

app.MapGet("/api/v1/preferences/{userId}", async (string userId, string? tenantId, HttpContext http, IPreferenceService prefs, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    var p = await prefs.GetAsync(userId, tid, ct);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).WithName("GetPreferences");

app.MapPut("/api/v1/preferences", async (UserPreference pref, HttpContext http, IPreferenceService prefs, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied) return denied;
    pref = pref with { TenantId = http.ResolveTenantId(pref.TenantId) };
    await prefs.SaveAsync(pref, ct);
    return Results.NoContent();
}).WithName("SavePreferences");

app.MapPost("/api/v1/webhooks", async (WebhookSubscription sub, HttpContext http, NotificationDbContext db, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    if (!WebhookUrlValidator.IsSafe(sub.Url, out var urlErr))
        return Results.BadRequest(new { error = urlErr });
    var tenantId = http.ResolveTenantId(sub.TenantId);
    var id = ServerIds.New();
    db.WebhookSubscriptions.Add(new WebhookSubscriptionEntity
    {
        Id = id,
        Url = sub.Url, Secret = sub.Secret, EventsJson = System.Text.Json.JsonSerializer.Serialize(sub.Events),
        TenantId = tenantId,
        IsActive = sub.IsActive
    });
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/webhooks/{id}", sub with { Id = id, TenantId = tenantId });
}).WithName("CreateWebhook");
