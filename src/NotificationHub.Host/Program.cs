using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Queue;
using NotificationHub.Core.RateLimiting;
using NotificationHub.Core.Scheduling;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Webhooks;
using NotificationHub.Host.Middleware;
using NotificationHub.Plugins.Email.SendGrid;
using NotificationHub.Plugins.Email.Smtp;
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

builder.Services.AddSingleton<INotificationQueue, RabbitMqNotificationQueue>();
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<ITemplateEngine, InMemoryTemplateEngine>();
builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();

builder.Services.AddScoped<INotificationStatusStore, PostgresNotificationStatusStore>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
builder.Services.AddScoped<NotificationOrchestrator>();

builder.Services.AddHostedService<NotificationBackgroundWorker>();
builder.Services.AddHostedService<ScheduledNotificationWorker>();

builder.Services.AddSingleton<IPlugin, SendGridEmailPlugin>();
builder.Services.AddSingleton<IPlugin, SmtpEmailPlugin>();
builder.Services.AddSingleton<IPlugin, KavenegarSmsPlugin>();
builder.Services.AddSingleton<IPlugin, SmsIrPlugin>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
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
    await loader.LoadFromAssembliesAsync(scope.ServiceProvider.GetServices<IPlugin>().Select(p => p.GetType().Assembly).Distinct(), ctx);
}

app.MapPost("/api/v1/notifications", async (NotificationRequest request, NotificationOrchestrator orch, INotificationQueue queue, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    var limit = config.GetValue("RateLimiting:PerMinute", 60);
    if (!await rl.IsAllowedAsync($"tenant:{request.TenantId ?? "default"}:{request.Channel ?? "any"}", limit, ct))
        return Results.StatusCode(429);

    var (accepted, status) = await orch.AcceptAsync(request, ct);
    if (!accepted) return Results.Conflict(status);
    if (status.Status == DeliveryStatus.Suppressed)
        return Results.Ok(new { id = status.NotificationId, status = status.Status.ToString(), reason = status.ErrorMessage });
    if (status.Status != DeliveryStatus.Scheduled)
        await queue.EnqueueAsync(request, ct);

    return Results.Accepted($"/api/v1/notifications/{status.NotificationId}", new { id = status.NotificationId, status = status.Status.ToString() });
}).WithName("SendNotification").WithOpenApi();

app.MapPost("/api/v1/notifications/sync", async (NotificationRequest request, NotificationOrchestrator orch, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    var limit = config.GetValue("RateLimiting:PerMinute", 60);
    if (!await rl.IsAllowedAsync($"tenant:{request.TenantId ?? "default"}:{request.Channel ?? "any"}", limit, ct))
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

app.MapGet("/api/v1/plugins", (PluginLoader loader) => loader.LoadedPlugins.Select(p => new { p.Id, p.Name, Version = p.Version.ToString(), p.Capabilities }))
.WithName("ListPlugins").WithOpenApi();

app.MapPost("/api/v1/templates", async (TemplateDefinition t, ITemplateEngine engine, CancellationToken ct) =>
{
    await engine.RegisterTemplateAsync(t, ct);
    return Results.Created($"/api/v1/templates/{t.Key}", t);
}).WithName("RegisterTemplate").WithOpenApi();

app.MapGet("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, ITemplateEngine engine, CancellationToken ct) =>
{
    var t = await engine.GetTemplateAsync(key, channel, locale ?? "en", tenantId, ct);
    return t is null ? Results.NotFound() : Results.Ok(t);
}).WithName("GetTemplate").WithOpenApi();

app.MapPost("/api/v1/templates/preview", async (NotificationRequest request, ITemplateEngine engine, CancellationToken ct) =>
{
    var rendered = await engine.RenderAsync(request, ct);
    return Results.Ok(rendered);
}).WithName("PreviewTemplate").WithOpenApi();

app.MapGet("/api/v1/preferences/{userId}", async (string userId, string? tenantId, IPreferenceService prefs, CancellationToken ct) =>
{
    var p = await prefs.GetAsync(userId, tenantId, ct);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).WithName("GetPreferences").WithOpenApi();

app.MapPut("/api/v1/preferences", async (UserPreference pref, IPreferenceService prefs, CancellationToken ct) =>
{
    await prefs.SaveAsync(pref, ct);
    return Results.NoContent();
}).WithName("SavePreferences").WithOpenApi();

app.MapPost("/api/v1/webhooks", async (WebhookSubscription sub, NotificationDbContext db, CancellationToken ct) =>
{
    db.WebhookSubscriptions.Add(new WebhookSubscriptionEntity
    {
        Id = sub.Id, Url = sub.Url, Secret = sub.Secret,
        EventsJson = System.Text.Json.JsonSerializer.Serialize(sub.Events),
        TenantId = sub.TenantId, IsActive = sub.IsActive
    });
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
