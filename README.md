# NotificationHub

Multi-channel notification platform built with **.NET 9** and a **Microkernel (Plugin) architecture**.

Core stays small and stable; channels, providers, and extensions load as plugins. Messaging uses a **custom EF Outbox + RabbitMQ** path (MassTransit only if multi-message sagas become primary).

---

## Features

### Delivery core
- Unified async / sync send API with idempotency and collapse keys
- Preferred provider + automatic failover
- Templates with `{{placeholders}}`, locales, versioning, and preview
- Durable RabbitMQ queues (ack, prefetch, DLX/TTL, publisher confirms)
- **Per-channel work queues** (`notifications.email` / `.sms` / `.push`, …) with independent consumers
- EF Core Outbox + Inbox (SKIP LOCKED claiming, dual-write safe transactions)
- PostgreSQL status store (optionally via PgBouncer)
- Scheduling (`ScheduledAt`), retry / dead-letter, audit trail

### Preferences & compliance
- Channel / category opt-in, quiet hours, daily cap, weekly availability
- Critical priority bypasses schedule (not hard opt-out)
- Consent ledger, retention sweep, GDPR-style export / delete
- Preference **embed contract** for preference centers

### Orchestration
- **Broadcast state machine** (`BroadcastStateMachine`) for campaign lifecycle
- Workflow engine: `send`, `delay`, `condition`, `branch`, `http`
- Workflow DSL export / import and code-first `WorkflowBuilder`
- Segments, topics + broadcast, digest buffer + flush worker
- Frequency throttle policies (enforced on send)
- CDP identify / track (optional workflow trigger)

### Inbox & engagement
- In-app inbox feed, mark read / archive, SSE stream
- Multi-instance inbox bus (in-memory or **Redis pub/sub**)
- Open / click tracking pixels + cross-channel read sync
- Analytics summary and admin activity feed

### Content
- HTML layouts + partials (`{{content}}`, `{{>partial}}`)
- i18n localization catalog with English fallback

### Security & ops
- API key auth (PBKDF2 v2 + legacy SHA256 migration path)
- Role-based access, admin IP allowlist, CORS, security headers
- Rate limiting (in-memory or **Redis**), auth failure-only limits
- Webhook URL / redirect SSRF guards, request body size limits
- Circuit breaker on provider health
- Minimal public `/health` + `/health/live` + `/health/ready` (Postgres / Redis / RabbitMQ)
- OIDC config stub for future dashboard SSO
- Plugin hot-reload from directory (`POST /api/v1/admin/plugins/reload`)

### Observability
- **Serilog** enriched logs (console text or JSON for ELK)
- **OpenTelemetry** traces/metrics → OTLP (**Jaeger**)
- **.NET Aspire** AppHost for local composition and health

---

## Providers (plugins)

| Channel  | Provider        | Plugin id        |
|----------|-----------------|------------------|
| Email    | SendGrid        | `email-sendgrid` |
| Email    | SMTP            | `email-smtp`     |
| Email    | Resend          | `email-resend`   |
| Email    | Amazon SES      | `email-ses`      |
| SMS      | Kavenegar       | `sms-kavenegar`  |
| SMS      | Sms.ir          | `sms-smsir`      |
| SMS      | Twilio          | `sms-twilio`     |
| Chat     | Slack           | `chat-slack`     |
| Chat     | WhatsApp        | `chat-whatsapp`  |
| Chat     | Telegram        | `chat-telegram`  |
| Chat     | Discord         | `chat-discord`   |
| Chat     | MS Teams        | `chat-teams`     |
| Push     | FCM             | `push-fcm`       |
| Push     | Expo            | `push-expo`      |
| In-app   | Built-in        | `inapp`          |

Preferred / fallback order: `Providers` section in configuration.  
Plugin SDK notes: [`docs/sdk/plugin-sdk.md`](docs/sdk/plugin-sdk.md)

---

## Quick start

### Aspire (recommended for local full topology)

```bash
dotnet run --project src/NotificationHub.AppHost
```

Composes:

| Resource | Role |
|----------|------|
| `notification-api` | HTTP API + outbox relay (no delivery consume) |
| `worker-email` / `worker-sms` / `worker-push` | Per-channel delivery consumers |
| Postgres (`notificationdb`) | Persistence |
| RabbitMQ | Work queues + management UI |
| Redis | Rate limit + inbox bus |
| Jaeger | Traces (`http://localhost:16686`) |

### Docker Compose

```bash
cp .env.example .env   # if present
docker compose up -d --build
```

| Service    | Port        |
|------------|-------------|
| PostgreSQL | 5432        |
| PgBouncer  | 6432        |
| RabbitMQ   | 5672 / 15672|
| Redis      | 6379 (optional) |
| API        | 8080        |

### Monolithic local run (single process)

