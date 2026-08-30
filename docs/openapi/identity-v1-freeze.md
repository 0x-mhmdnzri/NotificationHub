# OpenAPI freeze — Identity human surface (Sprint 4)

Contract for Admin UI (`apps/admin`). Machine API Key routes are **out of scope**.

Base path: `/api/v1`
Auth: `Authorization: Bearer <access_token>` (OIDC from Identity host)

## Auth

| Method | Path | Response highlights |
|--------|------|---------------------|
| GET | `/auth/me` | `{ user, tenant, membershipId, roles, permissions }` |
| GET | `/auth/organizations` | `[{ membershipId, organizationId, name, organizationStatus, membershipStatus, roles }]` |
| POST | `/auth/organizations/switch` | body `{ organizationId }` → `{ organizationId, membershipId, roles, permissions }` |
| POST | `/auth/logout` | 204 |
| POST | `/auth/invitations` | body `{ email, roleName?, organizationId? }` → 201 `{ id }` |
| POST | `/auth/invitations/accept` | body `{ token }` → 204 |
| GET | `/auth/sessions` | `[{ id, organizationId, clientId, ip, userAgent, createdAt, lastSeenAt, expiresAt, isActive }]` |
| DELETE | `/auth/sessions/{sessionId}` | 204 |
| POST | `/auth/sessions/revoke-all` | 204 |

## Organizations

| Method | Path | Permission |
|--------|------|------------|
| POST | `/organizations` | PlatformAdmin |
| GET | `/organizations/{id}` | organization.read |
| PATCH | `/organizations/{id}` | organization.update |
| GET | `/organizations/{id}/members` | member.read |
| POST | `/organizations/{id}/members/{mid}/roles` | member.role.assign |
| DELETE | `/organizations/{id}/members/{mid}/roles/{roleName}` | member.role.assign |
| POST | `/organizations/{id}/members/{mid}/status` | member.suspend |

## Errors

- `401` unauthenticated
- `403` `{ "error": "forbidden" | "membership_inactive_or_missing" | ... }`
- `429` rate limited on sensitive auth routes

## Token claims (Identity host)

`sub`, `tenant_id`, `client_id`, `role` (minimal), `jti`, `iss`, `aud`, `exp`

Permissions are **not** embedded in access token; loaded server-side via membership.

## Status

**Frozen for Admin UI Epic B** — additive changes only; breaking changes require ADR amendment.
