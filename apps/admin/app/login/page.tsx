'use client'

import { Layers3 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/providers/auth-provider'

export default function LoginPage() {
  const { login, isAuthenticated } = useAuth()

  if (isAuthenticated) {
    if (typeof window !== 'undefined') window.location.href = '/dashboard'
  }

  return (
    <div className="grid min-h-screen place-items-center bg-background px-4">
      <div className="w-full max-w-md rounded-3xl border bg-card p-8 shadow-xl">
        <div className="mb-6 flex items-center gap-3">
          <div className="grid h-11 w-11 place-items-center rounded-2xl bg-primary text-primary-foreground">
            <Layers3 size={20} />
          </div>
          <div>
            <div className="text-lg font-bold">NotificationHub</div>
            <div className="text-xs text-muted-foreground">Admin control plane</div>
          </div>
        </div>
        <p className="mb-6 text-sm leading-6 text-muted-foreground">
          Sign in with your organization account. Multi-tenant access is enforced server-side.
        </p>
        <Button className="w-full" size="lg" onClick={() => login('/dashboard')}>
          Continue with SSO
        </Button>
      </div>
    </div>
  )
}
