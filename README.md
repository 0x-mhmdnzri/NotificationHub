# NotificationHub

Multi-channel notification service built with **Microkernel (Plugin) Architecture**.

## Architecture

- **Core (Microkernel)**: Orchestration, plugin lifecycle, template engine, queue, retry, status tracking, rate limiting.
- **Abstractions**: Formal contracts (`IPlugin`, `IChannelPlugin`).
- **Plugins**: Channel/provider implementations (Email/SendGrid, SMS/Twilio).
- **Host**: ASP.NET Core API + background worker.

## Phase 1 Features (Implemented)

- Unified Send API (async queue + sync endpoint)
- Channel abstraction with pluggable providers
- Template engine with variables (`{{name}}`), versioning, basic localization (en/fa)
- Asynchronous processing (in-memory channel queue + background worker)
- Retry with exponential backoff (2s → 4s → 8s) + dead-letter status
- Delivery status tracking (`queued → processing → sent / failed / deadletter`)
- Idempotency keys
- Basic API Key authentication (`X-Api-Key` header)
- Rate limiting per tenant/channel
- Structured logging

## Quick Start

```bash
dotnet restore
dotnet run --project src/NotificationHub.Host
```

Default API Key: `dev-secret-key-change-me`

### Send (async - recommended)

```http
POST /api/v1/notifications
X-Api-Key: dev-secret-key-change-me
Content-Type: application/json

{
  "recipient": "user@example.com",
  "channel": "email",
  "templateKey": "welcome",
  "data": { "name": "Ali" },
  "idempotencyKey": "welcome-ali-001",
  "locale": "fa"
}
```

### Check status

```http
GET /api/v1/notifications/{id}
X-Api-Key: dev-secret-key-change-me
```

### Sync send

```http
POST /api/v1/notifications/sync
```

### Templates

```http
POST /api/v1/templates
GET  /api/v1/templates/{key}?channel=email&locale=en
```

## Configuration

```json
{
  "Auth": { "ApiKey": "your-secret" },
  "RateLimiting": { "PerMinute": 60 },
  "Plugins": {
    "SendGrid": { "ApiKey": "SG.xxx" },
    "Twilio": { "AccountSid": "ACxxx", "AuthToken": "xxx" }
  }
}
```

## Roadmap

**Phase 2**: Preferences, smart routing/fallback, scheduling, digests, webhooks, multi-tenancy, audit trail, attachments  
**Phase 3**: Workflow engine, Push/WhatsApp/In-App, analytics, compliance, Admin UI

## License

MIT

## Providers (Real)

| Channel | Provider   | Plugin Id          | Config Section              |
|---------|------------|--------------------|-----------------------------|
| Email   | SendGrid   | email-sendgrid     | Plugins:SendGrid            |
| Email   | SMTP       | email-smtp         | Plugins:Smtp                |
| SMS     | Twilio     | sms-twilio         | Plugins:Twilio              |
| SMS     | Kavenegar  | sms-kavenegar      | Plugins:Kavenegar           |

Configuration is split across:
- `appsettings.json` (common)
- `appsettings.Development.json`
- `appsettings.Production.json`

Secrets should come from environment variables or secret manager in production (e.g. `Plugins__Kavenegar__ApiKey`).

## Infrastructure

| Service    | Port        | Notes                                      |
|------------|-------------|--------------------------------------------|
| PostgreSQL | 5432        | Primary data store                         |
| PgBouncer  | 6432→5432   | Transaction pooling, pool size controlled  |
| RabbitMQ   | 5672 / 15672| Queue + management UI                      |
| API        | 8080        | NotificationHub.Host                       |

```bash
docker compose up -d
```

Connection string uses:
- `Minimum Pool Size` / `Maximum Pool Size` (app-side pool)
- `No Reset On Close=true` (required for PgBouncer transaction mode)
- App connects to **PgBouncer**, not directly to Postgres

> MARS is SQL Server only and is not used. Npgsql pool + PgBouncer handle concurrency.
