# Integration Event Versioning Policy

## Rules

1. **Domain events never leave the process.** Only mapped **integration events** are published.
2. Wire format is versioned via `version` + stable `eventType` (e.g. `notification.accepted`).
3. **Additive changes** (new optional fields on V1 payload) are allowed without a new type.
4. **Breaking changes** require a **new contract type** and version bump:
   - `NotificationAcceptedV1` → `NotificationAcceptedV2`
   - `eventType` may stay the same with `version: 2`, or use `notification.accepted.v2` if consumers prefer explicit keys.
5. Publishers MAY emit V1 and V2 during a migration window.
6. Consumers MUST ignore unknown fields and MUST key idempotency on `messageId`.
7. Deprecation: announce ≥ 1 release before removing V1 emission.

## Current catalog (V1)

| eventType | Payload type | When |
|-----------|--------------|------|
| notification.accepted | NotificationAcceptedV1 | Accept queued/scheduled |
| notification.suppressed | NotificationSuppressedV1 | Preference/consent deny |
| notification.sent | NotificationSentV1 | Provider success |
| notification.failed | NotificationFailedV1 | Provider failure |
| notification.dead_lettered | NotificationDeadLetteredV1 | Exhausted retries |
| notification.cancelled | NotificationCancelledV1 | Cancelled |
| campaign.status_changed | CampaignStatusChangedV1 | Campaign lifecycle |

## Broker topology

```text
Exchange: notification.events (topic, durable)
Routing key: {eventType}
Headers: event-type, event-version, message-id, tenant-id?
```

Bind examples:
- `notification.#` — all notification events
- `notification.suppressed` — suppress only
- `campaign.#` — campaign events

## Webhooks

Same `eventType` names are forwarded via `IntegrationEventWebhookBridge` after broker publish.
