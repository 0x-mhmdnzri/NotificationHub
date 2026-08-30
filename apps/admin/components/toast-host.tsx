'use client'

import { Toast, type ToastTone } from '@/components/ui/toast'

export function ToastHost({ toast, onClose }: { toast: { tone: ToastTone; title: string; description?: string } | null; onClose: () => void }) {
  if (!toast) return null
  return <div className="fixed bottom-5 right-5 z-[100] pointer-events-none"><div className="pointer-events-auto"><Toast {...toast} onClose={onClose} /></div></div>
}
