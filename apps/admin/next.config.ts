import type { NextConfig } from 'next'

const isProd = process.env.NODE_ENV === 'production'

const connectOrigins = [
  process.env.NEXT_PUBLIC_API_BASE_URL,
]
  .filter(Boolean)
  .map((u) => {
    try {
      return new URL(u as string).origin
    } catch {
      return ''
    }
  })
  .filter(Boolean)

const cspParts = [
  "default-src 'self'",
  "base-uri 'self'",
  "frame-ancestors 'none'",
  "form-action 'self'",
  "object-src 'none'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  // Styles: Next/Tailwind still need inline in many builds
  "style-src 'self' 'unsafe-inline'",
  // Production: no unsafe-eval. Dev keeps it for Next HMR/tooling.
  isProd
    ? "script-src 'self' 'unsafe-inline'"
    : "script-src 'self' 'unsafe-inline' 'unsafe-eval'",
  "connect-src 'self' " +
    connectOrigins.join(' ') +
    (isProd ? '' : ' http://localhost:* http://127.0.0.1:* ws://localhost:* ws://127.0.0.1:*'),
]

if (isProd) {
  cspParts.push('upgrade-insecure-requests')
}

const csp = cspParts.join('; ').replace(/\s+/g, ' ').trim()

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
            ? [
                {
                  key: 'Strict-Transport-Security',
                  value: 'max-age=63072000; includeSubDomains; preload',
                },
              ]
            : []),
        ],
      },
    ]
  },
}

export default nextConfig
