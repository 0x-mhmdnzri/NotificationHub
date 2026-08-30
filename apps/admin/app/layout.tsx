import './globals.css'
import { AppProviders } from '@/providers/app-providers'
import { AppShell } from '@/components/app-shell'

export const metadata = {
  title: 'NotificationHub',
  description: 'Notification orchestration control plane',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <AppProviders>
          <AppShell>{children}</AppShell>
        </AppProviders>
      </body>
    </html>
  )
}
