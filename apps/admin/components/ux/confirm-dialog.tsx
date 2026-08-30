'use client'

import { Dialog } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel = 'Confirm',
  destructive,
  busy,
  onConfirm,
}: {
  open: boolean
  onOpenChange: (v: boolean) => void
  title: string
  description: string
  confirmLabel?: string
  destructive?: boolean
  busy?: boolean
  onConfirm: () => void | Promise<void>
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange} title={title} description={description}>
      <div className="flex justify-end gap-2 border-t p-5">
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
          Go back
        </Button>
        <Button
          variant={destructive ? 'destructive' : 'default'}
          disabled={busy}
          onClick={() => void onConfirm()}
        >
          {confirmLabel}
        </Button>
      </div>
    </Dialog>
  )
}
