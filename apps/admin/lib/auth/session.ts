/**
 * Session strategy (SPA until BFF lands):
 * - Access token: **memory only** (not sessionStorage) — reduces XSS token lifetime to tab JS heap
 * - Refresh token: sessionStorage (tab-scoped); still XSS-exfiltratable — tracked for BFF migration
 * - Auth marker cookie: non-secret presence flag for edge middleware UX only (not authorization)
 */

const REFRESH_TOKEN_KEY = 'nh.rt'
const TENANT_ID_KEY = 'nh.tid'
const AUTH_MARKER = 'nh_auth'

let memoryAccessToken: string | undefined
let authBootstrapLock = false

function ss(): Storage | null {
  if (typeof window === 'undefined') return null
  try {
    return window.sessionStorage
  } catch {
    return null
  }
}

function setAuthMarker(on: boolean) {
  if (typeof document === 'undefined') return
  const secure = typeof location !== 'undefined' && location.protocol === 'https:' ? '; Secure' : ''
  if (on) {
    // Marker is intentionally non-secret; middleware must never treat it as proof of identity.
    document.cookie = `${AUTH_MARKER}=1; Path=/; SameSite=Lax${secure}`
  } else {
    document.cookie = `${AUTH_MARKER}=; Path=/; Max-Age=0; SameSite=Lax${secure}`
  }
}

export function setAuthBootstrapLock(locked: boolean) {
  authBootstrapLock = locked
}

export function isAuthBootstrapLocked() {
  return authBootstrapLock
}

export function getAccessToken(): string | undefined {
  return memoryAccessToken
}

export function getRefreshToken(): string | undefined {
  return ss()?.getItem(REFRESH_TOKEN_KEY) ?? undefined
}

export function getTenantId(): string | undefined {
  return ss()?.getItem(TENANT_ID_KEY) ?? undefined
}

export function setSession(input: {
  accessToken?: string
  refreshToken?: string
  tenantId?: string
}) {
  const store = ss()
  if (input.accessToken) {
    memoryAccessToken = input.accessToken
    setAuthMarker(true)
  }
  if (input.refreshToken) store?.setItem(REFRESH_TOKEN_KEY, input.refreshToken)
  if (input.tenantId) store?.setItem(TENANT_ID_KEY, input.tenantId)
  // Purge any legacy access-token keys from storage
  try {
    store?.removeItem('nh.at')
    window.localStorage?.removeItem('notificationhub.accessToken')
  } catch {
    /* ignore */
  }
}

export function clearSession() {
  if (authBootstrapLock) return
  memoryAccessToken = undefined
  setAuthMarker(false)
  const store = ss()
  if (!store) return
  store.removeItem(REFRESH_TOKEN_KEY)
  store.removeItem(TENANT_ID_KEY)
  store.removeItem('nh.at')
  store.removeItem('nh.pkce')
  store.removeItem('nh.oidc_state')
  try {
    window.localStorage.removeItem('notificationhub.accessToken')
    window.localStorage.removeItem('notificationhub.refreshToken')
    window.localStorage.removeItem('notificationhub.tenantId')
    window.localStorage.removeItem('notificationhub.pkceVerifier')
    window.localStorage.removeItem('notificationhub.oidcState')
    window.sessionStorage.removeItem('notificationhub.returnTo')
  } catch {
    /* ignore */
  }
}

/** Only same-app relative paths; blocks open redirects. */
export function safeReturnPath(candidate?: string | null, fallback = '/dashboard'): string {
  if (!candidate) return fallback
  const t = candidate.trim()
  if (!t.startsWith('/') || t.startsWith('//')) return fallback
  if (t.includes('\\') || t.includes('@')) return fallback
  if (/^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(t)) return fallback
  return t
}

/**
 * @deprecated Prefer tenantId from login/register/switchOrganization API response or /auth/me.
 * Client-side JWT parse is unverified and must not drive authorization.
 */
export function tenantFromAccessToken(_accessToken?: string): string | undefined {
  return undefined
}

export function isPublicAuthPath(pathname?: string): boolean {
  const p = pathname ?? (typeof window !== 'undefined' ? window.location.pathname : '')
  return ['/login', '/auth/accept-invite'].some((x) => p === x || p.startsWith(`${x}/`))
}
