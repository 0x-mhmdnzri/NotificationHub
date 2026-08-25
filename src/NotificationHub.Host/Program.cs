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
using NotificationHub.Host.Middleware;
using NotificationHub.Plugins.Chat.Slack;
using NotificationHub.Plugins.Chat.WhatsApp;
using NotificationHub.Plugins.Email.SendGrid;
using NotificationHub.Plugins.Email.Smtp;
using NotificationHub.Plugins.InApp;
using NotificationHub.Plugins.Push.Fcm;
using NotificationHub.Plugins.Sms.Kavenegar;
using NotificationHub.Plugins.Sms.SmsIr;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("webhooks");

var cs = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' missing");

builder.Services.AddDbContext<NotificationDbContext>(opt =>
    opt.UseNpgsql(cs, n => { n.EnableRetryOnFailure(3); n.CommandTimeout(30); }));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<ProviderOptions>(builder.Configuration.GetSection("Providers"));
builder.Services.Configure<ProviderHealthOptions>(builder.Configuration.GetSection(ProviderHealthOptions.SectionName));
builder.Services.AddSingleton<IProviderHealthTracker, InMemoryProviderHealthTracker>();
builder.Services.AddSingleton<IProviderRouter, HealthAwareProviderRouter>();
builder.Services.AddScoped<IOutbox, EfOutbox>();
builder.Services.AddScoped<IInbox, EfInbox>();
builder.Services.AddHostedService<OutboxRelayWorker>();
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
builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();

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
builder.Services.AddSingleton<IPlugin, KavenegarSmsPlugin>();
builder.Services.AddSingleton<IPlugin, SmsIrPlugin>();
builder.Services.AddSingleton<IPlugin, InAppPlugin>();
builder.Services.AddSingleton<IPlugin, SlackPlugin>();
builder.Services.AddSingleton<IPlugin, WhatsAppPlugin>();
builder.Services.AddSingleton<IPlugin, FcmPushPlugin>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<TemplateSeeder>();
    await seeder.SeedDefaultsAsync();
    var keyBootstrap = scope.ServiceProvider.GetRequiredService<ApiKeyBootstrapper>();
    await keyBootstrap.EnsureBootstrapKeyAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

app.MapPost("/api/v1/notifications", async (NotificationRequest request, HttpContext http, NotificationOrchestrator orch, INotificationQueue queue, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Sender) is { } denied) return denied;
    var tenantId = http.ResolveTenantId(request.TenantId);
    request = request with { TenantId = tenantId };
    var limit = config.GetValue("RateLimiting:PerMinute", 60);
    if (!await rl.IsAllowedAsync($"tenant:{tenantId ?? "default"}:{request.Channel ?? "any"}", limit, ct))
        return Results.StatusCode(429);
    var (accepted, status) = await orch.AcceptAsync(request, ct);
    if (!accepted) return Results.Conflict(status);
    if (status.Status == DeliveryStatus.Suppressed)
        return Results.Ok(new { id = status.NotificationId, status = status.Status.ToString(), reason = status.ErrorMessage });
    if (status.Status != DeliveryStatus.Scheduled)
        await queue.EnqueueAsync(request, ct);
    return Results.Accepted($"/api/v1/notifications/{status.NotificationId}", new { id = status.NotificationId, status = status.Status.ToString() });
}).WithName("SendNotification").WithOpenApi();

app.MapPost("/api/v1/notifications/sync", async (NotificationRequest request, HttpContext http, NotificationOrchestrator orch, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Sender) is { } denied) return denied;
    var tenantId = http.ResolveTenantId(request.TenantId);
    request = request with { TenantId = tenantId };
    var limit = config.GetValue("RateLimiting:PerMinute", 60);
    if (!await rl.IsAllowedAsync($"tenant:{tenantId ?? "default"}:{request.Channel ?? "any"}", limit, ct))
        return Results.StatusCode(429);
    var (accepted, status) = await orch.AcceptAsync(request, ct);
    if (!accepted) return Results.Conflict(status);
    if (status.Status == DeliveryStatus.Suppressed)
        return Results.Ok(new { status = "Suppressed", reason = status.ErrorMessage });
    var result = await orch.ProcessAsync(request, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).WithName("SendNotificationSync").WithOpenApi();

