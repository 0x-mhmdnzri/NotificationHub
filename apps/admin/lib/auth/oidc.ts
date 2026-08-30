/**
 * OIDC Auth Code + PKCE client (SPA).
 * Tokens in localStorage — documented risk; prefer BFF in production hardening.
 */
import { setPkce, setSession, consumePkce, clearSession, getRefreshToken } from './session'

const authority = () => (process.env.NEXT_PUBLIC_IDENTITY_AUTHORITY ?? '').replace(/\/$/, '')
const clientId = () => process.env.NEXT_PUBLIC_OIDC_CLIENT_ID ?? 'admin-ui'
const redirectUri = () => process.env.NEXT_PUBLIC_OIDC_REDIRECT_URI ?? (typeof window !== 'undefined' ? `${window.location.origin}/auth/callback` : '')
const scopes = () => process.env.NEXT_PUBLIC_OIDC_SCOPES ?? 'openid profile email notificationhub.admin offline_access'

function randomString(len = 64) {
  const arr = new Uint8Array(len)
  crypto.getRandomValues(arr)
  return Array.from(arr, (b) => ('0' + b.toString(16)).slice(-2)).join('')
}

async function sha256Base64Url(input: string) {
  const data = new TextEncoder().encode(input)
  const hash = await crypto.subtle.digest('SHA-256', data)
  const bytes = new Uint8Array(hash)
  let str = ''
  bytes.forEach((b) => { str += String.fromCharCode(b) })
  return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

export async function beginLogin(returnTo?: string) {
  const verifier = randomString(64)
  const challenge = await sha256Base64Url(verifier)
  const state = randomString(24)
  if (returnTo) sessionStorage.setItem('notificationhub.returnTo', returnTo)
  setPkce(verifier, state)

  const url = new URL(`${authority()}/connect/authorize`)
  url.searchParams.set('client_id', clientId())
  url.searchParams.set('redirect_uri', redirectUri())
  url.searchParams.set('response_type', 'code')
  url.searchParams.set('scope', scopes())
  url.searchParams.set('state', state)
  url.searchParams.set('code_challenge', challenge)
  url.searchParams.set('code_challenge_method', 'S256')
  window.location.href = url.toString()
}

export async function handleCallback(search: string) {
  const params = new URLSearchParams(search)
  const code = params.get('code')
  const state = params.get('state')
  const err = params.get('error')
  if (err) throw new Error(params.get('error_description') || err)
  if (!code) throw new Error('missing_code')

  const { verifier, state: expected } = consumePkce()
  if (!verifier || !expected || expected !== state) throw new Error('invalid_state')

  const body = new URLSearchParams()
  body.set('grant_type', 'authorization_code')
  body.set('client_id', clientId())
  body.set('code', code)
  body.set('redirect_uri', redirectUri())
  body.set('code_verifier', verifier)

  const res = await fetch(`${authority()}/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  })
  if (!res.ok) throw new Error(`token_exchange_${res.status}`)
  const json = await res.json() as {
    access_token: string
    refresh_token?: string
    expires_in?: number
  }
  setSession({
    accessToken: json.access_token,
    refreshToken: json.refresh_token,
  })
  return json
}

export async function refreshAccessToken(): Promise<string | undefined> {
  const refresh = getRefreshToken()
  if (!refresh) return undefined
  const body = new URLSearchParams()
  body.set('grant_type', 'refresh_token')
  body.set('client_id', clientId())
  body.set('refresh_token', refresh)
  const res = await fetch(`${authority()}/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  })
  if (!res.ok) {
    clearSession()
    return undefined
  }
  const json = await res.json() as { access_token: string; refresh_token?: string }
  setSession({ accessToken: json.access_token, refreshToken: json.refresh_token ?? refresh })
  return json.access_token
}

export function logoutLocal() {
  clearSession()
}
