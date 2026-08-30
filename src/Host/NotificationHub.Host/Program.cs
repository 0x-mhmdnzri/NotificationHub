using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Application.Abstractions;
using NotificationHub.Application.Features.Admin.MessagingHealth;
using NotificationHub.Application.Features.Campaigns.AddRecipients;
using NotificationHub.Application.Features.Campaigns.Cancel;
using NotificationHub.Application.Features.Campaigns.Create;
using NotificationHub.Application.Features.Campaigns.Get;
using NotificationHub.Application.Features.Campaigns.GetProgress;
using NotificationHub.Application.Features.Campaigns.ImportCsv;
using NotificationHub.Application.Features.Campaigns.Start;
using NotificationHub.Application.Features.Consents.Evaluate;
using NotificationHub.Application.Features.Consents.List;
using NotificationHub.Application.Features.Consents.Record;
using NotificationHub.Application.Features.Devices.List;
using NotificationHub.Application.Features.Devices.Register;
using NotificationHub.Application.Features.Devices.Unregister;
using NotificationHub.Application.Features.Engagement.Count;
using NotificationHub.Application.Features.Engagement.ListByNotification;
using NotificationHub.Application.Features.Engagement.Track;
using NotificationHub.Application.Features.Notifications.Accept;
using NotificationHub.Application.Features.Notifications.GetStatus;
using NotificationHub.Application.Features.Notifications.SendSync;
using NotificationHub.Application.Features.Preferences.Get;
using NotificationHub.Application.Features.Preferences.Save;
using NotificationHub.Application.Features.Segments.Get;
using NotificationHub.Application.Features.Segments.Match;
using NotificationHub.Application.Features.Segments.Save;
using NotificationHub.Application.Features.Templates.Delete;
using NotificationHub.Application.Features.Templates.GetByKey;
using NotificationHub.Application.Features.Templates.List;
using NotificationHub.Application.Features.Templates.Preview;
using NotificationHub.Application.Features.Templates.Save;
using NotificationHub.Application.Features.Topics.List;
using NotificationHub.Application.Features.Topics.ListSubscribers;
using NotificationHub.Application.Features.Topics.Save;
using NotificationHub.Application.Features.Topics.Subscribe;
using NotificationHub.Application.Features.Topics.Unsubscribe;
using NotificationHub.Application.Features.Webhooks.Create;
using NotificationHub.Application.Features.Workflows.Cancel;
using NotificationHub.Application.Features.Workflows.GetRun;
using NotificationHub.Application.Features.Workflows.GetTimeline;
using NotificationHub.Application.Features.Workflows.Save;
using NotificationHub.Application.Features.Workflows.Start;
using NotificationHub.Core.Activity;
using NotificationHub.Core.Analytics;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Auth;
using NotificationHub.Core.Campaigns;
using NotificationHub.Core.Cdp;
using NotificationHub.Core.Common;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.DependencyInjection;
using NotificationHub.Core.Devices;
using NotificationHub.Core.Digest;
using NotificationHub.Core.Engagement;
using NotificationHub.Core.Environments;
using NotificationHub.Core.Expressions;
using NotificationHub.Core.I18n;
using NotificationHub.Core.Inbox;
using NotificationHub.Core.Layouts;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Observability;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Queue;
using NotificationHub.Core.RateLimiting;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Scheduling;
using NotificationHub.Core.Security;
using NotificationHub.Core.Segmentation;
using NotificationHub.Core.Store;
using NotificationHub.Core.Sync;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Throttle;
using NotificationHub.Core.Topics;
using NotificationHub.Core.Validation;
using NotificationHub.Core.Webhooks;
using NotificationHub.Core.Workflow;
using NotificationHub.Core.Workflow.Handlers;
using NotificationHub.Host.Composition;
using NotificationHub.Host.Configuration;
using NotificationHub.Host.Hangfire;
using NotificationHub.Host.Http;
using NotificationHub.Host.Middleware;
using NotificationHub.Host.Performance;
using NotificationHub.Host.Security;
using NotificationHub.Infrastructure.DependencyInjection;
using NotificationHub.Infrastructure.HangfireJobs;
using NotificationHub.Infrastructure.Messaging.Integration;
using NotificationHub.Plugins.Chat.Discord;
using NotificationHub.Plugins.Chat.Slack;
using NotificationHub.Plugins.Chat.Teams;
using NotificationHub.Plugins.Chat.Telegram;
using NotificationHub.Plugins.Chat.WhatsApp;
using NotificationHub.Plugins.Email.Resend;
using NotificationHub.Plugins.Email.SendGrid;
using NotificationHub.Plugins.Email.Ses;
using NotificationHub.Plugins.Email.Smtp;
using NotificationHub.Plugins.InApp;
using NotificationHub.Plugins.Push.Expo;
using NotificationHub.Plugins.Push.Fcm;
using NotificationHub.Plugins.Sms.Kavenegar;
using NotificationHub.Plugins.Sms.SmsIr;
using NotificationHub.Plugins.Sms.Twilio;
using NotificationHub.ServiceDefaults;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Ensure appsettings*.json are loaded from the Host project (and output) directory.
// After solution restructure, wrong ContentRoot can miss ConnectionStrings in Development JSON.
var configDir = builder.Environment.ContentRootPath;
builder.Configuration
    .AddJsonFile(Path.Combine(configDir, "appsettings.json"), optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(configDir, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true);
var baseDir = AppContext.BaseDirectory;
if (!string.Equals(baseDir.TrimEnd(Path.DirectorySeparatorChar), configDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
{
    builder.Configuration
        .AddJsonFile(Path.Combine(baseDir, "appsettings.json"), optional: true, reloadOnChange: true)
        .AddJsonFile(Path.Combine(baseDir, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true);
}
// Environment variables always win over JSON (Aspire injects ConnectionStrings__notificationdb)
builder.Configuration.AddEnvironmentVariables();

builder.AddHighLoadRuntimeTuning();


// Serilog + OTEL (Jaeger via OTLP) + health checks + service discovery
builder.AddNotificationHubDefaults();

// SEC-26: limit request body size (DoS)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2 * 1024 * 1024); // 2 MB

builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
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

// ConnectionStrings from appsettings.{Environment}.json, Aspire (notificationdb), or env.
// Base appsettings.json may have empty Default — must not block Development / Aspire keys.
var cs = ConnectionStringResolver.ResolvePostgres(builder.Configuration);
if (string.IsNullOrWhiteSpace(cs))
{
    var envName = builder.Environment.EnvironmentName;
    throw new InvalidOperationException(
        $"PostgreSQL connection string is missing for environment '{envName}'. " +
        "Checked keys: ConnectionStrings:Default | notificationdb | postgres (appsettings + Aspire). " +
        "For local run set ASPNETCORE_ENVIRONMENT=Development (loads appsettings.Development.json) " +
        "or set ConnectionStrings__Default / DATABASE_URL. " +
        "ContentRoot: " + builder.Environment.ContentRootPath);
}

Console.WriteLine(
    $"[Startup] PostgreSQL from {ConnectionStringResolver.PostgresSourceKey(builder.Configuration)} " +
    $"(Environment={builder.Environment.EnvironmentName}, ContentRoot={builder.Environment.ContentRootPath})");

builder.Services.AddDbContextPool<NotificationDbContext>(opt =>
    opt.UseNpgsql(cs, n => { n.EnableRetryOnFailure(3); n.CommandTimeout(15); }));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<ProviderOptions>(builder.Configuration.GetSection("Providers"));
builder.Services.Configure<ProviderHealthOptions>(builder.Configuration.GetSection(ProviderHealthOptions.SectionName));
builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));

// --- Hangfire: durable outbox dispatch ---
builder.Services.Configure<HangfireMessagingOptions>(builder.Configuration.GetSection(HangfireMessagingOptions.SectionName));
var hangfireEnabled = builder.Configuration.GetValue("HangfireMessaging:Enabled", true);
var hangfireCs = cs; // same resolved Postgres connection
if (hangfireEnabled && !string.IsNullOrWhiteSpace(hangfireCs))
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireCs), new PostgreSqlStorageOptions
        {
            // Explicit schema so tables appear under "hangfire" in PostgreSQL (pgAdmin / \dn)
            SchemaName = "hangfire",
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(2),
            InvisibilityTimeout = TimeSpan.FromMinutes(30),
            JobExpirationCheckInterval = TimeSpan.FromHours(1),
            CountersAggregateInterval = TimeSpan.FromMinutes(5)
        }));
    var hfOpts = builder.Configuration.GetSection(HangfireMessagingOptions.SectionName).Get<HangfireMessagingOptions>()
                 ?? new HangfireMessagingOptions();
    var criticalWorkers = hfOpts.CriticalWorkerCount > 0
        ? hfOpts.CriticalWorkerCount
        : Math.Max(4, Environment.ProcessorCount);
    var standardWorkers = hfOpts.StandardWorkerCount > 0
        ? hfOpts.StandardWorkerCount
        : Math.Max(2, Environment.ProcessorCount);

    if (hfOpts.DedicatedCriticalServer)
    {
        builder.Services.AddHangfireServer(options =>
        {
            options.ServerName = $"critical-{Environment.MachineName}";
            options.Queues = new[] { MessagingQueues.Critical };
            options.WorkerCount = criticalWorkers;
        });
        builder.Services.AddHangfireServer(options =>
        {
            options.ServerName = $"standard-{Environment.MachineName}";
            options.Queues = new[] { MessagingQueues.Notifications, MessagingQueues.Outbox, MessagingQueues.Default };
            options.WorkerCount = standardWorkers;
        });
    }
    else
    {
        builder.Services.AddHangfireServer(options =>
        {
            options.ServerName = $"all-{Environment.MachineName}";
            options.Queues = new[] { MessagingQueues.Critical, MessagingQueues.Notifications, MessagingQueues.Outbox, MessagingQueues.Default };
            options.WorkerCount = Math.Max(criticalWorkers, standardWorkers);
        });
    }
    builder.Services.AddHangfireJobs();
    builder.Services.AddSingleton<IOutboxDispatchScheduler, HangfireOutboxDispatchScheduler>();
}
else
{
    builder.Services.AddSingleton<IOutboxDispatchScheduler, NullOutboxDispatchScheduler>();
}

