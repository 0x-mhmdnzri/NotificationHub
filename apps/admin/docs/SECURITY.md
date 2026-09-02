# Admin UI security posture

## Current model
- SPA talks to API with Bearer access tokens (memory) + refresh token (sessionStorage).
- Next.js middleware `nh_auth` cookie is a **UX gate only**, not authorization.
- Server API enforces JWT + RBAC/ABAC.

## Mitigations applied
- Access token not persisted to web storage
- CSP without `unsafe-eval` in production; HSTS in production
- OIDC SPA client removed (single password/API auth path)
- Login/register/refresh rate-limited server-side
- Webhook URLs validated client (UX) and server (DNS + private range deny)
- Client RBAC is progressive disclosure only

## Target: BFF
Move session to httpOnly Secure SameSite cookies via Next.js Route Handlers proxying the API, eliminating browser-held refresh tokens (closes residual XSS session-theft risk).
