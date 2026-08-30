'use client'

import { useState } from 'react'
import { Loader2, Save } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { ToastHost } from '@/components/toast-host'
import { ApiError } from '@/lib/api/errors'
import { friendlyError } from '@/lib/ux/labels'

export function OperationPanel({
  title,
  description,
  children,
  onSubmit,
  label = 'Save',
}: {
  title: string
  description?: string
  children: React.ReactNode
  onSubmit: () => Promise<unknown>
  label?: string
}) {
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<{
    tone: 'success' | 'error'
    title: string
    description?: string
  } | null>(null)

  async function submit() {
    setBusy(true)
    setToast(null)
    try {
      await onSubmit()
      setToast({ tone: 'success', title: 'Saved', description: 'Your changes are in effect.' })
    } catch (e) {
      setToast({
        tone: 'error',
        title: 'Could not save',
        description: friendlyError(e instanceof ApiError ? e.message : undefined),
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
          <CardTitle>{title}</CardTitle>
          {description && <p className="mt-1 text-xs text-muted-foreground">{description}</p>}
        </CardHeader>
        <CardContent className="space-y-5 p-6">
          {children}
          <div className="flex justify-end border-t pt-4">
            <Button onClick={submit} disabled={busy}>
              {busy ? <Loader2 size={15} className="animate-spin" /> : <Save size={15} />}
              {busy ? 'Saving…' : label}
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

/** Advanced-only: technical payload editor. Do not use as primary form control. */
export function JsonArea({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <div className="space-y-2">
      <p className="text-[10px] font-medium uppercase tracking-widest text-muted-foreground">
        Technical details
      </p>
      <Textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="min-h-40 font-mono text-xs"
        spellCheck={false}
      />
    </div>
  )
}