// Aspire role flags (default true for monolithic Host)
var runOutbox = builder.Configuration.GetValue("Workers:RunOutboxRelay", true);
var runMessagingHealth = builder.Configuration.GetValue("Workers:RunMessagingHealthMonitor", true);
var runCampaign = builder.Configuration.GetValue("Workers:RunCampaignDispatch", true);
var runDigest = builder.Configuration.GetValue("Workers:RunDigest", true);
var runRetention = builder.Configuration.GetValue("Workers:RunRetention", true);
var runDelivery = builder.Configuration.GetValue("Workers:RunDeliveryConsumer", true);
var runScheduled = builder.Configuration.GetValue("Workers:RunScheduled", true);
var runWorkflow = builder.Configuration.GetValue("Workers:RunWorkflow", true);

if (runOutbox)
    builder.Services.Configure<OutboxRelayOptions>(builder.Configuration.GetSection(OutboxRelayOptions.SectionName));
if (builder.Configuration.GetValue("HangfireMessaging:KeepRelayWorker", true) ||
    !builder.Configuration.GetValue("HangfireMessaging:Enabled", true))
{
    builder.Services.AddHostedService<OutboxRelayWorker>();
}
builder.Services.Configure<MessagingHealthOptions>(builder.Configuration.GetSection(MessagingHealthOptions.SectionName));
if (runMessagingHealth)
    builder.Services.AddHostedService<MessagingHealthMonitorWorker>();

