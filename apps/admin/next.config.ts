import type { NextConfig } from 'next'

const isProd = process.env.NODE_ENV === 'production'

const csp = [
  "default-src 'self'",
  "base-uri 'self'",
  "frame-ancestors 'none'",
  "form-action 'self'",
  "object-src 'none'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  "style-src 'self' 'unsafe-inline'",
  // Next.js requires unsafe-inline/eval in some builds; tighten further with nonces when BFF lands.
  "script-src 'self' 'unsafe-inline' 'unsafe-eval'",
  "connect-src 'self' " +
    [process.env.NEXT_PUBLIC_API_BASE_URL, process.env.NEXT_PUBLIC_IDENTITY_AUTHORITY]
      .filter(Boolean)
      .map((u) => {
        try {
          return new URL(u as string).origin
        } catch {
          return ''
        }
      })
      .filter(Boolean)
      .join(' ') +
    (isProd ? '' : ' http://localhost:* http://127.0.0.1:*'),
  'upgrade-insecure-requests',
]
  .join('; ')
  .replace(/\s+/g, ' ')
  .trim()

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  productionBrowserSourceMaps: false,
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'Content-Security-Policy', value: csp },
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'X-Frame-Options', value: 'DENY' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
          {
            key: 'Permissions-Policy',
            value: 'camera=(), microphone=(), geolocation=(), payment=(), usb=()',
          },
          { key: 'X-DNS-Prefetch-Control', value: 'off' },
          ...(isProd
            ? [{ key: 'Strict-Transport-Security', value: 'max-age=63072000; includeSubDomains; preload' }]
            : []),
        ],
      },
    ]
  },
}

export default nextConfig
