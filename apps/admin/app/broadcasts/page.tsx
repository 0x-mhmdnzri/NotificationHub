'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { KeyValueEditor, pairsToRecord, type KeyValuePair } from '@/components/key-value-editor'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { useTemplates } from '@/hooks/use-templates'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, friendlyError, templateTitle } from '@/lib/ux/labels'

export default function BroadcastsPage() {
  const { tenantId } = useTenant()
  const templates = useTemplates()
  const [name, setName] = useState('Product announcement')
  const [templateKey, setTemplateKey] = useState('')
  const [channel, setChannel] = useState('push')
  const [recipients, setRecipients] = useState('')
  const [segmentKey, setSegmentKey] = useState('')
  const [locale, setLocale] = useState('fa-IR')
  const [pairs, setPairs] = useState<KeyValuePair[]>([])
  const [confirm, setConfirm] = useState(false)
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)

  const list = recipients.split(/\n|,/).map((x) => x.trim()).filter(Boolean)
  const audienceSize = segmentKey ? 'a saved audience' : `${list.length} recipient${list.length === 1 ? '' : 's'}`

  async function send() {
    setBusy(true)
    try {
      await resourcesApi.broadcasts.send({
        name,
        templateKey,
        channel,
        recipients: list.length ? list : undefined,
        segmentKey: segmentKey || undefined,
        locale,
        tenantId,
        data: pairsToRecord(pairs),
      })
      setConfirm(false)
      setToast({ tone: 'success', title: 'Broadcast submitted', description: `Sending via ${formatChannel(channel)}.` })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not send broadcast', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[900px]">
        <PageHeader
          eyebrow="Send"
          title="Broadcast"
          description="Send one message to many people at once. Review carefully before sending."
        />
        <Card>
          <CardContent className="space-y-5 p-6">
            <Field label="Broadcast name"><Input value={name} onChange={(e) => setName(e.target.value)} /></Field>
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Template">
                <Select value={templateKey} onChange={(e) => setTemplateKey(e.target.value)} className="w-full">
                  <option value="">Choose template</option>
                  {templates.data?.map((t) => (
                    <option key={`${t.key}-${t.channel}`} value={t.key}>{templateTitle(t)} · {formatChannel(t.channel)}</option>
                  ))}
                </Select>
              </Field>
              <Field label="Channel">
                <Select value={channel} onChange={(e) => setChannel(e.target.value)} className="w-full">
                  <option value="push">Push</option>
                  <option value="email">Email</option>
                  <option value="sms">SMS</option>
                </Select>
              </Field>
            </div>
            <Field label="Saved audience code" hint="Optional — use instead of a manual list">
              <Input value={segmentKey} onChange={(e) => setSegmentKey(e.target.value)} placeholder="e.g. high-value-users" />
            </Field>
            <Field label="Recipients" hint="One per line — optional if you use a saved audience">
              <textarea
                value={recipients}
                onChange={(e) => setRecipients(e.target.value)}
                className="min-h-28 w-full rounded-xl border bg-background p-3 text-sm"
                placeholder="user-1\nuser-2"
              />
            </Field>
            <Field label="Language"><Input value={locale} onChange={(e) => setLocale(e.target.value)} /></Field>
            <Field label="Shared personalization">
              <KeyValueEditor pairs={pairs} onChange={setPairs} />
            </Field>
            <Button disabled={!templateKey || (!segmentKey && !list.length)} onClick={() => setConfirm(true)}>
              Review & send
            </Button>
          </CardContent>
        </Card>
      </div>

      <ConfirmDialog
        open={confirm}
        onOpenChange={setConfirm}
        title="Send this broadcast?"
        confirmLabel="Yes, send to audience"
        busy={busy}
        onConfirm={send}
        description={
          <ul className="list-disc space-y-1 pl-4">
            <li>Name: <strong>{name}</strong></li>
            <li>Channel: <strong>{formatChannel(channel)}</strong></li>
            <li>Audience: <strong>{audienceSize}</strong></li>
          </ul>
        }
      />
    </div>
  )
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-2 text-sm">
      <span className="flex gap-2 font-medium">
        {label}
        {hint && <span className="text-[10px] font-normal text-muted-foreground">{hint}</span>}
      </span>
      {children}
    </label>
  )
}
