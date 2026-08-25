# ADR 0002: PostgreSQL with PgBouncer for Notification Status Persistence

## Status

Accepted

## Date

2026-08-25

## Context

NotificationHub must persist delivery status, idempotency keys, scheduled payloads, user preferences, audit entries, and webhook subscriptions. The service is expected to handle concurrent API traffic and background workers that update status frequently.

Constraints:

- Need a relational model for status queries, unique idempotency constraints, and tenant-scoped indexes.
- Connection pressure from API + workers must not overwhelm the database.
- Deployment target includes Docker Compose based local/prod-like environments.
- Team is on .NET and EF Core.

MARS (Multiple Active Result Sets) was requested in discussion, but MARS is a SQL Server feature and does not apply to PostgreSQL/Npgsql.

## Decision

We will use **PostgreSQL** as the system of record and place **PgBouncer** in front of it in transaction pooling mode.

Application connection policy:

- App connects to PgBouncer, not directly to PostgreSQL.
- Npgsql pool settings use explicit `Minimum Pool Size` and `Maximum Pool Size`.
- Connection string includes `No Reset On Close=true` for PgBouncer transaction mode compatibility.
- Schema is managed with official EF Core migrations.

## Alternatives Considered

### Option A: SQL Server
- **Pros:**
  - Native MARS support
  - Strong EF Core tooling familiarity in some .NET teams
- **Cons:**
  - Heavier operational cost in containerized environments
  - Less ideal default for this Linux/Docker deployment path
- **Why rejected:**
  - PostgreSQL fits the deployment and cost constraints better; MARS is not required for the access patterns.

### Option B: Direct PostgreSQL connections without PgBouncer
- **Pros:**
  - Simpler topology
  - Fewer moving parts
- **Cons:**
  - Higher risk of connection exhaustion under worker + API concurrency
  - Harder to enforce centralized pooling limits
- **Why rejected:**
  - Background processing and multi-instance scale make connection management a first-class concern.

### Option C: Redis-only persistence
- **Pros:**
  - Very fast status writes
  - Natural fit for ephemeral queue-like data
- **Cons:**
  - Weak relational querying for audit/history
  - Durability and complex constraint handling are weaker by default
- **Why rejected:**
  - Status, audit, preferences, and idempotency benefit from relational constraints and queryability.

## Consequences

**Positive:**
- Durable status and audit history
- Controlled database connection pressure via PgBouncer
- Clear migration path with EF Core

**Negative / trade-offs:**
- Additional infrastructure component (PgBouncer) to operate
- Transaction pooling restricts some session-level PostgreSQL features
- Operational complexity versus a single database container

**Risks / follow-up actions:**
- Monitor pool saturation (`MAX_CLIENT_CONN`, `DEFAULT_POOL_SIZE`, app max pool)
- Keep long transactions out of the request path
- Review indexes on `IdempotencyKey`, `Status`, `ScheduledAt`, and tenant fields under real load

## References

- Related ADRs:
  - ADR 0001: Microkernel architecture
  - ADR 0003: RabbitMQ queue
- Design docs:
  - `docker-compose.yml`
  - `src/NotificationHub.Core/Persistence/`
- Discussion threads / tickets:
  - Phase 1 persistence decision (2026-08-25)
