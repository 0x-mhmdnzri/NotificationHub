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

/**
 * Client-side UX guard only. Server re-validates with DNS resolution (WebhookUrlValidator).
 * Blocks obvious SSRF targets before the request is sent.
 */
export function isAllowedHttpsUrl(value: string): boolean {
  try {
    const u = new URL(value)
    if (u.protocol !== 'https:') return false
    if (u.username || u.password) return false
    const host = u.hostname.toLowerCase().replace(/^\[|\]$/g, '')

    if (host === 'localhost' || host === '127.0.0.1' || host === '::1' || host === '0.0.0.0')
      return false
    if (host.endsWith('.local') || host.endsWith('.internal') || host.endsWith('.localhost'))
      return false
    if (host === 'metadata.google.internal' || host === 'metadata.google') return false

    // IPv4 literal checks (incl. cloud metadata)
    const ipv4 = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/.exec(host)
    if (ipv4) {
      const o = ipv4.slice(1).map(Number)
      if (o.some((n) => n > 255)) return false
      if (o[0] === 10) return false
      if (o[0] === 127) return false
      if (o[0] === 0) return false
      if (o[0] === 169 && o[1] === 254) return false
      if (o[0] === 192 && o[1] === 168) return false
      if (o[0] === 172 && o[1] >= 16 && o[1] <= 31) return false
      if (o[0] === 100 && o[1] >= 64 && o[1] <= 127) return false
      if (o[0] >= 224) return false
    }

    // IPv6 unique-local / link-local prefixes
    if (host.includes(':')) {
      if (host.startsWith('fc') || host.startsWith('fd') || host.startsWith('fe80')) return false
      if (host === '::' || host === '::1') return false
    }

    return true
  } catch {
    return false
  }
}