```bash
dotnet restore
dotnet build
dotnet run --project src/NotificationHub.Host
```

Default `Workers:*` flags keep API + delivery consumer + background jobs in one host.  
Swagger is available only in **Development**.

---

## Configuration (high level)

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:Default` | PostgreSQL |
| `ConnectionStrings:Redis` | Optional: distributed rate limit + inbox SSE |
| `RabbitMQ:*` | Host, credentials, topology |
| `RabbitMQ:ChannelRouting` | `true` → per-channel queues (default in Aspire) |
| `RabbitMQ:ConsumeChannel` | `email` / `sms` / `push` / … for a dedicated worker process |
| `Workers:RunDeliveryConsumer` | Register `NotificationBackgroundWorker` |
| `Workers:RunOutboxRelay` | Register `OutboxRelayWorker` |
| `Workers:RunCampaignDispatch` / `RunScheduled` / `RunWorkflow` / … | Role flags for Aspire split |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP endpoint (Jaeger collector) |
| `Serilog:UseJsonConsole` | Structured JSON for ELK / Filebeat |
| `Auth:BootstrapApiKey` | Bootstrap key (`nh_{guid}_{secret}`) |
| `Auth:AdminIpAllowlist` | Optional admin IP list |
| `ForwardedHeaders:KnownProxies` | Trusted proxies only |
| `RateLimiting:PerMinute` / `AuthFailuresPerMinute` | Limits |
| `CircuitBreaker:*` | Failure threshold / open duration |
| `Plugins:*` | Per-provider secrets |
| `Plugins:Directory` | Optional DLL folder for hot-load |
| `NotificationHub:Environment` | `Development` / `Staging` / `Production` |
| `Auth:Oidc:*` | Future dashboard SSO (stub) |

**Do not commit secrets.** Base `appsettings.json` keeps placeholders; local values go in `appsettings.Development.json` or environment variables.

---

## Authentication

All API routes (except `/health*`, and Development swagger) require:

```http
X-Api-Key: nh_<keyId>_<secret>
```

Roles: `Admin`, `Sender`, `Reader` (and combinations on endpoints).

---

## API overview

### Notifications
| Method | Path | Notes |
|--------|------|-------|
| `POST` | `/api/v1/notifications` | Queue send (transactional outbox) |
| `POST` | `/api/v1/notifications/sync` | Synchronous attempt |
| `GET`  | `/api/v1/notifications/{id}` | Status |

### Templates, preferences, webhooks
| Method | Path |
|--------|------|
| `POST/GET` | `/api/v1/templates`, `/api/v1/templates/{key}`, `/api/v1/templates/preview` |
| `GET/PUT`  | `/api/v1/preferences/{userId}`, `/api/v1/preferences` |
| `GET`      | `/api/v1/preferences/{userId}/embed` |
| `POST`     | `/api/v1/webhooks` |
| `GET`      | `/api/v1/audit` |

### Workflows, segments, campaigns
| Method | Path |
|--------|------|
| `POST/GET` | `/api/v1/workflows`, runs, timeline, import/export |
| `POST`     | `/api/v1/segments`, `/api/v1/segments/{key}/match` |
| `POST`     | `/api/v1/campaigns`, recipients, CSV, start, cancel |
| `GET`      | `/api/v1/campaigns/{id}`, `/api/v1/campaigns/{id}/progress` |

### Inbox, digest, topics, devices
| Method | Path |
|--------|------|
| `GET/POST` | `/api/v1/inbox/{userId}`, read / archive / SSE stream |
| `POST`     | `/api/v1/digest/policies`, `/api/v1/digest/buffer` |
| `POST`     | `/api/v1/topics`, subscribe, broadcast |
| `POST/GET/DELETE` | `/api/v1/devices` |

### Admin & health
| Method | Path |
|--------|------|
| `GET`  | `/health`, `/health/live`, `/health/ready` |
| `GET`  | `/api/v1/admin/messaging/health`, metrics, activity |
| `GET/POST` | `/api/v1/admin/api-keys` |
| `POST` | `/api/v1/admin/plugins/reload`, retention, digest flush |
| `GET`  | `/api/v1/providers/health`, `/api/v1/environment` |

Public tracking (rate-limited): `/t/o/{id}`, `/t/c/{id}?url=...`

---

## Client SDKs

| Client | Location |
|--------|----------|
| .NET   | `src/NotificationHub.Sdk` (`NotificationHubClient`) |
| Node   | `clients/notificationhub.js` |
| Python | `clients/notificationhub.py` |

```csharp
using var client = new NotificationHubClient("http://localhost:8080", apiKey);
await client.SendAsync(new NotificationRequest
{
    Recipient = "user@example.com",
    Channel = "email",
    TemplateKey = "welcome",
    Data = new Dictionary<string, object?> { ["name"] = "Ada" }
});
```

---

## Architecture

```
┌────────────────── Aspire AppHost ──────────────────┐
│  API (outbox)   worker-email   worker-sms  worker-push │
│       │              │             │            │      │
│       └──────────────┴──────┬──────┴────────────┘      │
│                             ▼                          │
│              Postgres · RabbitMQ · Redis · Jaeger      │
└────────────────────────────────────────────────────────┘

