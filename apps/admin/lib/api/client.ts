import { apiUrl } from './config'
import { ApiError, type ProblemDetails } from './errors'
import { getAccessToken, getTenantId } from '@/lib/auth/session'
import { refreshAccessToken, logoutLocal } from '@/lib/auth/oidc'

export interface ApiRequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
  tenantId?: string
  accessToken?: string
  _retried?: boolean
}

async function parseResponse(response: Response): Promise<unknown> {
  if (response.status === 204) return undefined
  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('application/json')) {
    return response.json()
  }
  const text = await response.text()
  return text || undefined
}

export async function request<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')

  if (options.body !== undefined && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  const tenantId = options.tenantId ?? getTenantId()
  let accessToken = options.accessToken ?? getAccessToken()

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

  if (response.status === 401 && !options._retried) {
    const next = await refreshAccessToken()
    if (next) {
      return request<T>(path, { ...options, accessToken: next, _retried: true })
    }
    logoutLocal()
  }

  const payload = await parseResponse(response)

  if (!response.ok) {
    const details =
      typeof payload === 'object' && payload !== null ? (payload as ProblemDetails) : undefined
    throw new ApiError(
      details?.detail || details?.title || `Request failed with status ${response.status}`,
      response.status,
      details,
    )
  }

  return payload as T
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