app.MapGet("/api/v1/notifications/{id:guid}", async (Guid id, INotificationStatusStore store, CancellationToken ct) =>
{
    var s = await store.GetAsync(id, ct);
    return s is null ? Results.NotFound() : Results.Ok(s);
}).WithName("GetNotificationStatus").WithOpenApi();

app.MapGet("/api/v1/plugins", (PluginLoader loader) =>
    loader.LoadedPlugins.Select(p => new { p.Id, p.Name, Version = p.Version.ToString(), Capabilities = p.Capabilities }))
.WithName("ListPlugins").WithOpenApi();

app.MapPost("/api/v1/templates", async (TemplateDefinition t, ITemplateEngine engine, CancellationToken ct) =>
{ await engine.RegisterTemplateAsync(t, ct); return Results.Created($"/api/v1/templates/{t.Key}", t); }).WithName("RegisterTemplate").WithOpenApi();

app.MapGet("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, ITemplateEngine engine, CancellationToken ct) =>
{ var t = await engine.GetTemplateAsync(key, channel, locale ?? "en", tenantId, ct); return t is null ? Results.NotFound() : Results.Ok(t); }).WithName("GetTemplate").WithOpenApi();

app.MapGet("/api/v1/templates", async (string? tenantId, string? channel, ITemplateStore store, CancellationToken ct) =>
    Results.Ok(await store.ListAsync(tenantId, channel, ct))).WithName("ListTemplates").WithOpenApi();

