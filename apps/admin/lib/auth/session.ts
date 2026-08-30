const ACCESS_TOKEN_KEY = 'notificationhub.accessToken'
const REFRESH_TOKEN_KEY = 'notificationhub.refreshToken'
const TENANT_ID_KEY = 'notificationhub.tenantId'
const PKCE_VERIFIER_KEY = 'notificationhub.pkceVerifier'
const OIDC_STATE_KEY = 'notificationhub.oidcState'

export function getAccessToken(): string | undefined {
  if (typeof window === 'undefined') return undefined
  return window.localStorage.getItem(ACCESS_TOKEN_KEY) ?? undefined
}

export function getRefreshToken(): string | undefined {
  if (typeof window === 'undefined') return undefined
  return window.localStorage.getItem(REFRESH_TOKEN_KEY) ?? undefined
}

export function getTenantId(): string | undefined {
  if (typeof window === 'undefined') return undefined
  return window.localStorage.getItem(TENANT_ID_KEY) ?? undefined
}

export function setSession(input: {
  accessToken?: string
  refreshToken?: string
  tenantId?: string
}) {
  if (typeof window === 'undefined') return
  if (input.accessToken) window.localStorage.setItem(ACCESS_TOKEN_KEY, input.accessToken)
  if (input.refreshToken) window.localStorage.setItem(REFRESH_TOKEN_KEY, input.refreshToken)
  if (input.tenantId) window.localStorage.setItem(TENANT_ID_KEY, input.tenantId)
}

export function clearSession() {
  if (typeof window === 'undefined') return
  window.localStorage.removeItem(ACCESS_TOKEN_KEY)
  window.localStorage.removeItem(REFRESH_TOKEN_KEY)
  window.localStorage.removeItem(TENANT_ID_KEY)
  window.localStorage.removeItem(PKCE_VERIFIER_KEY)
  window.localStorage.removeItem(OIDC_STATE_KEY)
}

export function setPkce(verifier: string, state: string) {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PKCE_VERIFIER_KEY, verifier)
  window.localStorage.setItem(OIDC_STATE_KEY, state)
}

export function consumePkce(): { verifier?: string; state?: string } {
  if (typeof window === 'undefined') return {}
  const verifier = window.localStorage.getItem(PKCE_VERIFIER_KEY) ?? undefined
  const state = window.localStorage.getItem(OIDC_STATE_KEY) ?? undefined
  window.localStorage.removeItem(PKCE_VERIFIER_KEY)
  window.localStorage.removeItem(OIDC_STATE_KEY)
  return { verifier, state }
}
