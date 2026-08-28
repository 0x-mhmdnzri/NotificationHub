# ADR 0016: Result Pattern for Expected Outcomes

## Status
Accepted

## Context
Business failures (not found, validation, preference deny, invalid state) must not be modeled as exceptions. Unexpected failures (DB down, broker down, bugs) remain exceptions.

## Decision

### Semantics
| Kind | Mechanism |
|------|-----------|
| Expected business outcome | `Result` / `Result<T>` |
| Unexpected / infrastructure | Exception → global handler / resilience |

### Shape
- `Result` / `Result<T>` with enforced invariants (no success+error, no failure without error)
- Multi-error support for validation lists
- Composition: `Map`, `Bind`, `Match`, `Ensure`, `Tap`
- Stable error codes (`notification.not_found`, …) + `ErrorType` taxonomy
- Catalogs: `Errors`, `NotificationErrors`, `CampaignErrors`

### HTTP boundary only
`ResultHttpExtensions.ToHttpResult` → RFC 7807 `ProblemDetails` (`application/problem+json`) with `code` + `errors[]`.

Mapping:
| ErrorType | HTTP |
|-----------|------|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| RateLimited | 429 |
| BusinessRule / Failure | 422 |

### Validation pipeline
`ValidationBehavior` returns `Result.Failure(errors)` when `TResponse` is `Result`/`Result<T>`; otherwise throws `ValidationException`.

### Non-goals
- Result is not a transaction, retry, or idempotency framework
- Domain does not reference HTTP / ProblemDetails
- Do not swallow infrastructure exceptions into Result

## Consequences
+ Explicit, typed control flow for expected failures
+ Stable machine-readable API error codes
- Handlers must return Result for business branches (not throw)
