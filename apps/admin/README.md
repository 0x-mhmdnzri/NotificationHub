# NotificationHub Admin

Production-oriented Next.js admin control plane built around the NotificationHub OpenAPI contract.

## Stack

- Next.js App Router + TypeScript
- Tailwind CSS
- shadcn/ui-inspired primitives
- Framer Motion
- TanStack Query
- Recharts

## P1 implementation

### Tenant-aware application shell

The active tenant is held by `TenantProvider`, persisted in local storage and propagated by the API client as `X-Tenant-Id`. Changing tenant invalidates server-state queries so tenant data is never intentionally reused across contexts.

### Templates

Implemented against the provided contract:

- `POST /api/v1/templates`
- `GET /api/v1/templates`
- `GET /api/v1/templates/{key}`
- `DELETE /api/v1/templates/{key}`
- `POST /api/v1/templates/preview`

The template workspace supports channel and locale filtering, version/state visibility, create/edit/delete, subject/body/HTML body, active state and client-side placeholder preview.

### Notifications

Implemented against:

- `POST /api/v1/notifications`
- `POST /api/v1/notifications/sync`
- `GET /api/v1/notifications/{id}`
- `POST /api/v1/templates/preview`

Composer supports recipient, template, channel, priority, locale, category, scheduling, time zone, idempotency key, preferred provider, collapse key, provider fallback and JSON data.

`/notifications/status` polls the supported status endpoint until a terminal state.

The OpenAPI contract does **not** expose a notification history collection endpoint, so the UI does not fabricate one.

### Campaigns

Implemented against:

- `POST /api/v1/campaigns`
- `POST /api/v1/campaigns/{id}/recipients`
- `POST /api/v1/campaigns/{id}/recipients/import`
- `POST /api/v1/campaigns/{id}/send`
- `POST /api/v1/campaigns/{id}/cancel`
- `GET /api/v1/campaigns/{id}`
- `GET /api/v1/campaigns/{id}/progress`

The workspace supports campaign creation, multi-channel selection, UTC scheduling, JSON campaign data, recipient entry, CSV import, send, cancel and progress inspection.

## API configuration

Copy `.env.example` to `.env.local`:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
```

## Run

```bash
npm install
npm run dev
```

## Contract limitations

The supplied OpenAPI document does not define authentication endpoints, notification list/history, campaign list, or dashboard aggregate endpoints. The application deliberately does not invent those server APIs. Mock data is retained only where the contract cannot support a real query.

## P2 implementation

The operational surfaces are now backed by the supplied OpenAPI contract instead of static tables:

- Workflow Studio: save definitions and start runs using `WorkflowStep` branching/delay/notification fields.
- Segments: save definitions and evaluate arbitrary attribute payloads.
- Devices: register, list and unregister tokens.
- Topics: create, subscribe, unsubscribe and inspect subscribers.
- Webhooks: create signed event subscriptions.
- Consents: record and evaluate consent.
- Preferences: save and retrieve user preferences.
- Engagement: track events, query stats and inspect notification events.
- Plugins & Messaging: inspect messaging health and provider model.
- Broadcast: send one-shot messages to recipients or a segment.

No unsupported collection endpoints were invented. Where the contract only exposes command/lookup operations, the UI exposes those operations directly.

## Contract fidelity

This phase intentionally does not introduce frontend calls for endpoints absent from the supplied OpenAPI document. Realtime behavior is implemented only where the contract exposes status/progress/run inspection endpoints, using TanStack Query polling. No SSE, SignalR, WebSocket, notification history, campaign listing, or dashboard aggregate endpoint is assumed.
