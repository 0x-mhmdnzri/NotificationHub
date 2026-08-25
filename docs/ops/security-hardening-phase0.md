# Phase 0 security (SEC-20 … SEC-30)

| ID | Change |
|----|--------|
| SEC-20 | List engagements requires notification + `CanAccessTenant` |
| SEC-21 | POST engagements requires existing notification + tenant |
| SEC-22 | `/t/*` tracking only persists when notification exists |
| SEC-23 | Auth rate limit only on missing/invalid API key (`auth-fail:ip:`) |
| SEC-24 | Optional Redis `IRateLimiter` when `ConnectionStrings:Redis` set |
| SEC-25 | ForwardedHeaders only trusts `ForwardedHeaders:KnownProxies` |
| SEC-26 | Kestrel MaxRequestBodySize = 2 MB |
| SEC-27 | Swagger/OpenAPI unauthenticated only in Development |
| SEC-28 | `/health` returns `{status:ok}`; `/health/ready` for DB |
| SEC-29 | Slack webhook https + no private/loopback; 10s timeout |
| SEC-30 | Base `appsettings.json` has empty secrets; dev file holds local defaults |

Tests: `tests/NotificationHub.Core.Tests/Engagement`, `RateLimiting`, `Security/SlackWebhookSafetyTests.cs`
Cases: `tests/docs/phase0-security-test-cases.md`
