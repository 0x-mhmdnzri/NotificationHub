# Orchestration, OpenTelemetry, Aspire

## Separation of concerns

| Layer | Owner | Responsibility |
|-------|--------|----------------|
| Aspire AppHost | `NotificationHub.AppHost` | Compose API + Postgres + RabbitMQ + Redis + Jaeger |
| Business orchestration | `BroadcastStateMachine` + `CampaignService` | Broadcast lifecycle transitions |
| Delivery | Channel plugins + workers | Independent channel execution |
| Observability | `NotificationHub.ServiceDefaults` | Serilog, OTEL→Jaeger, health checks |

## Broadcast state machine

```
Draft → Scheduled → Preparing → Dispatching → Delivering
                                              ├→ Completed
                                              ├→ PartiallyCompleted
                                              └→ Failed
Any active → Cancelled
```

Delivery rows use `BroadcastRecipientStatus` (separate from campaign lifecycle).

## Local Aspire

```bash
dotnet run --project src/NotificationHub.AppHost
```

- Aspire dashboard: service graph + resource health
- Jaeger UI: typically `http://localhost:16686`
- API health: `/health` (live), `/health/ready` (postgres/redis/rabbitmq)

## Serilog

Enriched with Application, Environment, MachineName, ThreadId.  
Console sink always on; OTLP sink when `OTEL_EXPORTER_OTLP_ENDPOINT` or `OpenTelemetry:OtlpEndpoint` is set (ELK/Jaeger collectors).

## Without Aspire

`docker compose up` includes Jaeger + Redis; set the same OTEL env vars on the API container.
