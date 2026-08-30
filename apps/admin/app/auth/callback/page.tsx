'use client'

import { useEffect, useState } from 'react'
import { handleCallback } from '@/lib/auth/oidc'

export default function AuthCallbackPage() {
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void (async () => {
      try {
        await handleCallback(window.location.search)
        const returnTo = sessionStorage.getItem('notificationhub.returnTo') || '/dashboard'
        sessionStorage.removeItem('notificationhub.returnTo')
        window.location.replace(returnTo)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'login_failed')
      }
    })()
  }, [])

  if (error) {
    return (
      <div className="grid min-h-screen place-items-center p-6">
        <div className="rounded-2xl border border-destructive/40 bg-destructive/5 p-6 text-sm">
          Sign-in failed: {error}
          <div className="mt-4">
            <a className="text-primary underline" href="/login">Back to login</a>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="grid min-h-screen place-items-center text-sm text-muted-foreground">
      Completing sign-in…
    </div>
  )
}
