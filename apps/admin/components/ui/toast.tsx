'use client'

import { useEffect } from 'react'
import { CheckCircle2, CircleAlert, X } from 'lucide-react'
import { cn } from '@/lib/utils'

export type ToastTone = 'success' | 'error'

export function Toast({
  tone,
  title,
  description,
  onClose,
}: {
  tone: ToastTone
  title: string
  description?: string
  onClose: () => void
}) {
  useEffect(() => {
    const timer = window.setTimeout(onClose, 4500)
    return () => window.clearTimeout(timer)
  }, [onClose])

  const Icon = tone === 'success' ? CheckCircle2 : CircleAlert

  return (
    <div className="pointer-events-auto w-[360px] max-w-[calc(100vw-2rem)] animate-in slide-in-from-right-5 fade-in rounded-2xl border bg-card p-4 shadow-2xl duration-300">
      <div className="flex gap-3">
        <Icon className={cn('mt-0.5 h-5 w-5 shrink-0', tone === 'success' ? 'text-emerald-500' : 'text-destructive')} />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold">{title}</p>
          {description && <p className="mt-1 text-xs leading-5 text-muted-foreground">{description}</p>}
        </div>
        <button type="button" onClick={onClose} className="rounded-lg p-1 text-muted-foreground hover:bg-muted" aria-label="بستن">
          <X size={16} />
        </button>
      </div>
    </div>
  )
}
