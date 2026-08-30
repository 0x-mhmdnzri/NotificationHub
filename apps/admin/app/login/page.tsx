'use client'

import { useSearchParams } from 'next/navigation'
import { Suspense } from 'react'
import { Button } from '@/components/ui/button'
import { beginLogin } from '@/lib/auth/oidc'
import { safeReturnPath } from '@/lib/auth/session'

function LoginInner() {
  const params = useSearchParams()
  const next = safeReturnPath(params.get('next'), '/dashboard')

  return (
    <div className="grid min-h-screen place-items-center p-6">
      <div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-sm">
        <h1 className="text-xl font-semibold tracking-tight">Sign in</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Use your organization account to access NotificationHub operations.
        </p>
        <Button className="mt-6 w-full" onClick={() => void beginLogin(next)}>
          Continue with Identity
        </Button>
      </div>
    </div>
  )
}

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="grid min-h-screen place-items-center text-sm text-muted-foreground">Loading…</div>}>
      <LoginInner />
    </Suspense>
  )
}
