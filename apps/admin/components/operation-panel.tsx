'use client'

import { useState } from 'react'
import { Loader2, Save } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { ToastHost } from '@/components/toast-host'
import { ApiError } from '@/lib/api/errors'
import { friendlyError } from '@/lib/ux/labels'

export function OperationPanel({
  title,
  description,
  children,
  onSubmit,
  label = 'ذخیره',
  successMessage = 'با موفقیت ذخیره شد.',
}: {
  title: string
  description?: string
  children: React.ReactNode
  onSubmit: () => Promise<unknown>
  label?: string
  successMessage?: string
}) {
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)

  async function submit() {
    setBusy(true)
    setToast(null)
    try {
      await onSubmit()
      setToast({ tone: 'success', title: successMessage })
    } catch (e) {
      setToast({
        tone: 'error',
        title: 'ذخیره ممکن نشد',
        description: e instanceof ApiError ? friendlyError(e) : friendlyError(e),
      })
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <Card className="overflow-hidden">
        <CardHeader className="border-b bg-muted/20">
          <div>
            <CardTitle>{title}</CardTitle>
            {description && <p className="mt-1 text-xs text-muted-foreground">{description}</p>}
          </div>
        </CardHeader>
        <CardContent className="space-y-5 p-6">
          {children}
          <div className="flex justify-end border-t pt-4">
            <Button onClick={() => void submit()} disabled={busy}>
              {busy ? <Loader2 size={15} className="animate-spin" /> : <Save size={15} />}
              {busy ? 'در حال انجام…' : label}
            </Button>
          </div>
        </CardContent>
      </Card>
    </>
  )
}

export function Field({
  label,
  hint,
  children,
}: {
  label: string
  hint?: string
  children: React.ReactNode
}) {
  return (
    <label className="block space-y-2">
      <span className="flex gap-2 text-sm font-medium">
        {label}
        {hint && <span className="text-[10px] font-normal text-muted-foreground">{hint}</span>}
      </span>
      {children}
    </label>
  )
}
