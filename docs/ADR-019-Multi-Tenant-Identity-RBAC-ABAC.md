# ADR-019: Multi-Tenant Identity, Authentication, RBAC & ABAC (B2B IaaS)

## Status

Proposed

## Date

2026-08-30

## Context

NotificationHub is sold as **IaaS / B2B SaaS** to companies (organizations).

Each customer organization must:

- Own its own data boundary (tenant isolation)
- Manage its own users (invite, roles, suspend, revoke)
- Authenticate humans via OIDC (Admin Panel)
- Authorize fine-grained actions (RBAC + ABAC)
- Be ready for future **Pay-As-You-Go** entitlements and **BNPL** domain features

Today the platform has:

- **API Key authentication** for machine/service clients (send notification, campaigns, …)
- Simple roles on API keys (`Admin` / `Sender` / `Reader`)
- Optional `TenantId` on domain entities and request context
- Placeholder `OidcOptions` (not wired)
- Admin UI (`apps/admin`) with localStorage token stubs only

**API Key must remain unchanged.** It is the machine-to-machine credential for the notification API.
Human admin identity is a separate concern.

### Actors

| Actor | Auth mechanism | Purpose |
|-------|----------------|---------|
| Machine / Integration | API Key (`X-Api-Key`) | Send notifications, campaigns, webhooks, … |
| Human Admin (customer) | OIDC + JWT (IdentityServer) | Manage org, users, templates, campaigns, billing later |
| Platform operator | OIDC + Platform roles | Cross-tenant support / compliance |
| Internal services | Client Credentials | Service-to-service |

### Goals

1. First-class **Organization (Tenant)** model
2. **User ↔ Organization membership** (many-to-many)
3. OIDC authentication via IdentityServer
4. RBAC (roles + permissions) scoped to organization
5. ABAC (contextual policies) for high-risk operations
6. Deny-by-default, fail-closed, backend as sole security boundary
7. Admin Panel contract-compatible with backend (OpenAPI-driven)
8. Ready for Entitlements (Pay-As-You-Go) and BNPL permissions later

### Non-Goals (this ADR)

- Replacing or modifying API Key auth
- Full billing / metering implementation
- Full BNPL domain
- External policy engine (OPA / OpenFGA) in v1
- Organization hierarchy deeper than Platform → Organization (defer workspace hierarchy)

---

## Decision

### 1. Architectural principle

Identity and Authorization are a **platform capability**, not an Admin Panel feature.

```text
Admin UI (Next.js)
      │  OIDC / OAuth2 (Auth Code + PKCE)
      ▼
IdentityServer
  (authentication, token issuance, session, MFA readiness)
      │  Access Token (JWT)
      ▼
Backend API (NotificationHub.Host)
  (token validation, tenant context, RBAC, ABAC, entitlements, domain rules)
      │
      ├── Identity / Membership store
      ├── Domain data (tenant-scoped)
      └── Redis (revocation / session cache only)
```

- IdentityServer proves **who** the actor is.
- Backend decides **what** the actor may do.
- Tenant boundary determines **where**.
- API Key path stays independent for machine clients.

### 2. Organization = Tenant

In this product, **Organization** is the commercial and isolation unit (the customer company).

```text
Platform
 └── Organization (Tenant)
      ├── Memberships (Users)
      ├── Roles / Permissions
      ├── API Keys (existing — unchanged)
      ├── Domain data (notifications, templates, campaigns, …)
      ├── Subscription / Entitlements (future)
      └── Security policy
```

Every tenant-owned entity implements a tenant boundary (strongly typed `OrganizationId` / `TenantId`).

### 3. User–Organization relationship

Not 1:1. Use membership:

```text
User
 └── OrganizationMembership
      ├── OrganizationId
      ├── UserId
      ├── Status: Invited | Pending | Active | Suspended | Revoked
      ├── Roles
      ├── JoinedAt / RevokedAt
```

One user may belong to multiple organizations with different roles.

### 4. Authentication (humans)

- IdentityServer as OIDC + OAuth Authorization Server
- Admin UI: Authorization Code Flow + PKCE
- No Resource Owner Password Credentials for the browser app
- Separate: Identity Token / Access Token / Refresh Token
- Access token carries minimal claims:

```text
sub, client_id, tenant_id (active org), scope, role (or role names),
jti, iat, exp, iss, aud, amr
```

Do **not** embed large permission lists in JWT. Resolve permissions server-side.

### 5. Tenant (Organization) context

Active organization is explicit in the access token (`tenant_id`).

Tenant switch:

1. Verify active membership in target org
2. Verify org is active
3. Issue new access token with new `tenant_id`
4. Audit the switch

Never trust raw `X-Tenant-Id` alone for human sessions.

Canonical abstractions:

```csharp
ITenantContext   // OrganizationId, UserId, MembershipId
ISecurityContext // roles, permissions, MFA, platform flag — immutable, server-derived
```

### 6. RBAC

Business roles (examples):

