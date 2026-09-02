'use client'

import { useEffect } from 'react'

/**
 * OIDC SPA callback removed — admin authenticates via API login/register only.
 * Keep route to avoid broken bookmarks; redirect to password login.
 */
export default function AuthCallbackRemoved() {
  useEffect(() => {
    window.location.replace('/login')
  }, [])
  return (
    <div className="grid min-h-screen place-items-center text-sm text-muted-foreground">
      Redirecting to sign-in…
    </div>
  )
}
