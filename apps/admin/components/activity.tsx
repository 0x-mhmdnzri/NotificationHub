'use client'

import { CheckCircle2, Clock3, AlertCircle } from 'lucide-react'

export type ActivityItem = {
  title: string
  description: string
  time: string
  type?: 'success' | 'warning' | 'info'
}

export function Activity({ items }: { items: ActivityItem[] }) {
  if (!items.length) {
    return <p className="py-8 text-center text-sm text-muted-foreground">No activity yet.</p>
  }

  return (
    <div className="space-y-5">
      {items.map((item) => (
        <div key={`${item.title}-${item.time}`} className="flex gap-3">
          <div className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-muted">
            {item.type === 'success' ? (
              <CheckCircle2 size={16} className="text-emerald-500" />
            ) : item.type === 'warning' ? (
              <AlertCircle size={16} className="text-amber-500" />
            ) : (
              <Clock3 size={16} className="text-primary" />
            )}
          </div>
          <div className="min-w-0 flex-1">
            <div className="text-sm font-medium">{item.title}</div>
            <div className="truncate text-xs text-muted-foreground">{item.description}</div>
            <div className="mt-1 text-[10px] text-muted-foreground">{item.time}</div>
          </div>
        </div>
      ))}
    </div>
  )
}
