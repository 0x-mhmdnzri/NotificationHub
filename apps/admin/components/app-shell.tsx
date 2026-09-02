'use client'

import { usePathname } from 'next/navigation'
import { Sidebar } from '@/components/sidebar'
import { Topbar } from '@/components/topbar'
import { RequireAuth } from '@/components/require-auth'

const PUBLIC = ['/login', '/auth/callback', '/auth/accept-invite']

export function AppShell({ children }: { children: React.ReactNode }) {
  const path = usePathname()
  const isPublic = PUBLIC.some((p) => path === p || path.startsWith(p + '/'))

  if (isPublic) {
    return <RequireAuth>{children}</RequireAuth>
  }

  return (
    <RequireAuth>
      <div className="min-h-screen bg-background">
        <Sidebar />
        {/* Physical right sidebar → content padding-right */}
        <div className="lg:pr-[270px]">
          <Topbar />
          <main className="min-h-[calc(100vh-72px)]">{children}</main>
        </div>
      </div>
    </RequireAuth>
  )
}
