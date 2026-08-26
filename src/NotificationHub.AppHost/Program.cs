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

builder.AddProject<Projects.NotificationHub_Host>("notification-api")
    .WithReference(notificationDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(notificationDb)
    .WaitFor(rabbit)
    .WaitFor(redis)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", jaeger.GetEndpoint("otlp-grpc"))
    .WithEnvironment("OpenTelemetry__OtlpEndpoint", jaeger.GetEndpoint("otlp-grpc"))
    .WithEnvironment("Serilog__UseJsonConsole", "false") // human-readable in Aspire dashboard; set true for ELK
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();
