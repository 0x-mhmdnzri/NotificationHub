import { apiUrl } from './config'
import { ApiError, type ProblemDetails } from './errors'
import {
  getAccessToken,
  getRefreshToken,
  getTenantId,
  setSession,
  clearSession,
} from '@/lib/auth/session'

export interface ApiRequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
  tenantId?: string
  accessToken?: string
  /** Skip Authorization header (login/register/refresh) */
  anonymous?: boolean
  _retried?: boolean
}

async function parseResponse(response: Response): Promise<unknown> {
  if (response.status === 204) return undefined
  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('application/json')) {
    try {
      return await response.json()
    } catch {
      return undefined
    }
  }
  const text = await response.text()
  return text || undefined
}

function errorMessage(payload: unknown, status: number): string {
  if (payload && typeof payload === 'object') {
    const o = payload as Record<string, unknown>
    if (typeof o.error === 'string') return o.error
    if (typeof o.detail === 'string') return o.detail
    if (typeof o.title === 'string') return o.title
    if (typeof o.message === 'string') return o.message
  }
  return `Request failed with status ${status}`
}

async function tryRefresh(): Promise<string | undefined> {
  const rt = getRefreshToken()
  if (!rt) return undefined
  try {
    const response = await fetch(apiUrl('/api/v1/auth/refresh'), {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: rt }),
      credentials: 'omit',
      cache: 'no-store',
    })
    if (!response.ok) {
      clearSession()
      return undefined
    }
    const data = (await response.json()) as {
      accessToken?: string
      refreshToken?: string
      AccessToken?: string
      RefreshToken?: string
    }
    const accessToken = data.accessToken ?? data.AccessToken
    const refreshToken = data.refreshToken ?? data.RefreshToken
    if (!accessToken) {
      clearSession()
      return undefined
    }
    setSession({ accessToken, refreshToken: refreshToken ?? rt })
    return accessToken
  } catch {
    clearSession()
    return undefined
  }
}

export async function request<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')

  if (options.body !== undefined && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  const tenantId = options.tenantId ?? getTenantId()
  let accessToken = options.anonymous ? undefined : (options.accessToken ?? getAccessToken())

  if (tenantId) headers.set('X-Tenant-Id', tenantId)
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  const response = await fetch(apiUrl(path), {
    ...options,
    body:
      options.body === undefined
        ? undefined
        : options.body instanceof FormData
          ? options.body
          : JSON.stringify(options.body),
    headers,
    credentials: 'omit',
    cache: 'no-store',
  })

  if (response.status === 401 && !options._retried && !options.anonymous) {
    const next = await tryRefresh()
    if (next) {
      return request<T>(path, { ...options, accessToken: next, _retried: true })
    }
    clearSession()
    if (typeof window !== 'undefined' && !path.includes('/auth/login') && !path.includes('/auth/refresh')) {
      const nextPath = window.location.pathname + window.location.search
      if (!window.location.pathname.startsWith('/login')) {
        window.location.href = `/login?next=${encodeURIComponent(nextPath)}`
      }
    }
  }

  const payload = await parseResponse(response)

  if (!response.ok) {
    const details =
      typeof payload === 'object' && payload !== null ? (payload as ProblemDetails) : undefined
    throw new ApiError(errorMessage(payload, response.status), response.status, details)
  }

  return normalizeTokens(payload) as T
}

/** Accept both camelCase and PascalCase token payloads from API. */
function normalizeTokens(payload: unknown): unknown {
  if (!payload || typeof payload !== 'object') return payload
  const o = payload as Record<string, unknown>
  if ('AccessToken' in o || 'accessToken' in o) {
    return {
      ...o,
      accessToken: (o.accessToken ?? o.AccessToken) as string,
      refreshToken: (o.refreshToken ?? o.RefreshToken) as string | undefined,
      expiresIn: (o.expiresIn ?? o.ExpiresIn) as number | undefined,
      tokenType: (o.tokenType ?? o.TokenType) as string | undefined,
      organizationId: (o.organizationId ?? o.OrganizationId) as string | undefined,
    }
  }
  return payload
}

export const api = {
  get: <T>(path: string, options?: Omit<ApiRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'GET' }),

  post: <T>(path: string, body?: unknown, options?: Omit<ApiRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'POST', body }),

  put: <T>(path: string, body?: unknown, options?: Omit<ApiRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'PUT', body }),

  patch: <T>(path: string, body?: unknown, options?: Omit<ApiRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'PATCH', body }),

  delete: <T>(path: string, options?: Omit<ApiRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'DELETE' }),
}
