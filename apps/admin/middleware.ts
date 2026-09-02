import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'

/**
 * UX gate only. Cookie `nh_auth` is a forgeable presence marker set by the SPA.
 * Authorization is enforced exclusively by the API via JWT Bearer validation.
 * Do not treat this middleware as a security boundary for data access.
 */
const PUBLIC_PREFIXES = ['/login', '/auth/accept-invite']

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl
  const isPublic = PUBLIC_PREFIXES.some((p) => pathname === p || pathname.startsWith(`${p}/`))
  if (isPublic) return NextResponse.next()

  const marker = request.cookies.get('nh_auth')?.value
  if (!marker) {
    const login = new URL('/login', request.url)
    login.searchParams.set('next', pathname)
    return NextResponse.redirect(login)
  }

  const res = NextResponse.next()
  res.headers.set('X-Content-Type-Options', 'nosniff')
  res.headers.set('X-Frame-Options', 'DENY')
  return res
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)'],
}
