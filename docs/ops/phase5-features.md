# Phase 5 — Production readiness

| ID | Feature | Notes |
|----|---------|-------|
| F31 | Digest real send | FlushDue enqueues via Accept+Outbox when orch/queue available |
| F32 | Inbox multi-instance | `IInboxEventBus` — Redis when `ConnectionStrings:Redis` set |
| F33 | Broadcast audience | SegmentKey expands CDP profile emails |
| F34 | SES SigV4 | AWS Signature Version 4 on SES v2 endpoint |
| F35 | Accept metrics | `notifications.accept` / `suppressed` / `process` |