app.MapDelete("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, ITemplateStore store, CancellationToken ct) =>
{
    var ok = await store.DeleteAsync(key, channel, locale ?? "en", tenantId, ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).WithName("DeleteTemplate").WithOpenApi();

app.MapPost("/api/v1/templates/preview", async (NotificationRequest request, ITemplateEngine engine, CancellationToken ct) =>
    Results.Ok(await engine.RenderAsync(request, ct))).WithName("PreviewTemplate").WithOpenApi();

app.MapGet("/api/v1/preferences/{userId}", async (string userId, string? tenantId, IPreferenceService prefs, CancellationToken ct) =>
{ var p = await prefs.GetAsync(userId, tenantId, ct); return p is null ? Results.NotFound() : Results.Ok(p); }).WithName("GetPreferences").WithOpenApi();

app.MapPut("/api/v1/preferences", async (UserPreference pref, IPreferenceService prefs, CancellationToken ct) =>
{ await prefs.SaveAsync(pref, ct); return Results.NoContent(); }).WithName("SavePreferences").WithOpenApi();

app.MapPost("/api/v1/webhooks", async (WebhookSubscription sub, NotificationDbContext db, CancellationToken ct) =>
{
    db.WebhookSubscriptions.Add(new WebhookSubscriptionEntity { Id = sub.Id, Url = sub.Url, Secret = sub.Secret, EventsJson = System.Text.Json.JsonSerializer.Serialize(sub.Events), TenantId = sub.TenantId, IsActive = sub.IsActive });
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/webhooks/{sub.Id}", sub);
}).WithName("RegisterWebhook").WithOpenApi();

app.MapGet("/api/v1/audit", async (Guid? notificationId, string? tenantId, int take, NotificationDbContext db, CancellationToken ct) =>
{
    take = take <= 0 ? 50 : Math.Min(take, 200);
    var q = db.AuditEntries.AsNoTracking().OrderByDescending(x => x.CreatedAt).AsQueryable();
    if (notificationId.HasValue) q = q.Where(x => x.NotificationId == notificationId);
    if (!string.IsNullOrEmpty(tenantId)) q = q.Where(x => x.TenantId == tenantId);
    return Results.Ok(await q.Take(take).ToListAsync(ct));
}).WithName("GetAudit").WithOpenApi();

// Phase 3 APIs
app.MapPost("/api/v1/workflows", async (WorkflowDefinition def, IWorkflowEngine engine, CancellationToken ct) =>
    Results.Created($"/api/v1/workflows/{def.Key}", await engine.SaveAsync(def, ct))).WithName("SaveWorkflow").WithOpenApi();

app.MapGet("/api/v1/workflows/{key}", async (string key, string? tenantId, IWorkflowEngine engine, CancellationToken ct) =>
{ var w = await engine.GetAsync(key, tenantId, ct); return w is null ? Results.NotFound() : Results.Ok(w); }).WithName("GetWorkflow").WithOpenApi();

app.MapPost("/api/v1/workflows/start", async (WorkflowStartRequest request, IWorkflowEngine engine, CancellationToken ct) =>
{ var id = await engine.StartAsync(request, ct); return Results.Accepted($"/api/v1/workflows/runs/{id}", new { runId = id }); }).WithName("StartWorkflow").WithOpenApi();

app.MapGet("/api/v1/workflows/runs/{runId:guid}", async (Guid runId, IWorkflowEngine engine, CancellationToken ct) =>
{
    var run = await engine.GetRunAsync(runId, ct);
    return run is null ? Results.NotFound() : Results.Ok(run);
}).WithName("GetWorkflowRun").WithOpenApi();

app.MapGet("/api/v1/workflows/runs/{runId:guid}/timeline", async (Guid runId, IWorkflowEngine engine, CancellationToken ct) =>
{
    var run = await engine.GetRunAsync(runId, ct);
    if (run is null) return Results.NotFound();
    var timeline = await engine.GetTimelineAsync(runId, ct);
    return Results.Ok(timeline);
}).WithName("GetWorkflowTimeline").WithOpenApi();

app.MapPost("/api/v1/workflows/runs/{runId:guid}/cancel", async (Guid runId, IWorkflowEngine engine, CancellationToken ct) =>
{
    var ok = await engine.CancelAsync(runId, ct);
    return ok ? Results.NoContent() : Results.NotFound();
}).WithName("CancelWorkflowRun").WithOpenApi();



app.MapPost("/api/v1/segments", async (SegmentDefinition seg, ISegmentService segments, CancellationToken ct) =>
    Results.Created($"/api/v1/segments/{seg.Key}", await segments.SaveAsync(seg, ct))).WithName("SaveSegment").WithOpenApi();

app.MapPost("/api/v1/segments/{key}/match", async (string key, Dictionary<string, object?> attributes, string? tenantId, ISegmentService segments, CancellationToken ct) =>
    Results.Ok(new { matched = await segments.MatchesAsync(key, attributes, tenantId, ct) })).WithName("MatchSegment").WithOpenApi();


// Engagement ingest (authenticated)
app.MapPost("/api/v1/engagements", async (EngagementIngestRequest request, HttpContext http, IEngagementService engagement, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied) return denied;
    var tenantId = http.ResolveTenantId(request.TenantId);
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
    }, ct);
    return Results.Accepted($"/api/v1/notifications/{request.NotificationId}/engagements", evt);
}).WithName("TrackEngagement").WithOpenApi();

app.MapGet("/api/v1/notifications/{id:guid}/engagements", async (Guid id, HttpContext http, IEngagementService engagement, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied) return denied;
    return Results.Ok(await engagement.ListByNotificationAsync(id, ct));
}).WithName("ListEngagements").WithOpenApi();

// Public tracking endpoints (no API key) — open pixel + click redirect
app.MapGet("/t/o/{notificationId:guid}", async (Guid notificationId, HttpContext http, IEngagementService engagement, CancellationToken ct) =>
{
    await engagement.TrackAsync(new EngagementEvent
    {
        NotificationId = notificationId,
        EventType = EngagementEventTypes.Open,
        Channel = "email",
        UserAgent = http.Request.Headers.UserAgent.ToString(),
        IpAddress = http.Connection.RemoteIpAddress?.ToString()
    }, ct);

    // 1x1 transparent GIF
    var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
    return Results.File(gif, "image/gif");
}).WithName("TrackOpenPixel").ExcludeFromDescription();

app.MapGet("/t/c/{notificationId:guid}", async (Guid notificationId, string url, HttpContext http, IEngagementService engagement, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var target))
        return Results.BadRequest(new { error = "Valid absolute url query parameter required" });

    await engagement.TrackAsync(new EngagementEvent
    {
        NotificationId = notificationId,
        EventType = EngagementEventTypes.Click,
        Channel = "email",
        Url = url,
        UserAgent = http.Request.Headers.UserAgent.ToString(),
        IpAddress = http.Connection.RemoteIpAddress?.ToString()
    }, ct);

    return Results.Redirect(target.ToString());
}).WithName("TrackClickRedirect").ExcludeFromDescription();

