'use client'

import { Button } from '@/components/ui/button'

export function EmptyState({
  icon: Icon,
  title,
  description,
  actionLabel,
  onAction,
}: {
  icon?: React.ComponentType<{ size?: number }>
  title: string
  description: string
  actionLabel?: string
  onAction?: () => void
}) {
  return (
    <div className="rounded-2xl border border-dashed p-12 text-center">
      {Icon && (
        <div className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-primary/10 text-primary">
          <Icon size={22} />
        </div>
      )}
      <h3 className="mt-4 font-semibold">{title}</h3>
      <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">{description}</p>
      {actionLabel && onAction && (
        <Button className="mt-5" onClick={onAction}>
          {actionLabel}
        </Button>
      )}
    </div>
  )
}