var redisCs = ConnectionStringResolver.ResolveRedis(builder.Configuration);

// --- Scrutor: Core platform (services, workflow, security, templates, providers) ---
builder.Services.AddCorePlatform();

// RabbitMQ + integration publisher (explicit conditional wiring)
builder.Services.AddSingleton<RabbitMqNotificationQueue>();
builder.Services.AddSingleton<IIntegrationEventPublisher>(sp =>
{
    var q = sp.GetService<RabbitMqNotificationQueue>();
    return q is null
        ? new NullIntegrationEventPublisher()
        : new RabbitMqIntegrationEventPublisher(q);
});
builder.Services.AddIntegrationMessaging();

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

builder.Services.Configure<CampaignDispatchOptions>(builder.Configuration.GetSection(CampaignDispatchOptions.SectionName));
if (runCampaign)
    builder.Services.AddHostedService<CampaignDispatchWorker>();
if (runDigest)
    builder.Services.AddHostedService<DigestFlushWorker>();
builder.Services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.SectionName));
builder.Services.AddHttpClient("workflow-http", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
if (runRetention)
    builder.Services.AddHostedService<RetentionBackgroundWorker>();

// CQRS Application + pipeline (MediatR owns handlers; Scrutor owns domain ports)
builder.Services.AddInfrastructureCqrs();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

if (runDelivery)
    builder.Services.AddHostedService<NotificationBackgroundWorker>();
if (runScheduled)
    builder.Services.AddHostedService<ScheduledNotificationWorker>();
if (runWorkflow)
    builder.Services.AddHostedService<WorkflowBackgroundWorker>();

// Channel plugins via Scrutor (IPlugin, Singleton, multi-registration)
builder.Services.AddChannelPlugins();

var app = builder.Build();

if (hangfireEnabled && !string.IsNullOrWhiteSpace(hangfireCs))
{
    app.UseMiddleware<HangfireDashboardRateLimitMiddleware>();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        // API key auth (same keys as REST). Header X-Api-Key or ?api_key= — requires Admin by default.
        Authorization = [new HangfireApiKeyAuthorizationFilter()],
        DashboardTitle = "NotificationHub Jobs"
    });
    var reconMinutes = app.Configuration.GetValue("HangfireMessaging:ReconciliationIntervalMinutes", 2);
    RecurringJob.AddOrUpdate<OutboxReconciliationJob>(
        "outbox-reconciliation",
        j => j.ReconcileAsync(CancellationToken.None),
        "*/" + Math.Clamp(reconMinutes, 1, 60) + " * * * *");
}

