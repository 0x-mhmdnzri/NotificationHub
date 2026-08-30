'use client'

import { createContext, useContext, useMemo } from 'react'
import { useAuth } from './auth-provider'
import { getTenantId, setSession } from '@/lib/auth/session'

interface TenantContextValue {
  tenantId?: string
  setTenantId: (value: string) => void
}

const TenantContext = createContext<TenantContextValue | null>(null)

export function TenantProvider({ children }: { children: React.ReactNode }) {
  const { switchOrganization, me } = useAuth()
  const tenantId = me?.tenant?.id ?? getTenantId()

  const value = useMemo(
    () => ({
      tenantId,
      setTenantId(value: string) {
        setSession({ tenantId: value })
        void switchOrganization(value)
      },
    }),
    [tenantId, switchOrganization],
  )

  return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>
}

export function useTenant() {
  const value = useContext(TenantContext)
  if (!value) throw new Error('useTenant must be used inside TenantProvider')
  return value
}
