'use client'

import { cn } from '@/lib/utils'

export function Activity({ items, className }: { items: { id: string; title: string; time: string; tone?: string }[]; className?: string }) {
  if (!items.length) {
    return <p className={cn('text-sm text-muted-foreground', className)}>هنوز فعالیتی نیست.</p>
  }
  return (
    <div className={cn('space-y-3', className)}>
      {items.map((item) => (
        <div key={item.id} className="flex gap-3">
          <div className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-primary" />
          <div className="min-w-0 flex-1">
            <div className="text-sm">{item.title}</div>
            <div className="text-xs text-muted-foreground">{item.time}</div>
          </div>
        </div>
      ))}
    </div>
  )
}
