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
- EF Core Outbox + Inbox (SKIP LOCKED claiming, dual-write safe transactions)
- PostgreSQL status store (optionally via PgBouncer)
- Scheduling (`ScheduledAt`), retry / dead-letter, audit trail

### Preferences & compliance
- Channel / category opt-in, quiet hours, daily cap, weekly availability
- Critical priority bypasses schedule (not hard opt-out)
- Consent ledger, retention sweep, GDPR-style export / delete
- Preference **embed contract** for preference centers

### Orchestration
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
- Minimal public `/health` + `/health/ready`
- OIDC config stub for future dashboard SSO
- Plugin hot-reload from directory (`POST /api/v1/admin/plugins/reload`)

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

### Local run

```bash
dotnet restore
dotnet build
dotnet run --project src/NotificationHub.Host
```

Swagger is available only in **Development**.

---

## Configuration (high level)

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:Default` | PostgreSQL |
| `ConnectionStrings:Redis` | Optional: distributed rate limit + inbox SSE |
| `RabbitMQ:*` | Host, credentials, queue topology |
| `Auth:BootstrapApiKey` | Bootstrap key (`nh_{guid}_{secret}`) |
| `Auth:AdminIpAllowlist` | Optional admin IP list |
| `ForwardedHeaders:KnownProxies` | Trusted proxies only |
| `RateLimiting:PerMinute` / `AuthFailuresPerMinute` | Limits |
| `CircuitBreaker:*` | Failure threshold / open duration |
| `Plugins:Telegram:BotToken` etc. | Per-provider secrets |
| `Plugins:Directory` | Optional DLL folder for hot-load |
| `NotificationHub:Environment` | `Development` / `Staging` / `Production` |
| `Auth:Oidc:*` | Future dashboard SSO (stub) |

**Do not commit secrets.** Base `appsettings.json` keeps placeholders; local values go in `appsettings.Development.json` or environment variables.

---

## Authentication

All API routes (except `/health`, `/health/ready`, and Development swagger) require:

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

### Workflows & segments
| Method | Path |
|--------|------|
| `POST/GET` | `/api/v1/workflows`, `/api/v1/workflows/{key}` |
| `POST`     | `/api/v1/workflows/start`, `.../import`, `.../code-first` |
| `GET`      | `/api/v1/workflows/{key}/export`, runs, timeline |
| `POST`     | `/api/v1/segments`, `/api/v1/segments/{key}/match` |

### Inbox, digest, topics, devices
| Method | Path |
|--------|------|
| `GET/POST` | `/api/v1/inbox/{userId}`, read / archive / SSE stream |
| `POST`     | `/api/v1/digest/policies`, `/api/v1/digest/buffer` |
| `POST`     | `/api/v1/topics`, subscribe, broadcast |
| `POST/GET/DELETE` | `/api/v1/devices` |

### CDP, campaigns, i18n
| Method | Path |
|--------|------|
| `POST` | `/api/v1/cdp/identify`, `/api/v1/cdp/track` |
| `GET`  | `/api/v1/cdp/profiles/{userId}` |
| `POST` | `/api/v1/campaigns/broadcast` |
| `POST/GET` | `/api/v1/i18n`, `/api/v1/i18n/{locale}` |

### Admin & health
| Method | Path |
|--------|------|
| `GET`  | `/health`, `/health/ready` |
| `GET`  | `/api/v1/admin/messaging/health`, `/api/v1/admin/metrics`, `/api/v1/admin/activity` |
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

Example (.NET):

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
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│  HTTP API   │────▶│ Orchestrator │────▶│   Outbox    │
│  (Minimal)  │     │ Preferences  │     │  (EF Core)  │
└─────────────┘     │ Throttle/CDP │     └──────┬──────┘
                    └──────────────┘            │
                                                ▼
                                         ┌─────────────┐
                                         │  RabbitMQ   │
                                         └──────┬──────┘
                                                │
                    ┌───────────────────────────┼──────────────────┐
                    ▼                           ▼                  ▼
              Channel plugins            Inbox / Digests     Workflow worker
           (email/sms/chat/push)         Redis optional      Step handlers
```

