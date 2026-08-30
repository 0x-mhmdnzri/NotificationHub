export const API_BASE_URL = (process.env.NEXT_PUBLIC_API_BASE_URL ?? '').replace(/\/$/, '')

/**
 * Build API URL. Rejects absolute external URLs in `path` to prevent client-side path injection.
 */
export function apiUrl(path: string): string {
  const p = path.trim()
  if (/^https?:\/\//i.test(p) || p.startsWith('//')) {
    throw new Error('absolute_url_not_allowed')
  }
  const normalized = p.startsWith('/') ? p : `/${p}`
  if (!API_BASE_URL) return normalized
  return `${API_BASE_URL}${normalized}`
}

/** Client-side guard for user-supplied callback/webhook endpoints. */
export function isAllowedHttpsUrl(value: string): boolean {
  try {
    const u = new URL(value)
    if (u.protocol !== 'https:') return false
    if (u.username || u.password) return false
    const host = u.hostname.toLowerCase()
    if (host === 'localhost' || host === '127.0.0.1' || host === '::1') return false
    if (host.endsWith('.local') || host.endsWith('.internal')) return false
    if (/^(10\.|192\.168\.|172\.(1[6-9]|2\d|3[0-1])\.)/.test(host)) return false
    return true
  } catch {
    return false
  }
}
