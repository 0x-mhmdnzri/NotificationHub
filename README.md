# NotificationHub

## Solution structure (Microkernel)

```text
src/
├── Host/                 # Composition root (API / Aspire)
├── Kernel/               # Extension contracts (Abstractions)
├── BuildingBlocks/       # Domain · Application · Infrastructure · Core
└── Clients/              # Public SDK
Plugins/
├── Email/ Sms/ Chat/ Push/ InApp/
tests/
├── Architecture/ Unit/ Benchmarks/
```

See [ADR-012](docs/ADR-012-Solution-Structure-Microkernel.md).



**One place to send notifications** — email, SMS, push, chat, and in-app — with a clean API, reliable queues, and plugins you can swap without rewriting the core.

Built with **.NET 9**. Open source under **MIT**.

---

## What is this?

Imagine your product needs to:

- welcome a user by **email**
- send an OTP by **SMS**
- push a mobile alert
- post to **Slack / Telegram**
- show a message in an **in-app inbox**

NotificationHub is the service in the middle. You call one HTTP API; the platform queues the work, picks a provider (SendGrid, Twilio, FCM, …), retries on failure, and tracks status.

```
Your app  →  NotificationHub API  →  queue  →  channel workers  →  providers (email/SMS/push/…)
```

---

## Why it might interest you

| Idea | In plain words |
|------|----------------|
| **Plugins** | Each provider is a small package. Add Resend or Kavenegar without touching the core. |
| **Reliable send** | Messages are saved in the database first (outbox), then published to RabbitMQ — so a brief broker outage does not lose work. |
| **Per-channel workers** | Email, SMS, and push can run as separate processes so one slow provider does not block the others. |
| **Campaigns** | Batch send to many recipients (list or CSV) with progress tracking. |
| **Observability** | Logs (Serilog), traces (OpenTelemetry → Jaeger), health checks for Postgres / Redis / RabbitMQ. |
| **Local stack** | One Aspire command brings up API, workers, database, queue, Redis, and Jaeger. |

Deep design choices live in [docs/](docs/) as **ADRs** (Architecture Decision Records). Start with [docs/README.md](docs/README.md) if you want the “why”.

---

## Quick start (pick one)

### Option A — Full local stack (recommended)

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download) and Docker (for containers Aspire starts).

```bash
git clone https://github.com/0x-mhmdnzri/NotificationHub.git
cd NotificationHub
dotnet run --project src/NotificationHub.AppHost
```

You get:

- **API** — accepts send requests
- **Workers** — `worker-email`, `worker-sms`, `worker-push`
- **Postgres, RabbitMQ, Redis, Jaeger**

Then open the Aspire dashboard (URL is printed in the terminal) and Jaeger at `http://localhost:16686`.

### Option B — Docker Compose

```bash
cp .env.example .env   # if the file exists
docker compose up -d --build
```

API typically on **port 8080**.

### Option C — Single process (simplest code path)

```bash
dotnet restore
dotnet run --project src/NotificationHub.Host
```

API + background jobs run together. Swagger appears only in **Development**.

---

## Send your first notification

Most routes need an API key header:

```http
X-Api-Key: nh_<keyId>_<secret>
```

Example (.NET client in `src/NotificationHub.Sdk`):

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

Also available: [JavaScript](clients/notificationhub.js) and [Python](clients/notificationhub.py) samples under `clients/`.

Useful endpoints:

| Method | Path | What it does |
|--------|------|----------------|
| `POST` | `/api/v1/notifications` | Queue a send (async) |
| `POST` | `/api/v1/notifications/sync` | Try to send immediately |
| `GET`  | `/api/v1/notifications/{id}` | Check status |
| `GET`  | `/health/ready` | Is DB / Redis / RabbitMQ OK? |

---

## Channels & providers

| Channel | Examples |
|---------|----------|
| Email | SendGrid, SMTP, Resend, Amazon SES |
| SMS | Kavenegar, Sms.ir, Twilio |
| Push | FCM, Expo |
| Chat | Slack, WhatsApp, Telegram, Discord, Teams |
| In-app | Built-in inbox |

How to write a plugin: [docs/sdk/plugin-sdk.md](docs/sdk/plugin-sdk.md).

---

## How the pieces fit (simple map)

```
┌─ Your client ─────────────────────────────────────┐
│  HTTP + API key                                     │
└───────────────────────┬─────────────────────────────┘
                        ▼
┌─ API (NotificationHub.Host) ──────────────────────┐
│  Validate → save status → write Outbox              │
└───────────────────────┬─────────────────────────────┘
                        ▼
┌─ RabbitMQ ────────────────────────────────────────┐
│  queues: notifications.email / .sms / .push / …     │
└───────┬──────────────────┬────────────────┬─────────┘
        ▼                  ▼                ▼
   worker-email       worker-sms       worker-push
        │                  │                │
        ▼                  ▼                ▼
     plugins            plugins          plugins
```

**Folder map** (only what newcomers need):

| Path | Role |
|------|------|
| `src/NotificationHub.Host` | HTTP API (and worker processes under Aspire) |
| `src/NotificationHub.Core` | Domain logic, queue, campaigns, security |
| `src/NotificationHub.Application` | Use-cases (CQRS / MediatR) |
| `src/NotificationHub.AppHost` | Local multi-service launcher (Aspire) |
| `src/NotificationHub.ServiceDefaults` | Logging, tracing, health |
| `Plugins/` | One project per provider |
| `docs/` | ADRs + ops notes |
| `tests/` | Automated tests |

