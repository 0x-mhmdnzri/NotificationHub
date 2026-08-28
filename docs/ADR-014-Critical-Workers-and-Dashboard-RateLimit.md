# ADR 0014: Dedicated Critical Hangfire Server + Dashboard Rate Limit

## Status
Accepted

## Context
Critical outbox jobs (OTP, security) share process capacity with bulk notification and reconciliation work. Hangfire dashboard polling can also burn rate-limit budget or be abused.

## Decision

### 1. Dual Hangfire servers (same process, isolated queues)

| Server | Queues | WorkerCount default |
|--------|--------|---------------------|
| `critical-{machine}` | `critical` only | `max(4, ProcessorCount)` |
| `standard-{machine}` | `notifications`, `outbox`, `default` | `max(2, ProcessorCount)` |

Config: `HangfireMessaging:DedicatedCriticalServer` (default true).  
Disable to fall back to a single server polling all queues.

### 2. RabbitMQ delivery workers (optional scale-out)

- `RabbitMQ:ConsumeCriticalOnly=true` → only `*.critical` queues  
- `RabbitMQ:ConsumeNonCriticalOnly=true` → skip critical  
Use separate AppHost/worker instances with higher `WorkerMaxConcurrency` for critical.

### 3. Dashboard rate limit

Middleware on `/hangfire*` with key `hangfire-dashboard:{apiKey|ip}`.  
Limit: `HangfireMessaging:DashboardRateLimitPerMinute` (default 30).  
Returns **429** + `Retry-After: 60` when exceeded. Independent of public API limits.

## Consequences
+ Critical dispatch capacity is reserved
+ Dashboard abuse cannot exhaust API rate limiter
- Two Hangfire server registrations (slightly more memory / heartbeat rows)
