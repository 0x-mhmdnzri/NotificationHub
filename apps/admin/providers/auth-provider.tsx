'use client'

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { identityApi, type AuthMe, type OrgMembership } from '@/lib/api/identity'
import { getAccessToken, setSession, clearSession } from '@/lib/auth/session'
import { beginLogin, logoutLocal, refreshAccessToken } from '@/lib/auth/oidc'
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

  useEffect(() => {
    void (async () => {
      if (!getAccessToken()) {
        await refreshAccessToken()
      }
      setTokenReady(true)
    })()
  }, [])

  const meQuery = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: () => identityApi.me(),
    enabled: tokenReady && !!getAccessToken(),
    retry: false,
  })

  const orgsQuery = useQuery({
    queryKey: ['auth', 'organizations'],
    queryFn: () => identityApi.organizations(),
    enabled: tokenReady && !!getAccessToken(),
    retry: false,
  })

  const login = useCallback((returnTo?: string) => {
    void beginLogin(returnTo)
  }, [])

  const logout = useCallback(async () => {
    try {
      if (getAccessToken()) await identityApi.logout()
    } catch {
      /* ignore */
    }
    logoutLocal()
    queryClient.clear()
    window.location.href = '/login'
  }, [queryClient])

  const switchOrganization = useCallback(async (organizationId: string) => {
    await identityApi.switchOrganization(organizationId)
    setSession({ tenantId: organizationId })
    // Client must re-request token with tenant_id from Identity host when available.
    await queryClient.invalidateQueries({ queryKey: ['auth'] })
    await queryClient.invalidateQueries()
  }, [queryClient])

  const refreshMe = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['auth'] })
  }, [queryClient])

  const value = useMemo<AuthContextValue>(() => ({
    me: meQuery.data,
    organizations: orgsQuery.data ?? [],
    isLoading: !tokenReady || meQuery.isLoading,
    isAuthenticated: !!getAccessToken() && !!meQuery.data,
    can: (p) => hasPermission(meQuery.data?.permissions, p),
    login,
    logout,
    switchOrganization,
    refreshMe,
  }), [meQuery.data, meQuery.isLoading, orgsQuery.data, tokenReady, login, logout, switchOrganization, refreshMe])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
