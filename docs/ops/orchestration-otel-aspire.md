# Orchestration, OpenTelemetry, Aspire

Runbook aligned with **ADR-012** (composition vs domain) and **ADR-013** (channel workers).

## Separation of concerns

| Layer | Owner | Responsibility |
|-------|--------|----------------|
| Aspire AppHost | `NotificationHub.AppHost` | Compose API + channel workers + Postgres + RabbitMQ + Redis + Jaeger |
| Business orchestration | `BroadcastStateMachine` + `CampaignService` | Broadcast lifecycle transitions |
| Delivery | Channel plugins + `worker-*` processes | Independent channel execution |
| Observability | `NotificationHub.ServiceDefaults` | Serilog, OTEL→Jaeger, health checks |

Aspire does **not** implement campaign state rules. Workers do **not** orchestrate other channels.

## Aspire topology

```bash
dotnet run --project src/NotificationHub.AppHost
```

| Resource | Role |
|----------|------|
| `notification-api` | HTTP + outbox relay; `Workers__RunDeliveryConsumer=false` |
| `worker-email` | Consumes `notifications.email` |
| `worker-sms` | Consumes `notifications.sms` |
| `worker-push` | Consumes `notifications.push` |
| Postgres / RabbitMQ / Redis | Shared dependencies |
| Jaeger | OTLP collector + UI |

Shared env (illustrative):

- `RabbitMQ__ChannelRouting=true`
- `OTEL_EXPORTER_OTLP_ENDPOINT` → Jaeger OTLP gRPC
- `Serilog__UseJsonConsole=false` in Aspire dashboard (human-readable); set `true` for ELK pipelines

## Broadcast state machine

```
Draft → Scheduled → Preparing → Dispatching → Delivering
                                              ├→ Completed
                                              ├→ PartiallyCompleted
                                              └→ Failed
Any non-terminal → Cancelled (where allowed)
Failed → Preparing (operational recovery)
```

- Code: `NotificationHub.Core.Orchestration.BroadcastStateMachine`
- OTEL: `ActivitySource` name `NotificationHub.Broadcast` on `Transition`
- Recipient rows use separate delivery statuses (not campaign lifecycle enums)

## Health

| Path | Meaning |
|------|---------|
| `/health`, `/health/live` | Process liveness (`self`) |
| `/health/ready` | `self` + postgres + redis + rabbitmq (when configured) |

Aspire projects use `WithHttpHealthCheck("/health/ready")`.

## Serilog

Enrichers: FromLogContext, Application, Environment, MachineName, ThreadId, EnvironmentName.  
Correlation id middleware pushes scope for request-scoped properties.

| Mode | When |
|------|------|
| Text template | Development / Aspire default |
| JSON console | `Serilog:UseJsonConsole=true`, `Serilog:ConsoleFormatter=json`, or Production |
| OTLP sink | When OTEL endpoint configured |

## Without Aspire

```bash
dotnet run --project src/NotificationHub.Host
# or
docker compose up -d --build
```

Monolith defaults: all `Workers:*` roles **on**, so API and delivery share one process. Set `RabbitMQ:ChannelRouting` / `ConsumeChannel` only if you intentionally split consumers outside AppHost.

## Related ADRs

- [ADR-012](../ADR-012-Aspire-Composition-vs-Business-Orchestration.md)
- [ADR-013](../ADR-013-Per-Channel-Delivery-Workers.md)
- [ADR-003](../ADR-003-RabbitMQ-Queue.md)
