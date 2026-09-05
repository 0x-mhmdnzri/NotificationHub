'use client'

import { Dialog } from '../ui/dialog'
import { Button } from '../ui/button'

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel = 'تأیید',
  cancelLabel = 'بازگشت',
  destructive,
  busy,
  onConfirm,
}: {
  open: boolean
  onOpenChange: (v: boolean) => void
  title: string
  description: React.ReactNode
  confirmLabel?: string
  cancelLabel?: string
  destructive?: boolean
  busy?: boolean
  onConfirm: () => void | Promise<void>
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange} title={title} description={typeof description === 'string' ? description : undefined}>
      <div className="space-y-5 p-5">
        {typeof description !== 'string' && <div className="text-sm leading-6 text-muted-foreground">{description}</div>}
        <div className="flex justify-end gap-2 border-t pt-4">
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={busy}>
            {cancelLabel}
          </Button>
          <Button
            variant={destructive ? 'destructive' : 'default'}
            disabled={busy}
            onClick={() => void onConfirm()}
          >
            {busy ? 'در حال انجام…' : confirmLabel}
          </Button>
        </div>
      </div>
    </Dialog>
  )
}
