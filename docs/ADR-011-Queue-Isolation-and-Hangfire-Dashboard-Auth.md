# ADR 0011: Queue Isolation + Hangfire Dashboard API-Key Auth

## Status
Accepted

## Context
Skill guidance: do not put unrelated workloads on one queue; protect Hangfire dashboard in production.

## Decision

### Hangfire queues (priority order)
| Queue | Use |
|-------|-----|
| `critical` | Critical priority notification outbox dispatch |
| `notifications` | Standard notification outbox dispatch |
| `outbox` | Reconciliation / stuck recovery |
| `default` | Catch-all maintenance |

Accept path: `ScheduleDispatch(outboxId, MessagingQueues.ForPriority(request.Priority))`.

### RabbitMQ priority routing
`RabbitMQ:PriorityRouting=true` (default): Critical messages use routing/queue suffix `.critical` so bulk traffic cannot starve OTP/security alerts. Workers consume both normal and critical queues.

### Dashboard auth
`HangfireApiKeyAuthorizationFilter` validates the **same API keys** as REST:

- Header `X-Api-Key`
- `Authorization: Bearer` / `ApiKey`
- Query `?api_key=`

Default: **Admin role required**.  
Config: `HangfireMessaging:DashboardRequireAdmin`, `DashboardAllowAnonymousInDevelopment`.

## Consequences
+ Critical work isolated from bulk notification backlog
+ Dashboard no longer open by default
- More queues to monitor
- Ops must pass API key to open `/hangfire`
