/**
 * Session storage strategy (SPA, pre-BFF):
 * - Access token: in-memory only (not durable across full page reloads until refresh).
 * - Refresh token + tenant + PKCE: sessionStorage (tab-scoped, not shared across tabs/origins as freely as localStorage).
 * - Auth presence cookie (non-secret): lets edge middleware redirect unauthenticated users.
 *
 * Production target: BFF with httpOnly Secure SameSite cookies.
 */

const REFRESH_TOKEN_KEY = 'nh.rt'
const TENANT_ID_KEY = 'nh.tid'
const PKCE_VERIFIER_KEY = 'nh.pkce'
const OIDC_STATE_KEY = 'nh.oidc_state'
const AUTH_MARKER = 'nh_auth'

let memoryAccessToken: string | undefined

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
    document.cookie = `${AUTH_MARKER}=1; Path=/; SameSite=Lax${secure}`
  } else {
    document.cookie = `${AUTH_MARKER}=; Path=/; Max-Age=0; SameSite=Lax${secure}`
  }
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
  if (input.accessToken) {
    memoryAccessToken = input.accessToken
    setAuthMarker(true)
  }
  const store = ss()
  if (!store) return
  if (input.refreshToken) store.setItem(REFRESH_TOKEN_KEY, input.refreshToken)
  if (input.tenantId) store.setItem(TENANT_ID_KEY, input.tenantId)
}

export function clearSession() {
  memoryAccessToken = undefined
  setAuthMarker(false)
  const store = ss()
  if (!store) return
  store.removeItem(REFRESH_TOKEN_KEY)
  store.removeItem(TENANT_ID_KEY)
  store.removeItem(PKCE_VERIFIER_KEY)
  store.removeItem(OIDC_STATE_KEY)
  // purge legacy localStorage keys if present
  try {
    window.localStorage.removeItem('notificationhub.accessToken')
    window.localStorage.removeItem('notificationhub.refreshToken')
    window.localStorage.removeItem('notificationhub.tenantId')
    window.localStorage.removeItem('notificationhub.pkceVerifier')
    window.localStorage.removeItem('notificationhub.oidcState')
  } catch {
    /* ignore */
  }
}

export function setPkce(verifier: string, state: string) {
  const store = ss()
  if (!store) return
  store.setItem(PKCE_VERIFIER_KEY, verifier)
  store.setItem(OIDC_STATE_KEY, state)
}

export function consumePkce(): { verifier?: string; state?: string } {
  const store = ss()
  if (!store) return {}
  const verifier = store.getItem(PKCE_VERIFIER_KEY) ?? undefined
  const state = store.getItem(OIDC_STATE_KEY) ?? undefined
  store.removeItem(PKCE_VERIFIER_KEY)
  store.removeItem(OIDC_STATE_KEY)
  return { verifier, state }
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

export function tenantFromAccessToken(accessToken?: string): string | undefined {
  if (!accessToken) return undefined
  try {
    const part = accessToken.split('.')[1]
    if (!part) return undefined
    const json = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')))
    return (json.tenant_id || json.organization_id || json.tid) as string | undefined
  } catch {
    return undefined
  }
}
