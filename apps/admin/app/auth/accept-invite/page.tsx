'use client'

import { Suspense, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { identityApi } from '@/lib/api/identity'
import { useAuth } from '@/providers/auth-provider'
import { getAccessToken } from '@/lib/auth/session'

function AcceptInviteInner() {
  const params = useSearchParams()
  const token = params.get('token') ?? ''
  const { login, isAuthenticated } = useAuth()
  const [done, setDone] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  async function accept() {
    if (!getAccessToken()) {
      login(`/auth/accept-invite?token=${encodeURIComponent(token)}`)
      return
    }
    setPending(true)
    setError(null)
    try {
      await identityApi.acceptInvite(token)
      setDone(true)
    } catch {
      setError('Invalid or expired invitation')
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="grid min-h-screen place-items-center px-4">
      <div className="w-full max-w-md rounded-3xl border bg-card p-8 shadow-xl">
        <h1 className="mb-2 text-lg font-bold">Accept invitation</h1>
        <p className="mb-6 text-sm text-muted-foreground">
          {isAuthenticated
            ? 'Confirm to join the organization.'
            : 'Sign in first, then accept the invite.'}
        </p>
        {done ? (
          <p className="text-sm text-emerald-600">
            You joined the organization.{' '}
            <a href="/dashboard" className="underline">Go to dashboard</a>
          </p>
        ) : (
          <Button className="w-full" disabled={!token || pending} onClick={() => void accept()}>
            {isAuthenticated ? 'Accept invite' : 'Sign in to accept'}
          </Button>
        )}
        {error && <p className="mt-3 text-xs text-destructive">{error}</p>}
      </div>
    </div>
  )
}

export default function AcceptInvitePage() {
  return (
    <Suspense fallback={<div className="grid min-h-screen place-items-center text-sm text-muted-foreground">Loading…</div>}>
      <AcceptInviteInner />
    </Suspense>
  )
}
