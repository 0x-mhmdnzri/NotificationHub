# NotificationHub

Multi-channel notification service with **Microkernel (Plugin) Architecture**.

## Phase 1 + Phase 2 (Complete)

### Core
- Unified async/sync Send API
- Channel abstraction + pluggable providers
- Preferred provider + automatic failover
- Template engine (`{{vars}}`, locale, versioning, preview)
- RabbitMQ durable queue
- Retry with exponential backoff + DeadLetter
- PostgreSQL status tracking via PgBouncer
- Idempotency keys
- API Key auth
- Rate limiting
- Official EF Core migrations

### Phase 2
- User preferences (opt-out, quiet hours, frequency cap)
- Smart routing & channel/provider fallback
- Scheduling (timezone-aware via ScheduledAt)
- Webhooks (HMAC signed)
- Multi-tenant fields (TenantId isolation indexes)
- Audit trail
- Template preview endpoint
- Attachment support (email plugins)
- Providers: SendGrid, SMTP, Kavenegar, Sms.ir

## Providers

| Channel | Provider  | Id              |
|---------|-----------|-----------------|
| Email   | SendGrid  | email-sendgrid  |
| Email   | SMTP      | email-smtp      |
| SMS     | Kavenegar | sms-kavenegar   |
| SMS     | Sms.ir    | sms-smsir       |

Preferred + fallback order configured under `Providers` in appsettings.

## Infrastructure

```bash
cp .env.example .env
docker compose up -d
```

| Service    | Port        |
|------------|-------------|
| PostgreSQL | 5432        |
| PgBouncer  | 6432        |
| RabbitMQ   | 5672/15672  |
| API        | 8080        |

## API

```
POST /api/v1/notifications
POST /api/v1/notifications/sync
GET  /api/v1/notifications/{id}
GET  /api/v1/plugins
POST /api/v1/templates
GET  /api/v1/templates/{key}
POST /api/v1/templates/preview
GET  /api/v1/preferences/{userId}
PUT  /api/v1/preferences
POST /api/v1/webhooks
GET  /api/v1/audit
GET  /health
```

Header: `X-Api-Key: dev-secret-key-change-me`

## License
MIT

## Architecture Decision Records

- [ADR 0001: Microkernel Architecture](docs/ADR-001-Microkernel-Architecture.md)
- [ADR 0002: PostgreSQL + PgBouncer](docs/ADR-002-PostgreSQL-PgBouncer-Persistence.md)
- [ADR 0003: RabbitMQ Queue](docs/ADR-003-RabbitMQ-Queue.md)
- [ADR 0004: Regional SMS Providers](docs/ADR-004-Regional-SMS-Providers.md)

## Running tests

```bash
dotnet test
```

Test case catalog: `tests/test-cases/core-test-cases.md`