app.UseSerilogRequestLogging();

// Schema bootstrap — skip only when explicitly disabled (tests / external migrator)
var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", true);
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    var startupLog = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Startup");
    try
    {
        // Fail fast with a clear message if Postgres is unreachable / CS invalid
        if (!await db.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                "Cannot connect to PostgreSQL with the configured ConnectionStrings:Default. " +
                "Verify the server is running and credentials are correct.");
        }

        await db.Database.MigrateAsync();

        // Create Hangfire.PostgreSql tables under schema "hangfire" (job, jobqueue, state, server, ...)
        if (hangfireEnabled && !string.IsNullOrWhiteSpace(hangfireCs))
        {
            try
            {
                await using var hfConn = new Npgsql.NpgsqlConnection(hangfireCs);
                await hfConn.OpenAsync();
                Hangfire.PostgreSql.PostgreSqlObjectsInstaller.Install(hfConn, "hangfire");
                startupLog?.LogInformation("Hangfire PostgreSQL schema/tables ensured (schema=hangfire)");
            }
            catch (Exception ex)
            {
                startupLog?.LogError(ex, "Hangfire schema install failed — jobs may not persist until fixed");
                throw;
            }
        }

        await Phase1Schema.EnsureAsync(db, startupLog);
        await Phase2Schema.EnsureAsync(db, startupLog);
        await BroadcastSchema.EnsureAsync(db, startupLog);
        await Phase4Schema.EnsureAsync(db, startupLog);
        var seeder = scope.ServiceProvider.GetRequiredService<TemplateSeeder>();
        await seeder.SeedDefaultsAsync();
        var keyBootstrap = scope.ServiceProvider.GetRequiredService<ApiKeyBootstrapper>();
        await keyBootstrap.EnsureBootstrapKeyAsync();
    }
    catch (Exception ex) when (ex is not InvalidOperationException)
    {
        startupLog?.LogCritical(ex,
            "Database startup failed. ConnectionStrings:Default must point to a reachable PostgreSQL instance.");
        throw;
    }
}
else
{
    app.Logger.LogWarning("Database:AutoMigrate is false — skipping MigrateAsync and seeders.");
}

