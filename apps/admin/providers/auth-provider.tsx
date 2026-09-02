'use client'

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { usePathname } from 'next/navigation'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { identityApi, type AuthMe, type OrgMembership } from '@/lib/api/identity'
import {
  getAccessToken,
  getRefreshToken,
  setSession,
  clearSession,
  isPublicAuthPath,
  isAuthBootstrapLocked,
} from '@/lib/auth/session'
import { hasPermission } from '@/lib/auth/permissions'

interface AuthContextValue {
  me?: AuthMe
  organizations: OrgMembership[]
  isLoading: boolean
  isAuthenticated: boolean
  can: (permission: string | string[]) => boolean
  login: (returnTo?: string) => void
  logout: () => Promise<void>
  switchOrganization: (organizationId: string) => Promise<void>
  refreshMe: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [tokenReady, setTokenReady] = useState(false)
  const queryClient = useQueryClient()
  const pathname = usePathname()
  const onPublic = isPublicAuthPath(pathname)

  useEffect(() => {
    void (async () => {
      // On login/register pages never auto-refresh or clearSession — avoids racing the form submit.
      if (onPublic || isAuthBootstrapLocked()) {
        setTokenReady(true)
        return
      }

      if (!getAccessToken()) {
        const rt = getRefreshToken()
        if (rt) {
          try {
            const tokens = await identityApi.refresh(rt)
            if (tokens?.accessToken) {
              setSession({
                accessToken: tokens.accessToken,
                refreshToken: tokens.refreshToken ?? rt,
              })
            }
          } catch {
            if (!isAuthBootstrapLocked()) clearSession()
          }
        }
      }
      setTokenReady(true)
    })()
  }, [onPublic])

  const hasToken = tokenReady && !!getAccessToken()

  const meQuery = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: () => identityApi.me(),
    enabled: !onPublic && hasToken,
    retry: false,
  })

  const orgsQuery = useQuery({
    queryKey: ['auth', 'organizations'],
    queryFn: () => identityApi.organizations(),
    enabled: !onPublic && hasToken,
    retry: false,
  })

  const login = useCallback((returnTo?: string) => {
    const next = returnTo ?? '/dashboard'
    window.location.href = `/login?next=${encodeURIComponent(next)}`
  }, [])

  const logout = useCallback(async () => {
    try {
      if (getAccessToken()) await identityApi.logout()
    } catch {
      /* ignore */
    }
    clearSession()
    queryClient.clear()
    window.location.href = '/login'
  }, [queryClient])

  const switchOrganization = useCallback(
    async (organizationId: string) => {
      const tokens = await identityApi.switchOrganization(organizationId)
      setSession({
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        tenantId: organizationId,
      })
      await queryClient.invalidateQueries({ queryKey: ['auth'] })
      await queryClient.invalidateQueries()
    },
    [queryClient],
  )

  const refreshMe = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['auth'] })
  }, [queryClient])

  const roles = meQuery.data?.roles ?? []
  const permissions = meQuery.data?.permissions ?? []

  const can = useCallback(
    (permission: string | string[]) => hasPermission(permissions, roles, permission),
    [permissions, roles],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      me: meQuery.data,
      organizations: orgsQuery.data ?? [],
      isLoading: !tokenReady || (!onPublic && hasToken && (meQuery.isLoading || orgsQuery.isLoading)),
      isAuthenticated: !!getAccessToken() && !!meQuery.data,
      can,
      login,
      logout,
      switchOrganization,
      refreshMe,
    }),
    [
      meQuery.data,
      meQuery.isLoading,
      orgsQuery.data,
      orgsQuery.isLoading,
      tokenReady,
      onPublic,
      hasToken,
      can,
      login,
      logout,
      switchOrganization,
      refreshMe,
    ],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
