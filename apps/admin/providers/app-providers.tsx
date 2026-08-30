'use client'

import { QueryProvider } from './query-provider'
import { TenantProvider } from './tenant-provider'
import { AuthProvider } from './auth-provider'

export function AppProviders({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
      <AuthProvider>
        <TenantProvider>{children}</TenantProvider>
      </AuthProvider>
    </QueryProvider>
  )
}