// ---------------------------------------------------------------------------
// Middleware order (ASP.NET Core best practice / "Hidden Bug in Program.cs"):
// 1. ForwardedHeaders   — correct scheme/IP behind proxy
// 2. ExceptionHandler   — catch failures early (outermost)
// 3. CorrelationId      — available for error logs
// 4. HSTS / HTTPS       — transport security (non-Development)
// 5. Security headers
// 6. Response compression
// 7. Static/Swagger (dev)
// 8. Routing
// 9. CORS               — before Auth so preflight OPTIONS succeeds
// 10. Admin IP allowlist
// 11. Authentication    — ApiKeyAuthMiddleware (custom AuthN)
// 12. Authorization     — enforced in endpoints + MediatR [AuthorizeRoles]
// 13. Map endpoints
// ---------------------------------------------------------------------------
app.UseForwardedHeaders();

// Exception handler near the top so downstream middleware/endpoint failures are captured
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// CORS must run before authentication so browser preflight is not blocked by 401
if (corsOrigins.Length > 0)
    app.UseCors("AppCors");

app.UseMiddleware<AdminIpAllowlistMiddleware>();

// AuthN (API key) — equivalent to UseAuthentication for this API
app.UseMiddleware<ApiKeyAuthMiddleware>();
// AuthZ: RequireRoles on endpoints + AuthorizationBehavior on MediatR commands

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
    NotificationRequest request, HttpContext http, ISender sender, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tenantId = http.ResolveTenantId(request.TenantId);
    var limit = config.GetValue("RateLimiting:PerMinute", 120);
    if (!await rl.IsAllowedAsync($"tenant:{tenantId ?? "default"}:{request.Channel ?? "any"}", limit, ct))
        return Results.StatusCode(429);

    try
    {
        var result = await sender.Send(new AcceptNotificationCommand(request, tenantId), ct);
        return result.ToHttpResult(r =>
            r.Status == nameof(DeliveryStatus.Suppressed) || r.Status == "Suppressed"
                ? Results.Ok(new { id = r.NotificationId, status = r.Status, reason = r.Reason })
                : Results.Accepted($"/api/v1/notifications/{r.NotificationId}", new { id = r.NotificationId, status = r.Status }));
    }
    catch (ValidationException vex)
    {
        return Results.BadRequest(new { error = "validation_failed", details = vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }) });
    }
}).WithName("SendNotification");

app.MapPost("/api/v1/notifications/sync", async (NotificationRequest request, HttpContext http, ISender sender, IRateLimiter rl, IConfiguration config, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tenantId = http.ResolveTenantId(request.TenantId);
    var limit = config.GetValue("RateLimiting:PerMinute", 60);
    if (!await rl.IsAllowedAsync($"tenant:{tenantId ?? "default"}:sync", limit, ct))
        return Results.StatusCode(429);
    var result = await sender.Send(new SendNotificationSyncCommand(request, tenantId), ct);
    return result.ToHttpResult();
}).WithName("SendNotificationSync");

app.MapGet("/api/v1/notifications/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var auth = http.GetAuthContext();
    var result = await sender.Send(new GetNotificationStatusQuery(id, auth?.TenantId, auth?.IsAdmin ?? false), ct);
    return result.ToHttpResult();
}).WithName("GetNotificationStatus");

app.MapGet("/api/v1/plugins", (HttpContext http, PluginLoader loader) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied)
        return denied;
    return Results.Ok(loader.LoadedPlugins.Select(p => new { p.Id, p.Name, Version = p.Version.ToString(), Capabilities = p.Capabilities }));
}).WithName("ListPlugins");

