import './globals.css'
import { AppProviders } from '@/providers/app-providers'
import { Sidebar } from '@/components/sidebar'
import { Topbar } from '@/components/topbar'
import { RequireAuth } from '@/components/require-auth'

export const metadata = {
  title: 'NotificationHub',
  description: 'Notification orchestration control plane',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <AppProviders>
          <RequireAuth>
            <Shell>{children}</Shell>
          </RequireAuth>
        </AppProviders>
      </body>
    </html>
  )
}

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-background">
      <Sidebar />
      <div className="lg:pl-[270px]">
        <Topbar />
        <main className="min-h-[calc(100vh-72px)]">{children}</main>
      </div>
    </div>
  )
}
