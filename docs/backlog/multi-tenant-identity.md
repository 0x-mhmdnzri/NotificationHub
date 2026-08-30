# Backlog: Multi-Tenant Identity (B2B IaaS)

Source: [ADR-019](../ADR-019-Multi-Tenant-Identity-RBAC-ABAC.md)

**Constraint:** API Key authentication remains untouched (machine clients).

**Order:** Backend complete before Admin UI (`apps/admin`).

---

## Epic A — Foundation (Backend)

### A1. Domain model & persistence
| ID | Item | Priority | Notes |
|----|------|----------|-------|
| A1.1 | `Organization` entity (Id, Name, Status, Type, CreatedAt, SecurityPolicy ref) | P0 | Tenant = Organization |
| A1.2 | `User` entity (Id, Email, DisplayName, Status, CreatedAt) | P0 | Email is attribute, not immutable id |
| A1.3 | `OrganizationMembership` (OrgId, UserId, Status lifecycle, timestamps) | P0 | Invited→Pending→Active→Suspended→Revoked |
| A1.4 | `Role`, `Permission`, `RolePermission`, `MembershipRole` | P0 | Data-driven mapping |
| A1.5 | `Invitation` (token hash, expires, single-use) | P0 | |
| A1.6 | EF configurations + migration(s) | P0 | No cascade-delete of domain history |
| A1.7 | Seed default permissions + system roles | P0 | |

### A2. Identity host / token issuer
| ID | Item | Priority | Notes |
|----|------|----------|-------|
| A2.1 | Choose stack: Duende IdentityServer **or** OpenIddict (document choice in ADR amendment) | P0 | |
| A2.2 | Identity host project (or AppHost service) | P0 | Separate process preferred |
| A2.3 | OIDC clients: `admin-ui` (Auth Code + PKCE), optional `admin-bff` | P0 | |
| A2.4 | Scopes: `openid`, `profile`, `email`, `notificationhub.admin` | P0 | |
| A2.5 | Access token claims: `sub`, `tenant_id`, `client_id`, roles (minimal), `jti`, standard OIDC | P0 | No fat permission arrays |
| A2.6 | Refresh token rotation + hashed storage | P0 | |
| A2.7 | Signing key config + rotation readiness (JWKS) | P1 | |
| A2.8 | MFA readiness hooks (policy-driven, not hard-coded) | P2 | |

### A3. API authentication & context (Host)
| ID | Item | Priority | Notes |
|----|------|----------|-------|
| A3.1 | JWT Bearer authentication scheme **alongside** existing API Key middleware | P0 | Do not change API Key |
| A3.2 | Audience / issuer validation | P0 | |
| A3.3 | `ITenantContext` + `ISecurityContext` (immutable, server-derived) | P0 | |
| A3.4 | Membership validation on every human request | P0 | Fail closed |
| A3.5 | Organization switch endpoint → new token with new `tenant_id` | P0 | |
| A3.6 | Map human principal into `IRequestContext` without breaking API Key path | P0 | |

### A4. RBAC / ABAC
| ID | Item | Priority | Notes |
|----|------|----------|-------|
| A4.1 | Permission catalog (`resource.action`) for current domain | P0 | notification, template, campaign, member, org, audit |
| A4.2 | Dynamic policy provider (permission name → policy) | P0 | |
| A4.3 | Authorization handlers: permission + same-org resource | P0 | |
| A4.4 | `[Authorize(Policy = "…")]` / endpoint filters for new admin routes | P0 | |
| A4.5 | ABAC sample: membership status + org status | P1 | |
| A4.6 | Deny-by-default + regression tests (cross-org IDOR) | P0 | |