app.MapPost("/api/v1/templates", async (TemplateDefinition t, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    t = t with { TenantId = http.ResolveTenantId(t.TenantId) };
    var result = await sender.Send(new SaveTemplateCommand(t), ct);
    return result.ToHttpResult(saved => Results.Created($"/api/v1/templates/{saved.Key}", saved));
}).WithName("SaveTemplate");

app.MapGet("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new GetTemplateQuery(key, channel, locale ?? "en", tid), ct);
    return result.ToHttpResult();
}).WithName("GetTemplate");

app.MapGet("/api/v1/templates", async (string? tenantId, string? channel, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new ListTemplatesQuery(tid, channel), ct);
    return result.ToHttpResult();
}).WithName("ListTemplates");

app.MapDelete("/api/v1/templates/{key}", async (string key, string channel, string? locale, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new DeleteTemplateCommand(key, channel, locale ?? "en", tid), ct);
    return result.ToHttpResult();
}).WithName("DeleteTemplate");

app.MapPost("/api/v1/templates/preview", async (NotificationRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(request.TenantId);
    var result = await sender.Send(new PreviewTemplateQuery(request, tid), ct);
    return result.ToHttpResult();
}).WithName("PreviewTemplate");

app.MapGet("/api/v1/preferences/{userId}", async (string userId, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new GetPreferencesQuery(userId, tid), ct);
    return result.ToHttpResult();
}).WithName("GetPreferences");

app.MapPut("/api/v1/preferences", async (UserPreference pref, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    pref = pref with { TenantId = http.ResolveTenantId(pref.TenantId) };
    var result = await sender.Send(new SavePreferencesCommand(pref), ct);
    return result.ToHttpResult();
}).WithName("SavePreferences");

app.MapPost("/api/v1/webhooks", async (WebhookSubscription sub, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied)
        return denied;
    var tenantId = http.ResolveTenantId(sub.TenantId);
    var result = await sender.Send(new CreateWebhookCommand(sub, tenantId), ct);
    return result.ToHttpResult(w => Results.Created($"/api/v1/webhooks/{w.Id}", w));
}).WithName("CreateWebhook");

// --- Consents ---
app.MapPost("/api/v1/consents", async (ConsentRecord record, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(record.TenantId);
    var result = await sender.Send(new RecordConsentCommand(record, tid), ct);
    return result.ToHttpResult(r => Results.Created($"/api/v1/consents/{r.SubjectId}", r));
}).WithName("RecordConsent");

app.MapGet("/api/v1/consents/{subjectId}", async (string subjectId, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new ListConsentsQuery(subjectId, tid), ct);
    return result.ToHttpResult();
}).WithName("ListConsents");

app.MapPost("/api/v1/consents/evaluate", async (string subjectId, string purpose, string? channel, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new EvaluateConsentQuery(subjectId, purpose, channel, tid), ct);
    return result.ToHttpResult();
}).WithName("EvaluateConsent");

// --- Workflows ---
app.MapPost("/api/v1/workflows", async (WorkflowDefinition def, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(def.TenantId);
    var result = await sender.Send(new SaveWorkflowCommand(def, tid), ct);
    return result.ToHttpResult(d => Results.Created($"/api/v1/workflows/{d.Key}", d));
}).WithName("SaveWorkflow");

app.MapPost("/api/v1/workflows/start", async (WorkflowStartRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(request.TenantId);
    var result = await sender.Send(new StartWorkflowCommand(request, tid), ct);
    return result.ToHttpResult(id => Results.Accepted($"/api/v1/workflows/runs/{id}", new { runId = id }));
}).WithName("StartWorkflow");

app.MapGet("/api/v1/workflows/runs/{runId:guid}", async (Guid runId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var result = await sender.Send(new GetWorkflowRunQuery(runId), ct);
    return result.ToHttpResult();
}).WithName("GetWorkflowRun");

app.MapGet("/api/v1/workflows/runs/{runId:guid}/timeline", async (Guid runId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var result = await sender.Send(new GetWorkflowTimelineQuery(runId), ct);
    return result.ToHttpResult();
}).WithName("GetWorkflowTimeline");

