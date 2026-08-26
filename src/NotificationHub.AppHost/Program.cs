var builder = DistributedApplication.CreateBuilder(args);

// Layer 1: Aspire application composition (NOT business workflow orchestration)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var notificationDb = postgres.AddDatabase("notificationdb");

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithDataVolume();

var redis = builder.AddRedis("redis")
    .WithDataVolume();

// Jaeger all-in-one (OTLP + UI) — dashboard: http://localhost:16686
var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one", "1.64")
    .WithEndpoint(port: 16686, targetPort: 16686, name: "ui", scheme: "http")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "otlp-grpc", scheme: "http")
    .WithEndpoint(port: 4318, targetPort: 4318, name: "otlp-http", scheme: "http")
    .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true");

var otlp = jaeger.GetEndpoint("otlp-grpc");

// API: accept + outbox relay + orchestration workers (no delivery consume)
var api = builder.AddProject<Projects.NotificationHub_Host>("notification-api")
    .WithReference(notificationDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(notificationDb)
    .WaitFor(rabbit)
    .WaitFor(redis)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlp)
    .WithEnvironment("OpenTelemetry__OtlpEndpoint", otlp)
    .WithEnvironment("Serilog__UseJsonConsole", "false")
    .WithEnvironment("RabbitMQ__ChannelRouting", "true")
    .WithEnvironment("Workers__RunDeliveryConsumer", "false")
    .WithEnvironment("Workers__RunOutboxRelay", "true")
    .WithHttpHealthCheck("/health/ready");

// Channel delivery workers — each process consumes only its queue (email/sms/push)
foreach (var channel in new[] { "email", "sms", "push" })
{
    builder.AddProject<Projects.NotificationHub_Host>($"worker-{channel}")
        .WithReference(notificationDb)
        .WithReference(rabbit)
        .WithReference(redis)
        .WaitFor(notificationDb)
        .WaitFor(rabbit)
        .WaitFor(redis)
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlp)
        .WithEnvironment("OpenTelemetry__OtlpEndpoint", otlp)
        .WithEnvironment("Serilog__UseJsonConsole", "false")
        .WithEnvironment("RabbitMQ__ChannelRouting", "true")
        .WithEnvironment("RabbitMQ__ConsumeChannel", channel)
        .WithEnvironment("Workers__RunDeliveryConsumer", "true")
        .WithEnvironment("Workers__RunOutboxRelay", "false")
        .WithEnvironment("Workers__RunCampaignDispatch", "false")
        .WithEnvironment("Workers__RunScheduled", "false")
        .WithEnvironment("Workers__RunWorkflow", "false")
        .WithEnvironment("Workers__RunDigest", "false")
        .WithEnvironment("Workers__RunRetention", "false")
        .WithEnvironment("Workers__RunMessagingHealthMonitor", "false")
        .WithHttpHealthCheck("/health/ready");
}

builder.Build().Run();
