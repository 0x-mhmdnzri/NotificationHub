# NotificationHub Admin (Next.js)

Admin console for [NotificationHub](../../) API at `http://localhost:5245`.

## Quick start

```bash
# Terminal 1 — Host API
cd ../../
dotnet run --project src/Host/NotificationHub.Host
# copy bootstrap API key from logs

# Terminal 2 — Admin UI
cd apps/admin
cp .env.local.example .env.local
npm install
npm run dev
```

Open http://localhost:3000 → **Settings** → paste API base + key.

## Features (mapped from OpenAPI)

| Panel | API |
|-------|-----|
| Dashboard | `/health/*`, `/api/v1/admin/messaging/health`, `/api/v1/plugins` |
| Notifications | `POST /api/v1/notifications`, `/sync`, `GET /{id}` |
| Templates | CRUD + preview |
| Campaigns | create → recipients → send → progress |
| Workflows | save, start, timeline, cancel |
| Segments / Topics / Devices | manage |
| Preferences / Consents / Webhooks | compliance & integrations |
| Engagement | track + stats |

Auth header: `X-Api-Key` (stored in `localStorage`).