### A5. Human admin API surface
| ID | Item | Priority | Notes |
|----|------|----------|-------|
| A5.1 | `GET /api/v1/auth/me` — user, active org, roles, permissions | P0 | Contract for Admin UI |
| A5.2 | `GET /api/v1/auth/organizations` — memberships | P0 | |
| A5.3 | `POST /api/v1/auth/organizations/switch` | P0 | |
| A5.4 | `POST /api/v1/auth/logout` — revoke session/refresh | P0 | |
| A5.5 | Organization CRUD (platform + owner scoped) | P0 | |
| A5.6 | Invite member / accept invitation | P0 | |
| A5.7 | Assign/remove roles on membership | P0 | |
| A5.8 | Suspend / reactivate / revoke membership | P0 | |
| A5.9 | List sessions / revoke session | P1 | |
| A5.10 | OpenAPI update for all new endpoints | P0 | |

### A6. Audit & security ops
| ID | Item | Priority | Notes |
|----|------|----------|-------|
| A6.1 | Security events: login, logout, switch org, invite, role change, deny | P0 | No secrets in logs |
| A6.2 | Append-only audit store (or reuse Core.Audit with clear category) | P1 | |
| A6.3 | Rate limit login / token / invite / password-reset | P1 | |
| A6.4 | Metrics: auth success/fail, authz deny, refresh reuse | P2 | |

### A7. Future readiness (schema only or stubs)
| ID | Item | Priority | Notes |
|----|------|----------|-------|
| A7.1 | `OrganizationEntitlement` table + check hook in auth pipeline | P2 | Pay-As-You-Go |
| A7.2 | Permission names reserved for BNPL (document only) | P2 | |
| A7.3 | Platform vs Organization role scope enforced | P0 | |

---

## Epic B — Admin UI (`apps/admin`) — **after** backend contracts stable

| ID | Item | Priority | Notes |
|----|------|----------|-------|
| B1 | OIDC login (Auth Code + PKCE) against Identity host | P0 | |
| B2 | Session handling (prefer BFF/HttpOnly if adopted; else documented SPA risks) | P0 | |
| B3 | Tenant selector after login (multi-org) | P0 | |
| B4 | Load `/auth/me` → permission context for UI | P0 | UX only |
| B5 | Route guards from permission metadata | P1 | |
| B6 | Members / invite / roles screens | P0 | |
| B7 | Organization settings screen | P1 | |
| B8 | Generated API client from OpenAPI | P0 | No hand-rolled DTOs |
| B9 | Logout + session revoke | P0 | |

---

## Epic C — Quality gates

| ID | Item | Priority |
|----|------|----------|
| C1 | Auth tests: invalid/expired token, wrong audience, wrong issuer | P0 |
| C2 | Tenant isolation: Org A cannot read Org B resources | P0 |
| C3 | RBAC: Viewer cannot mutate; OrgAdmin cannot escalate to PlatformAdmin | P0 |
| C4 | Membership lifecycle: suspended/revoked cannot act | P0 |
| C5 | Invitation: expired / reused rejected | P0 |
| C6 | API Key path regression: existing machine flows still pass | P0 |
| C7 | Contract tests Admin ↔ OpenAPI | P1 |

---

## Suggested sprint slices (backend-first)

### Sprint 1 — Model + Identity host skeleton
- A1.*, A2.1–A2.5, A3.1–A3.3

### Sprint 2 — Membership + switch + `/auth/me`
- A3.4–A3.6, A5.1–A5.4, A5.6, A6.1

### Sprint 3 — RBAC on admin APIs + org/member management
- A4.*, A5.5, A5.7–A5.8, A5.10, C1–C6

### Sprint 4 — Sessions, hardening, OpenAPI freeze
- A2.6–A2.7, A5.9, A6.2–A6.3, C7

### Sprint 5+ — Admin UI Epic B

### Later — Entitlements / BNPL (A7, domain features)

---

## Definition of Done (identity vertical)

- [ ] Humans authenticate via OIDC; machines still use API Key unchanged
- [ ] Organization + membership lifecycle works
- [ ] Active org in token; switch audited and membership-checked
- [ ] Permissions enforced on admin APIs (deny-by-default)
- [ ] Cross-org isolation tested
- [ ] `/auth/me` (or equivalent) drives Admin UI
- [ ] OpenAPI matches implementation
- [ ] Security events audited without leaking secrets
- [ ] Admin UI only after backend contracts are stable