app.MapPost("/api/v1/workflows/runs/{runId:guid}/cancel", async (Guid runId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied)
        return denied;
    var result = await sender.Send(new CancelWorkflowCommand(runId), ct);
    return result.ToHttpResult();
}).WithName("CancelWorkflow");

// --- Admin ---
app.MapGet("/api/v1/admin/messaging/health", async (HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied)
        return denied;
    var result = await sender.Send(new GetMessagingHealthQuery(), ct);
    return result.ToHttpResult();
}).WithName("GetMessagingHealth");

// --- Segments ---
app.MapPost("/api/v1/segments", async (SegmentDefinition segment, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(segment.TenantId);
    var result = await sender.Send(new SaveSegmentCommand(segment, tid), ct);
    return result.ToHttpResult(s => Results.Created($"/api/v1/segments/{s.Key}", s));
}).WithName("SaveSegment");

app.MapGet("/api/v1/segments/{key}", async (string key, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new GetSegmentQuery(key, tid), ct);
    return result.ToHttpResult();
}).WithName("GetSegment");

app.MapPost("/api/v1/segments/{key}/match", async (string key, Dictionary<string, object?> attributes, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new MatchSegmentQuery(key, attributes, tid), ct);
    return result.ToHttpResult(m => Results.Ok(new { match = m }));
}).WithName("MatchSegment");

// --- Engagement ---
app.MapPost("/api/v1/engagement", async (EngagementEvent evt, HttpContext http, ISender sender, CancellationToken ct) =>
{
    // open/click tracking may be unauthenticated pixel; allow Sender+Admin; public track via signed links later
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied)
        return denied;
    var result = await sender.Send(new TrackEngagementCommand(evt), ct);
    return result.ToHttpResult(e => Results.Accepted($"/api/v1/notifications/{e.NotificationId}/engagement", e));
}).WithName("TrackEngagement");

app.MapGet("/api/v1/notifications/{id:guid}/engagement", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var result = await sender.Send(new ListEngagementQuery(id), ct);
    return result.ToHttpResult();
}).WithName("ListEngagement");

app.MapGet("/api/v1/engagement/stats", async (DateTimeOffset? from, DateTimeOffset? to, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new CountEngagementQuery(from, to, tid), ct);
    return result.ToHttpResult();
}).WithName("EngagementStats");

// --- Devices ---
app.MapPost("/api/v1/devices", async (RegisterDeviceRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(request.TenantId);
    var result = await sender.Send(new RegisterDeviceCommand(request, tid), ct);
    return result.ToHttpResult(d => Results.Created($"/api/v1/devices/{d.UserId}", d));
}).WithName("RegisterDevice");

app.MapDelete("/api/v1/devices", async (string userId, string token, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new UnregisterDeviceCommand(userId, token, tid), ct);
    return result.ToHttpResult();
}).WithName("UnregisterDevice");

app.MapGet("/api/v1/devices/{userId}", async (string userId, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new ListDevicesQuery(userId, tid), ct);
    return result.ToHttpResult();
}).WithName("ListDevices");

// --- Topics ---
app.MapPost("/api/v1/topics", async (TopicDefinition topic, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(topic.TenantId);
    var result = await sender.Send(new SaveTopicCommand(topic, tid), ct);
    return result.ToHttpResult(t => Results.Created($"/api/v1/topics/{t.Key}", t));
}).WithName("SaveTopic");

app.MapGet("/api/v1/topics", async (string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new ListTopicsQuery(tid), ct);
    return result.ToHttpResult();
}).WithName("ListTopics");

app.MapPost("/api/v1/topics/{key}/subscribe", async (string key, string subscriberId, string? channel, string? address, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new SubscribeTopicCommand(key, subscriberId, tid, channel, address), ct);
    return result.ToHttpResult();
}).WithName("SubscribeTopic");

app.MapPost("/api/v1/topics/{key}/unsubscribe", async (string key, string subscriberId, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new UnsubscribeTopicCommand(key, subscriberId, tid), ct);
    return result.ToHttpResult();
}).WithName("UnsubscribeTopic");

