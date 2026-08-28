# NotificationHub Admin

Production-style **demo console** for the NotificationHub API (`http://localhost:5245`).

Stack: **Next.js 15 · Tailwind CSS · shadcn/ui · TanStack Table · Sonner**

## Run

```bash
# Host API (from repo root)
dotnet run --project src/Host/NotificationHub.Host

# Admin UI
cd apps/admin
cp .env.local.example .env.local
npm install
npm run dev
```

Open http://localhost:3000 → **Settings** → paste API base + bootstrap key → **Test connection**.

## What each page is for

| Page | User need |
|------|-----------|
| Dashboard | See if the platform is healthy and which plugins loaded |
| Notifications | Send and track the core product action |
| Templates | Maintain message copy with preview |
| Campaigns | Guided batch send for marketers |
| Workflows | Multi-step journeys |
| Segments / Topics / Devices | Audience building blocks |
| Preferences / Consents | Compliance controls |
| Webhooks / Engagement | Integrations & analytics |
| Plugins | Extension surface |
| Settings | Connect to Host securely |

Lists use **DataTable** (sort, filter, pagination). Actions show live **API responses** with toasts.
