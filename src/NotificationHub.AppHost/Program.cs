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

// Jaeger all-in-one (OTLP + UI) for local tracing — dashboard: http://localhost:16686
var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one", "1.64")
    .WithEndpoint(port: 16686, targetPort: 16686, name: "ui", scheme: "http")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "otlp-grpc", scheme: "http")
    .WithEndpoint(port: 4318, targetPort: 4318, name: "otlp-http", scheme: "http")
    .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true");

static void WireShared(
    IResourceBuilder<ProjectResource> project,
    IResourceBuilder<IResourceWithConnectionString> db,
    IResourceBuilder<RabbitMQServerResource> rabbitMq,
    IResourceBuilder<IResourceWithConnectionString> redisCache,
    IResourceBuilder<ContainerResource> jaegerContainer)
{
    project
        .WithReference(db)
        .WithReference(rabbitMq)
        .WithReference(redisCache)
        .WaitFor(db)
        .WaitFor(rabbitMq)
        .WaitFor(redisCache)
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", jaegerContainer.GetEndpoint("otlp-grpc"))
        .WithEnvironment("OpenTelemetry__OtlpEndpoint", jaegerContainer.GetEndpoint("otlp-grpc"))
        .WithEnvironment("Serilog__UseJsonConsole", "false")
        .WithEnvironment("RabbitMQ__ChannelRouting", "true");
}

// API: accepts requests, outbox relay, campaigns — does NOT consume delivery queues when channel workers are present
var api = builder.AddProject<Projects.NotificationHub_Host>("notification-api")
    .WithEnvironment("Workers__RunDeliveryConsumer", "false")
    .WithEnvironment("Workers__RunOutboxRelay", "true")
    .WithHttpHealthCheck("/health/ready");
WireShared(api, notificationDb, rabbit, redis, jaeger);

// Channel delivery workers (choreography): each consumes only its RabbitMQ queue
void AddChannelWorker(string name, string channel)
{
    var w = builder.AddProject<Projects.NotificationHub_Host>(name)
        .WithEnvironment("Workers__RunDeliveryConsumer", "true")
        .WithEnvironment("Workers__RunOutboxRelay", "false")
        .WithEnvironment("Workers__RunCampaignDispatch", "false")
        .WithEnvironment("Workers__RunScheduled", "false")
        .WithEnvironment("Workers__RunWorkflow", "false")
        .WithEnvironment("Workers__RunDigest", "false")
        .WithEnvironment("Workers__RunRetention", "false")
        .WithEnvironment("Workers__RunMessagingHealthMonitor", "false")
        .WithEnvironment("RabbitMQ__ConsumeChannel", channel)
        .WithEnvironment("RabbitMQ__ChannelRouting", "true")
        .WithHttpHealthCheck("/health/ready");
    WireShared(w, notificationDb, rabbit, redis, jaeger);
}

AddChannelWorker("worker-email", "email");
AddChannelWorker("worker-sms", "sms");
AddChannelWorker("worker-push", "push");

builder.Build().Run();