```text
PlatformAdmin          // platform operators only
OrganizationOwner
OrganizationAdmin
FinanceManager         // future billing / BNPL
NotificationOperator   // send / campaigns
SupportAgent
Auditor
Viewer
```

Permissions follow `resource.action`:

```text
notification.read | notification.send
template.read | template.write | template.delete
campaign.read | campaign.create | campaign.start | campaign.cancel
member.invite | member.role.assign | member.suspend
audit.read
organization.read | organization.update
// future
bnpl.application.approve | settlement.execute | …
```

Mapping: Role → RolePermission → Permission (data-driven).
Roles are **scoped** (Platform vs Organization). OrganizationAdmin never becomes PlatformAdmin by accident.

### 7. ABAC

RBAC = baseline capability. ABAC = contextual restriction.

Example:

```text
Permission: campaign.start
ABAC: membership active AND org active AND (optional) entitlement campaigns.enabled
```

High-risk future ops (refund-style / limit override) use handlers that check amount, MFA, time, risk — without exploding role count.

Start with ASP.NET Core `IAuthorizationService` + policy provider + handlers. External engines only if policies become non-developer-managed.

### 8. Authorization pipeline (human path)

```text
HTTP Request
 → Authentication (JWT Bearer)
 → Token validation (iss, aud, exp, signature)
 → Tenant resolution from token
 → Membership validation (active)
 → Permission evaluation
 → Entitlement (when introduced)
 → Resource scope (tenant isolation)
 → ABAC / domain rules
 → Handler
```

Deny by default. Fail closed if membership/policy store unavailable.

### 9. Dual auth on Host

| Path | Mechanism |
|------|-----------|
| Machine API (`/api/v1/notifications`, …) | Existing API Key middleware — **unchanged** |
| Human Admin API (`/api/v1/auth/*`, `/api/v1/organizations/*`, members, …) | JWT Bearer |
| Some admin read endpoints | JWT only (or explicit policy) |

API Key roles stay as today for machine clients. Human RBAC is a separate permission space (or carefully mapped later if needed).

### 10. Sessions & tokens

- Short-lived access tokens (e.g. 5–15 min)
- Refresh token rotation + server-side session
- Store only hashed refresh tokens
- Logout revokes session + refresh family
- Optional JTI revocation list in Redis for immediate access-token kill

### 11. Admin Panel rules

- UI hides/disables actions based on permissions from `GET /auth/me` (or equivalent)
- UI is **never** the security boundary
- Routes and DTOs follow OpenAPI; no invented contracts
- Prefer BFF / HttpOnly session for production Admin UI when feasible

### 12. Data model (minimum)

```text
Users
Organizations
OrganizationMemberships
Roles
Permissions
RolePermissions
MembershipRoles
UserSessions
RefreshTokens (hashed)
SecurityEvents / AuditEvents
Invitations
OrganizationEntitlements (phase later)
```

Soft-delete / status for identity records; financial/domain history must not cascade-delete with users.

### 13. Implementation order

See backlog document: [docs/backlog/multi-tenant-identity.md](backlog/multi-tenant-identity.md).

Backend first, then `apps/admin`.

### 14. Explicit constraints

1. **Do not modify API Key authentication or its roles.**
2. Do not put domain/payment rules into IdentityServer.
3. Do not put large permission sets into JWTs.
4. Do not trust client-supplied organization id without membership check.
5. Do not use frontend guards as authorization.
6. Keep identity, authorization, entitlement, billing, and domain modules separate.

---

## Alternatives considered

| Alternative | Why not |
|-------------|---------|
| Extend API Key to human admin login | Wrong tool; no SSO, MFA, session, org switching |
| Single shared “admin password” per org | Insecure; no per-user audit |
| Put all permissions in JWT | Token size, staleness, hard to revoke |
| Trust `X-Tenant-Id` header only | Spoofing / IDOR risk |
| OPA/OpenFGA from day one | Premature; ASP.NET policies sufficient for v1 |
| One user = one organization | Blocks partners / consultants / multi-org operators |

---

## Consequences

### Positive

- Clear separation: machine (API Key) vs human (OIDC)
- Sellable multi-org product with delegated user management
- Path to entitlements and BNPL without redesigning auth
- Auditable membership and authorization decisions

### Negative / costs

- New IdentityServer (or Duende / OpenIddict) host and operational complexity
- Schema and migrations for identity tables
- Dual authentication paths on the API host
- Admin UI rewrite of session/auth flow

### Risks

- Stale JWT permissions → mitigate with short TTL + server-side permission checks
- Accidental cross-org access → mandatory membership + resource tenant checks + regression tests
- Confusion between API Key roles and human permissions → document and keep namespaces separate

---

## References

- Skill: Multi-Tenant Identity, Authentication, RBAC & ABAC (design source)
- ADR-008 Domain UoW and Tenant Partition
- Existing `OidcOptions`, `IRequestContext`, `ApiKeyAuthMiddleware` (leave API Key intact)
- OpenID Connect / OAuth 2.1 best practices
