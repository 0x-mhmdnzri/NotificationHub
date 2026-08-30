# Admin UI security posture

## Fixed in `sec/admin-nextjs-harden`

| Issue | Mitigation |
|-------|------------|
| #28 Tokens in localStorage | Access token in memory; refresh/PKCE in sessionStorage; purge legacy keys |
| #29 Missing headers | CSP, HSTS (prod), XFO DENY, nosniff, Referrer-Policy, Permissions-Policy |
| #30 Open redirect | `safeReturnPath` allows only same-app relative paths |
| #31 Client-only auth shell | `middleware.ts` + non-secret `nh_auth` marker |
| #32 API path injection | `apiUrl` rejects absolute URLs; webhook HTTPS + private-IP block |
| #33 Insecure defaults | `poweredByHeader: false`, no prod source maps, hardened env example |

## Residual risk

SPA cannot fully protect refresh tokens from XSS. Target architecture: Next.js BFF with httpOnly Secure SameSite cookies and server-side token exchange.
