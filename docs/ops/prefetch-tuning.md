# Prefetch Tuning & Load Testing

## Goal
Maximize durable throughput without overloading downstream providers (SMS/Email APIs) or starving consumers.

## Key formula
```text
approx_in_flight ≈ PrefetchCount × number_of_consumers
```
Each unacked message holds a prefetch slot. High prefetch improves throughput when processing is fast; it amplifies blast radius when providers slow down.

## Recommended starting points

| Profile | PrefetchCount | When |
|---------|---------------|------|
| Conservative | 5–10 | Unknown provider limits, first production rollout |
| Balanced (default) | 10–20 | Mixed email/SMS, moderate RPS |
| Aggressive | 30–50 | Fast providers, multiple workers, monitored DLQ/outbox |
| Avoid | >100 | Unless measured; risk of memory + provider 429 storms |

Also align:
- `RateLimiting:PerMinute` so API accept path does not outrun processing forever
- Provider concurrency / timeouts
- Outbox relay batch size (currently 50/tick)

## How to load-test

### 1. Bring stack up
```bash
docker compose up -d
# wait for API healthy
```

### 2. Run load test
```bash
export BASE_URL=http://localhost:8080
export API_KEY=dev-secret-key-change-me
export TOTAL=1000
export CONCURRENCY=50
./scripts/loadtest.sh
```

Or:
```bash
dotnet run --project tools/loadtest -c Release -- \
  --baseUrl http://localhost:8080 \
  --apiKey dev-secret-key-change-me \
  --total 1000 \
  --concurrency 50
```

### 3. Watch messaging health while testing
```http
GET /api/v1/admin/messaging/health
```
Watch:
- `outboxPendingCount` / `oldestPendingAgeSeconds` (publish lag)
- `workQueueDepth` (consume lag)
- `dlqDepth` (poison / systemic failure)

## Decision tree
1. **API latency high, queues empty** → scale API / DB (PgBouncer pool), not prefetch.
2. **workQueueDepth rising, providers healthy** → increase `PrefetchCount` or add consumer instances.
3. **provider errors / 429** → decrease prefetch, tighten rate limits, check failover health scores.
4. **outbox pending age rising** → Rabbit publish path / confirms / broker connectivity issue (not prefetch).
5. **dlqDepth > 0** → inspect payload/provider bugs before raising throughput.

## Changing prefetch
`appsettings.*.json`:
```json
"RabbitMQ": {
  "PrefetchCount": 20
}
```
Requires process restart (set at channel `BasicQos`).

## Safety rails already in platform
- Publisher confirms on outbox relay publish
- Manual ack after process
- Inbox idempotency
- DLX/DLQ + max redelivery
- Messaging health alerts (`MESSAGING_ALERT ...`)
