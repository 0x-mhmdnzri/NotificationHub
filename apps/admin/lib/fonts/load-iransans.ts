'use client'

import { b64 as regularB64, weight as regularW } from './regular'
import { b64 as mediumB64, weight as mediumW } from './medium'
import { b64 as boldB64, weight as boldW } from './bold'

/** Load IRANSans (FaNum) via FontFace API */
export async function loadIranSans() {
  if (typeof document === 'undefined') return
  const w = window as unknown as { __nhIranSans?: boolean }
  if (w.__nhIranSans) return

  const faces = [
    { weight: regularW, b64: regularB64 },
    { weight: mediumW, b64: mediumB64 },
    { weight: boldW, b64: boldB64 },
  ]

  await Promise.all(
    faces.map(async ({ weight, b64 }) => {
      const face = new FontFace('IRANSans', `url(data:font/woff2;base64,${b64})`, {
        weight: String(weight),
        style: 'normal',
        display: 'swap',
      })
      document.fonts.add(await face.load())
    }),
  )
  w.__nhIranSans = true
  document.documentElement.classList.add('font-iransans-ready')
}
