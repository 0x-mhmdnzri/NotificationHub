'use client'

import { useEffect } from 'react'
import { loadIranSans } from '@/lib/fonts/load-iransans'

export function FontLoader() {
  useEffect(() => {
    void loadIranSans()
  }, [])
  return null
}
