# Delayed Redelivery

## Why not `basic.nack(requeue=true)`?
Immediate requeue retries in a tight loop under sustained failure (provider outage), amplifies load, and offers no backoff.

## Pattern (no RabbitMQ plugin required)
1. On process failure, publish a copy to `notifications.retry` with routing key `notification.retry.{delaySeconds}`.
2. Each retry queue has:
   - `x-message-ttl` = delay
   - `x-dead-letter-exchange` = main work exchange
   - `x-dead-letter-routing-key` = main routing key
3. When TTL expires, RabbitMQ dead-letters the message **back to the work queue**.
4. Header `x-redelivery-count` increments each schedule.
5. After `MaxRedeliveryCount`, nack without requeue → final DLQ.

```text
work queue --fail--> retry.5s --ttl--> work queue
                 \-> retry.15s --ttl--> work queue
                 \-> ...
                 \-> max exceeded --> DLQ
```

## Config
```json
"RabbitMQ": {
  "RetryExchangeName": "notifications.retry",
  "RetryDelaySeconds": [5, 15, 30, 60, 120],
  "MaxRedeliveryCount": 5
}
```

## Observability
`GET /api/v1/admin/messaging/health` includes `retryQueueDepth` (sum of all delay queues).
