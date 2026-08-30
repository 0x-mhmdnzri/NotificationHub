'use client'

import { useState, Suspense } from 'react'
import { useSearchParams, useRouter } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { identityApi } from '@/lib/api/identity'
import { setSession, safeReturnPath } from '@/lib/auth/session'

function LoginInner() {
  const params = useSearchParams()
  const router = useRouter()
  const next = safeReturnPath(params.get('next'), '/dashboard')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [displayName, setDisplayName] = useState('')
  const [orgName, setOrgName] = useState('')

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const tokens =
        mode === 'login'
          ? await identityApi.login({ email, password })
          : await identityApi.register({
              email,
              password,
              displayName: displayName || undefined,
              createOrganization: true,
              organizationName: orgName || undefined,
            })
      setSession({
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
      })
      router.replace(next)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Authentication failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid min-h-screen place-items-center p-6">
      <form onSubmit={(e) => void submit(e)} className="w-full max-w-md space-y-4 rounded-2xl border bg-card p-8 shadow-sm">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">
            {mode === 'login' ? 'Sign in' : 'Create account'}
          </h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Backend API login powered by OpenIddict-ready JWT auth. SuperAdmin has full access.
          </p>
        </div>
        {mode === 'register' && (
          <>
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">Display name</span>
              <input
                className="w-full rounded-xl border bg-background px-3 py-2"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
              />
            </label>
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">Organization name</span>
              <input
                className="w-full rounded-xl border bg-background px-3 py-2"
                value={orgName}
                onChange={(e) => setOrgName(e.target.value)}
                placeholder="Acme Inc."
              />
            </label>
          </>
        )}
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">Email</span>
          <input
            type="email"
            required
            autoComplete="username"
            className="w-full rounded-xl border bg-background px-3 py-2"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">Password</span>
          <input
            type="password"
            required
            minLength={8}
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            className="w-full rounded-xl border bg-background px-3 py-2"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <Button type="submit" className="w-full" disabled={busy}>
          {busy ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Register'}
        </Button>
        <button
          type="button"
          className="w-full text-center text-xs text-muted-foreground underline"
          onClick={() => setMode(mode === 'login' ? 'register' : 'login')}
        >
          {mode === 'login' ? 'Need an account? Register' : 'Already registered? Sign in'}
        </button>
      </form>
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
