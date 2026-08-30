'use client'

import { useEffect } from 'react'
import { usePathname, useRouter } from 'next/navigation'
import { useAuth } from '@/providers/auth-provider'
import { getAccessToken } from '@/lib/auth/session'

const PUBLIC = ['/login', '/auth/callback', '/auth/accept-invite']

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isLoading, isAuthenticated, login } = useAuth()
  const path = usePathname()
  const router = useRouter()
  const isPublic = PUBLIC.some((p) => path === p || path.startsWith(p + '/'))

  useEffect(() => {
    if (isPublic || isLoading) return
    if (!getAccessToken() || !isAuthenticated) {
      login(path)
    }
  }, [isPublic, isLoading, isAuthenticated, login, path])

  if (isPublic) return <>{children}</>
  if (isLoading) {
    return (
      <div className="grid min-h-[40vh] place-items-center text-sm text-muted-foreground">
        Loading session…
      </div>
    )
  }
  if (!isAuthenticated) return null
  return <>{children}</>
}

export function RequirePermission({
  permission,
  children,
  fallback,
}: {
  permission: string | string[]
  children: React.ReactNode
  fallback?: React.ReactNode
}) {
  const { can } = useAuth()
  if (!can(permission)) {
    return (
      fallback ?? (
        <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-6 text-sm text-destructive">
          You do not have permission to view this page.
        </div>
      )
    )
  }
  return <>{children}</>
}
