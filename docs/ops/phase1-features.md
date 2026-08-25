# Phase 1 features (F01–F06)

| ID | Module | API highlights |
|----|--------|----------------|
| F01 | Inbox | `GET/POST /api/v1/inbox/{userId}`, read/archive, SSE `/stream` |
| F02 | Digest | policies, buffer, admin flush; background `DigestFlushWorker` |
| F03 | Throttle | policies; enforced on `POST /api/v1/notifications` |
| F04 | Topics | CRUD-ish topics, subscribe, broadcast fan-out |
| F05 | Devices | register/list/unregister (`apns|fcm|webpush|expo`) |
| F06 | Activity | `GET /api/v1/admin/activity` |

Schema: `Phase1Schema.EnsureAsync` after Migrate (Postgres IF NOT EXISTS).
