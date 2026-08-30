# Sprint 5+ — Admin UI (Epic B)

## Delivered

| ID | Item |
|----|------|
| B1 | OIDC Auth Code + PKCE (`lib/auth/oidc.ts`) |
| B2 | Session in localStorage (documented SPA risk) |
| B3 | Org selector in topbar from `/auth/organizations` |
| B4 | `AuthProvider` loads `/auth/me` + permissions |
| B5 | `RequireAuth` / `RequirePermission` |
| B6 | Members + invite + suspend (`/organization/members`) |
| B7 | Org settings (`/organization/settings`) |
| B8 | `lib/api/identity.ts` aligned to OpenAPI freeze |
| B9 | Logout + `/account/sessions` revoke |

## Env

See `apps/admin/.env.example`.

## Notes

- UI permissions are UX only; API enforces deny-by-default.
- After org switch, Identity host should re-issue token with new `tenant_id` (API returns membership confirmation).
- Shell still renders on `/login`; auth gate skips redirect loop via PUBLIC paths.
