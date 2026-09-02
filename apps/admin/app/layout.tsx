import './globals.css'
import { AppProviders } from '@/providers/app-providers'
import { AppShell } from '@/components/app-shell'

export const metadata = {
  title: 'ناتیفیکیش‌هاب',
  description: 'مرکز کنترل ارکستراسیون اعلان‌ها',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="fa" dir="rtl" suppressHydrationWarning>
      <body className="font-sans">
        <AppProviders>
          <AppShell>{children}</AppShell>
        </AppProviders>
      </body>
    </html>
  )
}
