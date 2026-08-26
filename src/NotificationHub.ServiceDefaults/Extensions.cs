using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.OpenTelemetry;

namespace NotificationHub.ServiceDefaults;

/// <summary>
/// Cross-cutting defaults: Serilog (console + ELK-friendly JSON + OTLP), OpenTelemetry (Jaeger via OTLP),
/// health checks for Postgres / Redis / RabbitMQ, service discovery.
/// Aspire AppHost composes apps; this is NOT a business orchestrator.
/// </summary>
public static class Extensions
{
    public const string ReadyTag = "ready";
    public const string LiveTag = "live";

    public static IHostApplicationBuilder AddNotificationHubDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureSerilog();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        return builder;
    }

    public static IHostApplicationBuilder ConfigureSerilog(this IHostApplicationBuilder builder)
    {
        var useJson = builder.Configuration.GetValue("Serilog:UseJsonConsole", false)
                      || string.Equals(builder.Configuration["Serilog:ConsoleFormatter"], "json", StringComparison.OrdinalIgnoreCase)
                      || builder.Environment.IsProduction();

        var cfg = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .ReadFrom.Configuration(builder.Configuration);

        if (useJson)
        {
            // Structured JSON for Filebeat / Fluent Bit / ELK
            cfg = cfg.WriteTo.Console(new JsonFormatter(renderMessage: true));
        }
        else
        {
            cfg = cfg.WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        cfg = cfg.WriteTo.Conditional(
            _ => !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])
                 || !string.IsNullOrWhiteSpace(builder.Configuration["OpenTelemetry:OtlpEndpoint"]),
            wt => wt.OpenTelemetry(options =>
            {
                options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                    ?? builder.Configuration["OpenTelemetry:OtlpEndpoint"]
                    ?? "http://localhost:4317";
                options.Protocol = OtlpProtocol.Grpc;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = builder.Environment.ApplicationName,
                    ["deployment.environment"] = builder.Environment.EnvironmentName
                };
            }));

        Log.Logger = cfg.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: true);
        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        var otlp = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? builder.Configuration["OpenTelemetry:OtlpEndpoint"];

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: typeof(Extensions).Assembly.GetName().Version?.ToString() ?? "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName
                }))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("NotificationHub");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                        o.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSource("NotificationHub")
                    .AddSource("NotificationHub.Broadcast");
            });

        if (!string.IsNullOrWhiteSpace(otlp))
        {
            builder.Services.ConfigureOpenTelemetryMeterProvider(m => m.AddOtlpExporter());
            builder.Services.ConfigureOpenTelemetryTracerProvider(t => t.AddOtlpExporter());
        }

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        var checks = builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag, ReadyTag]);

        var pg = builder.Configuration.GetConnectionString("Default")
            ?? builder.Configuration.GetConnectionString("notificationdb")
            ?? builder.Configuration.GetConnectionString("postgres");
        if (!string.IsNullOrWhiteSpace(pg))
        {
            checks.AddNpgSql(pg, name: "postgres", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(3));
        }

        var redis = builder.Configuration.GetConnectionString("Redis")
            ?? builder.Configuration.GetConnectionString("redis")
            ?? builder.Configuration["ConnectionStrings:Redis"];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            checks.AddRedis(redis, name: "redis", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(3));
        }

        var rmqCs = builder.Configuration.GetConnectionString("rabbitmq")
            ?? builder.Configuration.GetConnectionString("RabbitMQ");
        var rmqHost = builder.Configuration["RabbitMQ:HostName"];
        var rmqUser = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
        var rmqPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
        var rmqPort = builder.Configuration["RabbitMQ:Port"] ?? "5672";
        var rmqVhost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

        if (!string.IsNullOrWhiteSpace(rmqCs))
        {
            checks.AddRabbitMQ(rmqCs, name: "rabbitmq", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(5));
        }
        else if (!string.IsNullOrWhiteSpace(rmqHost))
        {
            checks.AddRabbitMQ(
                sp =>
                {
                    var factory = new RabbitMQ.Client.ConnectionFactory
                    {
                        HostName = rmqHost,
                        Port = int.TryParse(rmqPort, out var port) ? port : 5672,
                        UserName = rmqUser,
                        Password = rmqPass,
                        VirtualHost = string.IsNullOrWhiteSpace(rmqVhost) ? "/" : rmqVhost,
                        RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
                    };
                    return factory.CreateConnectionAsync("notificationhub-health").GetAwaiter().GetResult();
                },
                name: "rabbitmq",
                tags: [ReadyTag],
                timeout: TimeSpan.FromSeconds(5));
        }

        return builder;
    }

    public static WebApplication MapNotificationHubHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(LiveTag)
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(ReadyTag),
            ResponseWriter = WriteHealthJson
        }).AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(LiveTag)
        }).AllowAnonymous();

        return app;
    }

    private static async Task WriteHealthJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    error = e.Value.Exception?.Message
                })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