---

## Configuration (minimal)

Put secrets in environment variables or `appsettings.Development.json` — **never commit real keys**.

| Setting | Meaning |
|---------|---------|
| `ConnectionStrings:Default` | PostgreSQL |
| `ConnectionStrings:Redis` | Optional (rate limits, inbox stream) |
| `RabbitMQ:*` | Broker host and credentials |
| `Auth:BootstrapApiKey` | First API key to bootstrap |
| `Plugins:*` | Provider API keys / tokens |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Where to send traces (e.g. Jaeger) |

More detail: [docs/ops/orchestration-otel-aspire.md](docs/ops/orchestration-otel-aspire.md).

---

## Tests

```bash
dotnet test
```

---

## Learn more

| Topic | Link |
|-------|------|
| Decision log (ADRs) | [docs/README.md](docs/README.md) |
| Messaging reliability | [docs/ops/messaging-reliability.md](docs/ops/messaging-reliability.md) |
| Aspire / tracing / health | [docs/ops/orchestration-otel-aspire.md](docs/ops/orchestration-otel-aspire.md) |
| Plugin SDK | [docs/sdk/plugin-sdk.md](docs/sdk/plugin-sdk.md) |

---

## Collaborate

If this project is useful to you — or you want to improve plugins, docs, tests, workers, or the API — **you are welcome to collaborate**.

- Open an issue or pull request on GitHub  
- Or write directly: **[nazari.mohammad80@icloud.com](mailto:nazari.mohammad80@icloud.com)**

Ideas, bug reports, and contributions of any size are appreciated.

---





## Domain vs Integration events

Integration events publish to RabbitMQ topic exchange `notification.events` and trigger webhooks.
See [versioning policy](docs/INTEGRATION-EVENT-VERSIONING.md).


- **Domain events** stay in-process (`NotificationHub.Domain`)
- **Integration events** (`NotificationHub.Abstractions.IntegrationEvents`) are versioned contracts (`notification.accepted` v1, …)
- Mapper flattens VOs → primitives; outbox payload `kind: integration`
- Preference/consent suppress uses `Notification.Accept` → `Suppress(reason)`

## Messaging reliability (Hangfire + Outbox + RabbitMQ)

```text
Accept TX (status + outbox) → COMMIT → Hangfire job(outboxId) → RabbitMQ → Worker + Inbox
```

- Hangfire = durable **job** execution (not a broker)
- RabbitMQ = delivery backbone
- At-least-once + inbox idempotency
- Recurring reconciliation for stuck outbox rows
- Dashboard: `/hangfire` (API key required, Admin by default — header `X-Api-Key`; rate-limited separately)

Hangfire: dedicated **critical** server (`DedicatedCriticalServer`) with higher `CriticalWorkerCount` so bulk jobs cannot starve OTP dispatch.

### Queue isolation
- Hangfire: `critical` > `notifications` > `outbox` > `default`
- RabbitMQ: `PriorityRouting` → `*.critical` queues for Critical priority

- See [ADR-009](docs/ADR-009-Hangfire-Messaging-Reliability.md)

## Latency & concurrency

Hot paths use **bounded parallel work** (not unbounded fan-out):

| Stage | Knobs |
|-------|--------|
| Outbox → RabbitMQ | `OutboxRelay:BatchSize`, `IdlePollIntervalMs`, `BusyPollIntervalMs`, `PublishConcurrency` |
| Delivery workers | `RabbitMQ:PrefetchCount`, `WorkerMaxConcurrency` |
| Campaign Accept | `Campaigns:BatchSize`, `AcceptConcurrency`, adaptive poll |

See [ADR-007](docs/ADR-007-Latency-and-Concurrency.md).

## Worker management (RabbitMQ)

Delivery workers use **competing consumers + fair dispatch (prefetch) + application concurrency**:

| Setting | Default | Controls |
|---------|---------|----------|
| `RabbitMQ:PrefetchCount` | 16 | Broker delivery (QoS) |
| `RabbitMQ:WorkerMaxConcurrency` | 8 | Parallel process tasks |
| `RabbitMQ:ConsumerBufferCapacity` | 32 | Bounded hand-off buffer |

ACK only after successful processing + inbox mark; delayed retry then DLQ. See [ADR-006](docs/ADR-006-RabbitMQ-Worker-Management.md) and [ops/worker-management](docs/ops/worker-management.md).

## Domain-Driven Design

Core business rules live in `src/NotificationHub.Domain` (pure .NET, no EF/RabbitMQ).

| Aggregate | Boundary |
|-----------|----------|
| `Notification` | Single delivery lifecycle |
| `BroadcastCampaign` | Campaign lifecycle (recipients coordinated outside) |

See [ADR 0005](docs/ADR-005-Domain-Driven-Design.md).

EF adapters + domain event outbox + architecture tests enforce Domain independence.

Domain unit tests (no database):

```bash
dotnet test tests/NotificationHub.Domain.Tests
```

## License

[MIT](LICENSE) — use it, fork it, ship it.

## Admin console (Next.js)

```bash
cd apps/admin
cp .env.local.example .env.local
npm install && npm run dev
```

Open http://localhost:3000 — set API base `http://localhost:5245` and your Host API key under **Settings**.
