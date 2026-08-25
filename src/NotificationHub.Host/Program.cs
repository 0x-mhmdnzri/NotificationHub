using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.PluginHost;
using NotificationHub.Plugins.Email.SendGrid;
using NotificationHub.Plugins.Sms.Twilio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddSingleton<NotificationOrchestrator>();

builder.Services.AddSingleton<IPlugin, SendGridEmailPlugin>();
builder.Services.AddSingleton<IPlugin, TwilioSmsPlugin>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var loader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var context = new SimplePluginContext(scope.ServiceProvider, config, loggerFactory.CreateLogger("PluginContext"));

    var plugins = scope.ServiceProvider.GetServices<IPlugin>();
    await loader.LoadFromAssembliesAsync(plugins.Select(p => p.GetType().Assembly).Distinct(), context);
}

app.MapPost("/api/v1/notifications", async (NotificationRequest request, NotificationOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.SendAsync(request, ct);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
.WithName("SendNotification")
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

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "NotificationHub" }));

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
