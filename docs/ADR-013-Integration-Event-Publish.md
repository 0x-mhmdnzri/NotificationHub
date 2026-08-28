# ADR 0013: Integration Event Publish + Webhook Bridge

## Status
Accepted

## Decision

1. Outbox rows with `kind=integration` are published to RabbitMQ topic exchange `notification.events`.
2. After successful publish, `IntegrationEventWebhookBridge` invokes `IWebhookDispatcher` using the same event names.
3. Hangfire schedules integration dispatch on queue `outbox`.
4. Versioning policy documented in `docs/INTEGRATION-EVENT-VERSIONING.md`.

## Failure window

Publish succeeds → crash before mark published → Hangfire retry may republish.  
Mitigation: consumers and webhooks idempotent on `messageId`.