app.MapGet("/api/v1/analytics/summary", async (DateTimeOffset? from, DateTimeOffset? to, string? tenantId, IAnalyticsService analytics, CancellationToken ct) =>
    Results.Ok(await analytics.GetSummaryAsync(from, to, tenantId, ct))).WithName("AnalyticsSummary").WithOpenApi();


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

app.MapPost("/api/v1/admin/retention/sweep", async (HttpContext http, IRetentionService retention, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    return Results.Ok(await retention.SweepAsync(ct));
}).WithName("RunRetentionSweep").WithOpenApi();

app.MapGet("/api/v1/compliance/export/{userId}", async (string userId, string? tenantId, IComplianceService compliance, CancellationToken ct) =>
    Results.Ok(await compliance.ExportUserAsync(userId, tenantId, ct))).WithName("ComplianceExport").WithOpenApi();

app.MapDelete("/api/v1/compliance/users/{userId}", async (string userId, string? tenantId, HttpContext http, IComplianceService compliance, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    var tid = http.ResolveTenantId(tenantId);
    await compliance.DeleteUserAsync(userId, tid, ct);
    return Results.NoContent();
}).WithName("ComplianceDelete").WithOpenApi();

app.MapGet("/api/v1/inapp/{userId}", async (string userId, string? tenantId, bool unreadOnly, NotificationDbContext db, CancellationToken ct) =>
{
    var q = db.InAppMessages.AsNoTracking().Where(x => x.UserId == userId);
    if (!string.IsNullOrEmpty(tenantId)) q = q.Where(x => x.TenantId == tenantId);
    if (unreadOnly) q = q.Where(x => !x.IsRead);
    return Results.Ok(await q.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct));
}).WithName("ListInApp").WithOpenApi();

app.MapPost("/api/v1/inapp/{id:guid}/read", async (Guid id, NotificationDbContext db, CancellationToken ct) =>
{
    var msg = await db.InAppMessages.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (msg is null) return Results.NotFound();
    msg.IsRead = true;
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}).WithName("MarkInAppRead").WithOpenApi();

app.MapGet("/api/v1/providers/health", (IProviderHealthTracker health) =>
    Results.Ok(health.GetAll())).WithName("GetProviderHealth").WithOpenApi();


app.MapPost("/api/v1/admin/api-keys", async (CreateApiKeyRequest request, HttpContext http, IApiKeyStore store, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied) return denied;
    // Non-global admin tenant binding: if caller has tenant, force it
    var auth = http.GetAuthContext()!;
    var tenantId = auth.TenantId ?? request.TenantId;
    if (!auth.IsAdmin) return Results.Forbid();
    var plain = ApiKeyHasher.GeneratePlainKey();
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

app.MapGet("/api/v1/admin/providers", (PluginLoader loader) =>
    loader.LoadedPlugins.OfType<IChannelPlugin>().Select(p => new { p.Id, p.Name, p.Channel, Version = p.Version.ToString(), Capabilities = p.Capabilities }))
.WithName("AdminProviders").WithOpenApi();

app.MapGet("/api/v1/admin/monitoring", async (IAnalyticsService analytics, PluginLoader loader, CancellationToken ct) =>
{
    var summary = await analytics.GetSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), null, null, ct);
    return Results.Ok(new
    {
        last24h = summary,
        plugins = loader.LoadedPlugins.Select(p => new { p.Id, p.Name })
    });
}).WithName("AdminMonitoring").WithOpenApi();

app.MapGet("/health", async (NotificationDbContext db, CancellationToken ct) =>
{
    var up = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new { status = up ? "healthy" : "degraded", database = up ? "up" : "down", timestamp = DateTimeOffset.UtcNow });
});

app.Run();

file sealed class SimplePluginContext : IPluginContext
{
    public SimplePluginContext(IServiceProvider s, IConfiguration c, ILogger l) { Services = s; Configuration = c; Logger = l; }
    public IServiceProvider Services { get; }
    public IConfiguration Configuration { get; }
    public ILogger Logger { get; }
}
