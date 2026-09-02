'use client'

import { useState, Suspense } from 'react'
import { useSearchParams } from 'next/navigation'
import { Button } from '@/components/ui/button'
import { identityApi } from '@/lib/api/identity'
import {
  setSession,
  clearSession,
  safeReturnPath,
  tenantFromAccessToken,
  setAuthBootstrapLock,
} from '@/lib/auth/session'
import { ApiError } from '@/lib/api/errors'

function LoginInner() {
  const params = useSearchParams()
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

    // Stop AuthProvider / 401 handlers from wiping tokens during this flow
    setAuthBootstrapLock(true)
    // Drop stale refresh tokens so a parallel refresh cannot race login
    clearSession()
    setAuthBootstrapLock(true)

    try {
      const tokens =
        mode === 'login'
          ? await identityApi.login({ email: email.trim(), password })
          : await identityApi.register({
              email: email.trim(),
              password,
              displayName: displayName.trim() || undefined,
              createOrganization: true,
              organizationName: orgName.trim() || undefined,
            })

      const accessToken = tokens?.accessToken
      const refreshToken = tokens?.refreshToken
      if (!accessToken) {
        setError('Server did not return an access token')
        setAuthBootstrapLock(false)
        return
      }

      setSession({
        accessToken,
        refreshToken,
        tenantId:
          tokens.organizationId ??
          (typeof tokens.organizationId === 'string' ? tokens.organizationId : undefined) ??
          tenantFromAccessToken(accessToken),
      })

      // Hard navigation so middleware sees nh_auth cookie and memory/storage are consistent
      window.location.assign(next)
    } catch (err) {
      setAuthBootstrapLock(false)
      if (err instanceof ApiError) {
        const map: Record<string, string> = {
          invalid_credentials: 'Email or password is incorrect',
          email_taken: 'This email is already registered',
          invalid_input: 'Check email and password (min 8 characters)',
          user_inactive: 'This account is inactive',
        }
        setError(map[err.message] ?? err.message)
      } else {
        setError(err instanceof Error ? err.message : 'Authentication failed')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid min-h-screen place-items-center p-6">
      <form
        onSubmit={(e) => void submit(e)}
        className="w-full max-w-md space-y-4 rounded-2xl border bg-card p-8 shadow-sm"
      >
        <div>
          <h1 className="text-xl font-semibold tracking-tight">
            {mode === 'login' ? 'Sign in' : 'Create account'}
          </h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Sign in with your NotificationHub account against the API.
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
          onClick={() => {
            setMode(mode === 'login' ? 'register' : 'login')
            setError(null)
          }}
        >
          {mode === 'login' ? 'Need an account? Register' : 'Already registered? Sign in'}
        </button>
      </form>
    </div>
  )
}

export default function LoginPage() {
  return (
    <Suspense
      fallback={
        <div className="grid min-h-screen place-items-center text-sm text-muted-foreground">
          Loading…
        </div>
      }
    >
      <LoginInner />
    </Suspense>
  )
}
