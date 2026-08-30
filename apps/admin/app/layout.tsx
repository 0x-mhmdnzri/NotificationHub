import './globals.css'
import { AppProviders } from '@/providers/app-providers'
import { Sidebar } from '@/components/sidebar'
import { Topbar } from '@/components/topbar'

export const metadata = { title: 'NotificationHub', description: 'Notification orchestration control plane' }

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return <html lang="en" suppressHydrationWarning><body><div className="min-h-screen bg-background"><Sidebar/><div className="lg:pl-[270px]"><Topbar/><main className="min-h-[calc(100vh-72px)]"><AppProviders>{children}</AppProviders></main></div></div></body></html>
}