app.MapGet("/api/v1/topics/{key}/subscribers", async (string key, string? tenantId, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Reader) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(tenantId);
    var result = await sender.Send(new ListTopicSubscribersQuery(key, tid), ct);
    return result.ToHttpResult();
}).WithName("ListTopicSubscribers");

// --- Campaigns / Batch Broadcast ---
app.MapPost("/api/v1/campaigns", async (CreateCampaignRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(body.TenantId);
    var auth = http.GetAuthContext();
    var result = await sender.Send(new CreateCampaignCommand(body, tid, auth?.KeyName), ct);
    return result.ToHttpResult(c => Results.Created($"/api/v1/campaigns/{c.Id}", c));
}).WithName("CreateCampaign");

app.MapPost("/api/v1/campaigns/{id:guid}/recipients", async (Guid id, AddRecipientsRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(null);
    var result = await sender.Send(new AddRecipientsCommand(id, body, tid), ct);
    return result.ToHttpResult(n => Results.Ok(new { added = n }));
}).WithName("AddCampaignRecipients");

app.MapPost("/api/v1/campaigns/{id:guid}/recipients/import", async (Guid id, HttpRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    if (!request.HasFormContentType || request.Form.Files.Count == 0)
        return Results.BadRequest(new { error = "multipart form with file required" });
    var file = request.Form.Files[0];
    if (file.Length > 20 * 1024 * 1024)
        return Results.BadRequest(new { error = "file too large (max 20MB)" });
    await using var stream = file.OpenReadStream();
    var tid = http.ResolveTenantId(null);
    var result = await sender.Send(new ImportCsvCommand(id, stream, tid), ct);
    return result.ToHttpResult(n => Results.Ok(new { imported = n }));
}).WithName("ImportCampaignRecipientsCsv").DisableAntiforgery();

app.MapPost("/api/v1/campaigns/{id:guid}/send", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(null);
    var result = await sender.Send(new StartCampaignCommand(id, tid), ct);
    return result.ToHttpResult();
}).WithName("StartCampaign");

app.MapPost("/api/v1/campaigns/{id:guid}/cancel", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(null);
    var result = await sender.Send(new CancelCampaignCommand(id, tid), ct);
    return result.ToHttpResult();
}).WithName("CancelCampaign");

app.MapGet("/api/v1/campaigns/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(null);
    var result = await sender.Send(new GetCampaignQuery(id, tid), ct);
    return result.ToHttpResult();
}).WithName("GetCampaign");

app.MapGet("/api/v1/campaigns/{id:guid}/progress", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) is { } denied)
        return denied;
    var tid = http.ResolveTenantId(null);
    var result = await sender.Send(new GetCampaignProgressQuery(id, tid), ct);
    return result.ToHttpResult();
}).WithName("GetCampaignProgress");

// Compatibility: one-shot broadcast
app.MapPost("/api/v1/broadcasts", async (BroadcastRequest body, HttpContext http, IBroadcastService broadcast, CancellationToken ct) =>
{
    if (http.RequireRoles(AppRoles.Admin, AppRoles.Sender) is { } denied)
        return denied;
    body = body with { TenantId = http.ResolveTenantId(body.TenantId) };
    var result = await broadcast.SendAsync(body, ct);
    return Results.Accepted($"/api/v1/campaigns/{result.CampaignId}", result);
}).WithName("SendBroadcast");

app.MapNotificationHubHealthEndpoints();

// --- Runtime version (SemVer + commit) — no secrets ---
app.MapGet("/api/v1/version", () =>
{
    var asm = typeof(Program).Assembly;
    var info = asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
        .FirstOrDefault()?.InformationalVersion
        ?? asm.GetName().Version?.ToString()
        ?? "0.0.0";
    var version = info.Split('+')[0];
    var commit = info.Contains('+') ? info.Split('+', 2)[1] : (Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local");
    return Results.Ok(new
    {
        version,
        commit = commit.Length > 12 ? commit[..12] : commit,
        product = "NotificationHub",
        environment = app.Environment.EnvironmentName
    });
}).AllowAnonymous().WithName("GetVersion");

app.Run();
