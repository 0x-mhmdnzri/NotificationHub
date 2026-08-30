'use client'

import { QueryProvider } from './query-provider'
import { TenantProvider } from './tenant-provider'

export function AppProviders({ children }: { children: React.ReactNode }) {
  return <QueryProvider><TenantProvider>{children}</TenantProvider></QueryProvider>
}
