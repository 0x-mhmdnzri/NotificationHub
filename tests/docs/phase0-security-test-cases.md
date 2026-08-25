# Phase 0 Security Test Cases

| ID | Requirement | Priority | Type |
|----|-------------|----------|------|
| TC_SEC_020 | List engagements only for known notification (service) | High | Functional |
| TC_SEC_021 | Track rejects missing notification when required | High | Error |
| TC_SEC_022 | Track persists when notification exists; enrich tenant | High | Functional |
| TC_E_SEC_022 | Optional bypass when requireExisting=false | Low | Edge |
| TC_F_RL_001 | Rate limiter allows up to limit | High | Functional |
| TC_E_RL_002 | Rate limiter blocks over limit | High | Edge |
| TC_F_RL_003 | Independent keys | Medium | Functional |
| TC_SEC_029 | Slack webhook safety matrix | High | Security |

API-layer tenant checks (SEC-20/21) are enforced in Program.cs (`CanAccessTenant`); covered by service existence tests + code review. Integration host tests can be added when WebApplicationFactory is introduced.
