'use client'

import { useEffect } from 'react'

/** Prefer local IRANSans FaNum modules when present; CDN fallback already in CSS. */
export function FontLoader() {
  useEffect(() => {
    void (async () => {
      try {
        const mod = await import('@/lib/fonts/load-iransans')
        await mod.loadIranSans()
      } catch {
        // modules not bundled yet — CDN IRANSans alias remains
      }
    })()
  }, [])
  return null
}