**ADRs**
- [ADR 0001: Microkernel](docs/ADR-001-Microkernel-Architecture.md)
- [ADR 0002: PostgreSQL + PgBouncer](docs/ADR-002-PostgreSQL-PgBouncer-Persistence.md)
- [ADR 0003: RabbitMQ](docs/ADR-003-RabbitMQ-Queue.md)
- [ADR 0004: Regional SMS](docs/ADR-004-Regional-SMS-Providers.md)

**Ops notes**
- [Messaging reliability](docs/ops/messaging-reliability.md)
- [Security hardening](docs/ops/security-hardening-phase0.md)
- [Phase 1–5 feature notes](docs/ops/)

---

## Testing & CI

```bash
dotnet test
```

- Unit tests: `tests/NotificationHub.Core.Tests` (requirement-driven cases under `tests/docs/`)
- CI (GitHub Actions): restore → build → test → publish Host; NuGet vulnerability audit; Docker image build

```bash
dotnet build NotificationHub.sln -c Release
dotnet publish src/NotificationHub.Host/NotificationHub.Host.csproj -c Release -o ./publish
```

---

## Project layout

```
src/
  NotificationHub.Abstractions/   # contracts & models
  NotificationHub.Core/           # domain, messaging, security
  NotificationHub.Host/           # Minimal API host
  NotificationHub.Sdk/            # .NET client
Plugins/                          # channel plugins (one package each)
clients/                          # JS / Python samples
tests/                            # xUnit tests + case catalogs
docs/                             # ADRs, ops, SDK
```

---

## License

MIT

## Build & package management

Versions are centralized:

- `Directory.Packages.props` — NuGet package versions (CPM)
- `Directory.Build.props` — shared TFM (`net9.0`), nullable, RID list, light-runtime defaults

### Multi-platform publish (x64 / x86 / arm / arm64)

```bash
./scripts/publish-all.sh
# artifacts/publish/{linux-x64,linux-arm64,linux-arm,win-x64,win-x86,win-arm64,osx-x64,osx-arm64}
```

Framework-dependent publish (smaller). Requires matching .NET 9 runtime on the target.

### Light local run

```bash
./scripts/run-light.sh
```

## Latency & benchmarks

See [docs/ops/latency.md](docs/ops/latency.md).

Available Benchmark:
  #0 HotPathBenchmarks


You should select the target benchmark(s). Please, print a number of a benchmark (e.g. `0`) or a contained benchmark caption (e.g. `HotPathBenchmarks`).
If you want to select few, please separate them with space ` ` (e.g. `1 2 3`).
You can also provide the class name in console arguments by using --filter. (e.g. `--filter '*HotPathBenchmarks*'`).
Enter the asterisk `*` to select all.
To print all available benchmarks use `--list flat` or `--list tree`.
To learn more about filtering use `--help`.
Stress target=http://localhost:8080 total=2000 concurrency=100 warmup=50

## Architecture (CQRS + Vertical Slices)

Write and read **data-flow pipelines** are separated via **MediatR 12.4.1** + vertical feature folders:

| Side | Marker | Examples |
|------|--------|----------|
| Write | `ICommand` / `ICommand<T>` | AcceptNotification, SaveTemplate, SendSync |
| Read | `IQuery<T>` | GetNotificationStatus, GetTemplate, ListTemplates |

Pipeline: Validation → Logging → CommandOnly / QueryOnly behaviors.

See [docs/ADR-010-CQRS-MediatR.md](docs/ADR-010-CQRS-MediatR.md).

## Batch Broadcast Campaigns



Worker:  (config section ). See [docs/ADR-011-Batch-Broadcast-Campaigns.md](docs/ADR-011-Batch-Broadcast-Campaigns.md).
