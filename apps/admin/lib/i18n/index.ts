'use client'

import { fa, type FaKey } from './fa'

export function t(key: FaKey): string {
  return fa[key] ?? key
}

export function useT() {
  return t
}

export { fa }
export type { FaKey }
