const ACCESS_TOKEN_KEY = 'notificationhub.accessToken'
const TENANT_ID_KEY = 'notificationhub.tenantId'

export function getAccessToken(): string | undefined {
  if (typeof window === 'undefined') return undefined
  return window.localStorage.getItem(ACCESS_TOKEN_KEY) ?? undefined
}

export function getTenantId(): string | undefined {
  if (typeof window === 'undefined') return undefined
  return window.localStorage.getItem(TENANT_ID_KEY) ?? undefined
}

export function setSession(input: { accessToken?: string; tenantId?: string }) {
  if (typeof window === 'undefined') return
  if (input.accessToken) window.localStorage.setItem(ACCESS_TOKEN_KEY, input.accessToken)
  if (input.tenantId) window.localStorage.setItem(TENANT_ID_KEY, input.tenantId)
}

export function clearSession() {
  if (typeof window === 'undefined') return
  window.localStorage.removeItem(ACCESS_TOKEN_KEY)
  window.localStorage.removeItem(TENANT_ID_KEY)
}
