using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Queue;
using NotificationHub.Core.RateLimiting;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Host.Middleware;
using NotificationHub.Plugins.Email.SendGrid;
using NotificationHub.Plugins.Email.Smtp;
using NotificationHub.Plugins.Sms.Kavenegar;
using NotificationHub.Plugins.Sms.Twilio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PostgreSQL via PgBouncer
// Note: MARS is SQL Server only. For Npgsql we use pool sizing + No Reset On Close (compatible with PgBouncer transaction mode).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(3);
        npgsql.CommandTimeout(30);
    });
});

// RabbitMQ
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<INotificationQueue, RabbitMqNotificationQueue>();

// Core services
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<ITemplateEngine, InMemoryTemplateEngine>();
builder.Services.AddScoped<INotificationStatusStore, PostgresNotificationStatusStore>();
builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
builder.Services.AddScoped<NotificationOrchestrator>();

// Background worker
builder.Services.AddHostedService<NotificationBackgroundWorker>();

// Plugins
builder.Services.AddSingleton<IPlugin, SendGridEmailPlugin>();
builder.Services.AddSingleton<IPlugin, SmtpEmailPlugin>();
builder.Services.AddSingleton<IPlugin, TwilioSmsPlugin>();
builder.Services.AddSingleton<IPlugin, KavenegarSmsPlugin>();

var app = builder.Build();

// Migrate DB on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ApiKeyAuthMiddleware>();

// Bootstrap plugins
using (var scope = app.Services.CreateScope())
{
    var loader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var context = new SimplePluginContext(scope.ServiceProvider, config, loggerFactory.CreateLogger("PluginContext"));
    var plugins = scope.ServiceProvider.GetServices<IPlugin>();
    await loader.LoadFromAssembliesAsync(plugins.Select(p => p.GetType().Assembly).Distinct(), context);
}

app.MapPost("/api/v1/notifications", async (
    NotificationRequest request,
    NotificationOrchestrator orchestrator,
    INotificationQueue queue,
    IRateLimiter rateLimiter,
    IConfiguration config,
    CancellationToken ct) =>
{
    var tenantKey = request.TenantId ?? "default";
    var limit = config.GetValue("RateLimiting:PerMinute", 60);

    if (!await rateLimiter.IsAllowedAsync($"tenant:{tenantKey}:{request.Channel}", limit, ct))
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);

    var (accepted, status) = await orchestrator.AcceptAsync(request, ct);
    if (!accepted)
        return Results.Conflict(status);

    await queue.EnqueueAsync(request, ct);

    return Results.Accepted($"/api/v1/notifications/{status.NotificationId}", new
    {
        id = status.NotificationId,
        status = status.Status.ToString(),
        message = "Notification accepted and queued"
    });
})
.WithName("SendNotification")
.WithOpenApi();

app.MapPost("/api/v1/notifications/sync", async (
    NotificationRequest request,
    NotificationOrchestrator orchestrator,
    IRateLimiter rateLimiter,
    IConfiguration config,
    CancellationToken ct) =>
{
    var tenantKey = request.TenantId ?? "default";
    var limit = config.GetValue("RateLimiting:PerMinute", 60);

    if (!await rateLimiter.IsAllowedAsync($"tenant:{tenantKey}:{request.Channel}", limit, ct))
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);

    var (accepted, status) = await orchestrator.AcceptAsync(request, ct);
    if (!accepted)
        return Results.Conflict(status);

    var result = await orchestrator.ProcessAsync(request, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
.WithName("SendNotificationSync")
.WithOpenApi();

app.MapGet("/api/v1/notifications/{id:guid}", async (Guid id, INotificationStatusStore store, CancellationToken ct) =>
{
    var status = await store.GetAsync(id, ct);
    return status is null ? Results.NotFound() : Results.Ok(status);
})
.WithName("GetNotificationStatus")
.WithOpenApi();

app.MapGet("/api/v1/plugins", (PluginLoader loader) =>
{
    return loader.LoadedPlugins.Select(p => new
    {
        p.Id,
        p.Name,
        Version = p.Version.ToString(),
        Capabilities = p.Capabilities
    });
})
.WithName("ListPlugins")
.WithOpenApi();

app.MapPost("/api/v1/templates", async (TemplateDefinition template, ITemplateEngine engine, CancellationToken ct) =>
{
    await engine.RegisterTemplateAsync(template, ct);
    return Results.Created($"/api/v1/templates/{template.Key}", template);
})
.WithName("RegisterTemplate")
.WithOpenApi();

app.MapGet("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, ITemplateEngine engine, CancellationToken ct) =>
{
    var template = await engine.GetTemplateAsync(key, channel, locale ?? "en", tenantId, ct);
    return template is null ? Results.NotFound() : Results.Ok(template);
})
.WithName("GetTemplate")
.WithOpenApi();

app.MapGet("/health", async (NotificationDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new
    {
        status = canConnect ? "healthy" : "degraded",
        service = "NotificationHub",
        database = canConnect ? "up" : "down",
        timestamp = DateTimeOffset.UtcNow
    });
});

app.Run();

file sealed class SimplePluginContext : IPluginContext
{
    public SimplePluginContext(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        Services = services;
        Configuration = configuration;
        Logger = logger;
    }

    public IServiceProvider Services { get; }
    public IConfiguration Configuration { get; }
    public ILogger Logger { get; }
}