HTTP API ──▶ Orchestrator / Campaigns ──▶ EF Outbox
                                              │
                                              ▼
                                    RabbitMQ (per-channel)
                                              │
                    ┌─────────────────────────┼─────────────────────┐
                    ▼                         ▼                     ▼
              Email plugins              SMS plugins           Push plugins
```

**Important distinction (see ADRs):**

| Layer | What it is | What it is not |
|-------|------------|----------------|
| Aspire AppHost | Process composition & infra wiring | Business workflow engine |
| `BroadcastStateMachine` | Campaign lifecycle rules | Message broker topology |
| Channel workers | Independent delivery consumers | Orchestrators of other channels |

### Architecture Decision Records

| ADR | Title |
|-----|--------|
| [001](docs/ADR-001-Microkernel-Architecture.md) | Microkernel / plugin architecture |
| [002](docs/ADR-002-PostgreSQL-PgBouncer-Persistence.md) | PostgreSQL + PgBouncer persistence |
| [003](docs/ADR-003-RabbitMQ-Queue.md) | RabbitMQ + transactional outbox/inbox |
| [004](docs/ADR-004-Regional-SMS-Providers.md) | Regional SMS providers |
| [010](docs/ADR-010-CQRS-MediatR.md) | CQRS + MediatR vertical slices |
| [011](docs/ADR-011-Batch-Broadcast-Campaigns.md) | Batch broadcast campaigns |
| [012](docs/ADR-012-Aspire-Composition-vs-Business-Orchestration.md) | Aspire composition vs business orchestration |
| [013](docs/ADR-013-Per-Channel-Delivery-Workers.md) | Per-channel delivery workers |

Index: [`docs/README.md`](docs/README.md)

### Ops notes
- [Orchestration, OTEL, Aspire](docs/ops/orchestration-otel-aspire.md)
- [Messaging reliability](docs/ops/messaging-reliability.md)
- [Security hardening](docs/ops/security-hardening-phase0.md)
- [Latency](docs/ops/latency.md) · [Prefetch tuning](docs/ops/prefetch-tuning.md)
- Phase feature notes under [`docs/ops/`](docs/ops/)

---

## Observability

| Endpoint | Purpose |
|----------|---------|
| `/health` / `/health/live` | Liveness |
| `/health/ready` | Postgres + Redis + RabbitMQ readiness (JSON) |
| Jaeger UI | Traces (Aspire / compose → port **16686**) |
| Aspire dashboard | Resource graph, health, logs |

- **Serilog**: enrichers (Application, Environment, MachineName, ThreadId, LogContext / CorrelationId)
- **Console**: human template in dev; **JSON** when `Serilog:UseJsonConsole=true` or Production (ELK-friendly)
- **OTLP**: traces + metrics when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- **ActivitySource** `NotificationHub.Broadcast` on campaign state transitions

---

## Testing & CI

```bash
dotnet test
dotnet build NotificationHub.sln -c Release
dotnet publish src/NotificationHub.Host/NotificationHub.Host.csproj -c Release -o ./publish
```

- Unit tests: `tests/NotificationHub.Core.Tests`, `tests/NotificationHub.Application.Tests`
- CI: restore → build → test; NuGet vulnerability audit; Docker image build/publish
- Dockerfile restores **ServiceDefaults** in the Host project graph (required for publish)

---

## Project layout

```
src/
  NotificationHub.Abstractions/     # contracts & models
  NotificationHub.Core/             # domain, messaging, orchestration, security
  NotificationHub.Application/      # CQRS vertical slices
  NotificationHub.Infrastructure/   # DI wiring for application layer
  NotificationHub.Host/             # Minimal API host (also used as channel workers)
  NotificationHub.ServiceDefaults/  # Serilog, OTEL, health checks
  NotificationHub.AppHost/          # Aspire composition
  NotificationHub.Sdk/              # .NET client
Plugins/                            # channel plugins
clients/                            # JS / Python samples
tests/                              # xUnit + benchmarks
docs/                               # ADRs, ops, SDK
```

---

## Build & package management

- `Directory.Packages.props` — central NuGet versions (CPM)
- `Directory.Build.props` — shared TFM (`net9.0`), nullable, RID list

```bash
./scripts/publish-all.sh   # multi-RID under artifacts/publish/
./scripts/run-light.sh     # light local run
```

Benchmarks: `tests/NotificationHub.Benchmarks` (filter e.g. `--filter '*HotPathBenchmarks*'`).

---

## License

MIT
