'use client'

import { useMemo, useState } from 'react'
import { Eye, Loader2, Send, ShieldCheck, Sparkles } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Badge } from '@/components/ui/badge'
import { SectionCard } from '@/components/section-card'
import { ToastHost } from '@/components/toast-host'
import { notifications } from '@/lib/mock'
import { useTemplates } from '@/hooks/use-templates'
import { ApiError } from '@/lib/api/errors'
import { notificationsApi } from '@/lib/api/notifications'
import type { NotificationPriority, NotificationRequest } from '@/types/api'

const priorityMap: Record<string, NotificationPriority> = { low: 0, normal: 1, high: 2, critical: 3 }

export default function Notifications() {
  const [channel, setChannel] = useState('push')
  const [recipient, setRecipient] = useState('')
  const [templateKey, setTemplateKey] = useState('')
  const [priority, setPriority] = useState('normal')
  const [idempotencyKey, setIdempotencyKey] = useState('')
  const [preferredProvider, setPreferredProvider] = useState('')
  const [category, setCategory] = useState('transactional')
  const [locale, setLocale] = useState('fa-IR')
  const [collapseKey, setCollapseKey] = useState('')
  const [dataText, setDataText] = useState('{\n  "amount": "1,250,000",\n  "reference": "PAY-29381"\n}')
  const [allowFallback, setAllowFallback] = useState(true)
  const [busy, setBusy] = useState<'send' | 'sync' | 'preview' | null>(null)
  const [scheduledAt, setScheduledAt] = useState('')
  const [timeZoneId, setTimeZoneId] = useState('Asia/Tehran')
  const templatesQuery = useTemplates(channel)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)

  const parsedData = useMemo(() => {
    try {
      return { value: JSON.parse(dataText) as Record<string, unknown>, error: null }
    } catch {
      return { value: null, error: 'Template data must be valid JSON.' }
    }
  }, [dataText])

  const buildPayload = (): NotificationRequest | null => {
    if (!recipient.trim() || !templateKey.trim()) {
      setToast({ tone: 'error', title: 'Missing required fields', description: 'Recipient and template key are required.' })
      return null
    }
    if (parsedData.error) {
      setToast({ tone: 'error', title: 'Invalid template data', description: parsedData.error })
      return null
    }
    return {
      recipient: recipient.trim(),
      templateKey: templateKey.trim(),
      channel,
      priority: priorityMap[priority],
      data: parsedData.value,
      idempotencyKey: idempotencyKey.trim() || undefined,
      preferredProvider: preferredProvider.trim() || undefined,
      allowFallback,
      category,
      locale,
      collapseKey: collapseKey.trim() || undefined,
      scheduledAt: scheduledAt ? new Date(scheduledAt).toISOString() : undefined,
      timeZoneId: scheduledAt ? timeZoneId : undefined,
    }
  }

  async function execute(action: 'send' | 'sync' | 'preview') {
    const payload = buildPayload()
    if (!payload) return
    setBusy(action)
    setToast(null)
    try {
      if (action === 'send') await notificationsApi.send(payload)
      if (action === 'sync') await notificationsApi.sendSync(payload)
      if (action === 'preview') await notificationsApi.preview(payload)
      setToast({
        tone: 'success',
        title: action === 'preview' ? 'Preview generated' : action === 'sync' ? 'Notification sent synchronously' : 'Notification accepted',
        description: action === 'preview' ? 'The payload was accepted by the template preview endpoint.' : 'The NotificationHub API accepted the request.',
      })
    } catch (error) {
      const description = error instanceof ApiError ? `${error.message}${error.details?.traceId ? ` · traceId: ${error.details.traceId}` : ''}` : 'Unable to reach the NotificationHub API.'
      setToast({ tone: 'error', title: 'Request failed', description })
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1500px]">
        <PageHeader eyebrow="Messaging" title="Send notification" description="Dispatch through the orchestration layer with provider fallback, scheduling and idempotency controls." />

        <div className="grid gap-5 xl:grid-cols-[1.4fr_.6fr]">
          <Card className="overflow-hidden">
            <CardHeader className="border-b bg-muted/20">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <CardTitle>Notification payload</CardTitle>
                  <p className="mt-1 text-xs text-muted-foreground">Required fields are validated before the request leaves the browser.</p>
                </div>
                <Badge variant="outline">POST /notifications</Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-6 p-6">
              <div className="grid gap-4 md:grid-cols-2">
                <Field label="Recipient" required>
                  <Input value={recipient} onChange={e => setRecipient(e.target.value)} placeholder="user id, phone or email" />
                </Field>
                <Field label="Template" required>
                  <div className="flex gap-2"><Select value={templateKey} onChange={e => setTemplateKey(e.target.value)} className="flex-1"><option value="">Select a template</option>{templatesQuery.data?.map(t => <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>{t.key} · {t.locale}</option>)}</Select><Input value={templateKey} onChange={e => setTemplateKey(e.target.value)} placeholder="or type key" className="hidden md:block md:w-44" /></div>
                </Field>
                <Field label="Channel">
                  <Select value={channel} onChange={e => setChannel(e.target.value)} className="w-full">
                    <option value="push">Push</option><option value="sms">SMS</option><option value="email">Email</option><option value="webhook">Webhook</option>
                  </Select>
                </Field>
                <Field label="Priority">
                  <Select value={priority} onChange={e => setPriority(e.target.value)} className="w-full">
                    <option value="low">Low</option><option value="normal">Normal</option><option value="high">High</option><option value="critical">Critical</option>
                  </Select>
                </Field>
                <Field label="Locale"><Input value={locale} onChange={e => setLocale(e.target.value)} placeholder="fa-IR" /></Field>
                <Field label="Category"><Input value={category} onChange={e => setCategory(e.target.value)} placeholder="transactional" /></Field>
              </div>

              <Field label="Template data" hint="JSON object">
                <textarea value={dataText} onChange={e => setDataText(e.target.value)} className="min-h-40 w-full rounded-xl border bg-background p-3 font-mono text-xs leading-6 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15" spellCheck={false} />
              </Field>

              <div className="grid gap-4 md:grid-cols-2">
                <Field label="Schedule" hint="Optional"><Input type="datetime-local" value={scheduledAt} onChange={e => setScheduledAt(e.target.value)} /></Field>
                <Field label="Time zone"><Input value={timeZoneId} onChange={e => setTimeZoneId(e.target.value)} disabled={!scheduledAt} /></Field>
                <Field label="Idempotency key" hint="Recommended"><Input value={idempotencyKey} onChange={e => setIdempotencyKey(e.target.value)} placeholder="payment:29381:success" /></Field>
                <Field label="Preferred provider"><Input value={preferredProvider} onChange={e => setPreferredProvider(e.target.value)} placeholder="auto" /></Field>
                <Field label="Collapse key"><Input value={collapseKey} onChange={e => setCollapseKey(e.target.value)} placeholder="payment-status" /></Field>
              </div>

              <label className="flex cursor-pointer items-start gap-3 rounded-xl border p-4 transition hover:bg-muted/40">
                <input type="checkbox" checked={allowFallback} onChange={e => setAllowFallback(e.target.checked)} className="mt-1 h-4 w-4 accent-primary" />
                <span><span className="block text-sm font-medium">Allow provider fallback</span><span className="mt-1 block text-xs leading-5 text-muted-foreground">Let the orchestration layer move to another provider when the preferred provider cannot deliver.</span></span>
              </label>

              <div className="flex flex-wrap gap-2 border-t pt-5">
                <Button disabled={!!busy} onClick={() => execute('send')}>{busy === 'send' ? <Loader2 className="animate-spin" size={16} /> : <Send size={16} />}Send now</Button>
                <Button variant="outline" disabled={!!busy} onClick={() => execute('sync')}>{busy === 'sync' ? <Loader2 className="animate-spin" size={16} /> : <Sparkles size={16} />}Send sync</Button>
                <Button variant="ghost" disabled={!!busy} onClick={() => execute('preview')}>{busy === 'preview' ? <Loader2 className="animate-spin" size={16} /> : <Eye size={16} />}Preview</Button>
              </div>
            </CardContent>
          </Card>

          <div className="space-y-5">
            <SectionCard title="Delivery policy">
              <div className="space-y-4 text-sm">
                <PolicyRow label="Fallback" value={allowFallback ? 'Enabled' : 'Disabled'} good={allowFallback} />
                <PolicyRow label="Consent check" value="Required" good />
                <PolicyRow label="Quiet hours" value="22:00–08:00" />
                <PolicyRow label="Idempotency" value={idempotencyKey ? 'Configured' : 'Not set'} good={!!idempotencyKey} />
              </div>
            </SectionCard>
            <Card className="bg-primary text-primary-foreground shadow-lg shadow-primary/20">
              <CardContent className="p-5">
                <div className="mb-3 flex items-center gap-2 font-medium"><ShieldCheck size={17} />Safe dispatch</div>
                <p className="text-xs leading-5 text-primary-foreground/75">Authorization, tenant isolation, consent and idempotency remain server-side responsibilities. The UI never treats client validation as security.</p>
              </CardContent>
            </Card>
          </div>
        </div>

        <div className="mt-5">
          <SectionCard title="Recent deliveries" subtitle="Mock data until GET notification history is exposed by the API">
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{notifications.slice(0, 6).map(n => <div key={n[0]} className="rounded-xl border p-4 transition duration-200 hover:-translate-y-0.5 hover:shadow-md"><div className="flex justify-between"><span className="font-mono text-[10px] text-muted-foreground">{n[0]}</span><Badge variant={n[3] === 'Delivered' ? 'success' : n[3] === 'Queued' ? 'warning' : 'danger'}>{n[3]}</Badge></div><div className="mt-3 text-sm font-medium">{n[1]}</div><div className="mt-1 text-xs text-muted-foreground">{n[2]} · {n[4]} · {n[5]}</div></div>)}</div>
          </SectionCard>
        </div>
      </div>
    </div>
  )
}

function Field({ label, required, hint, children }: { label: string; required?: boolean; hint?: string; children: React.ReactNode }) {
  return <label className="space-y-2 text-sm"><span className="flex items-center gap-2 font-medium">{label}{required && <span className="text-destructive">*</span>}{hint && <span className="text-[10px] font-normal text-muted-foreground">{hint}</span>}</span>{children}</label>
}

function PolicyRow({ label, value, good }: { label: string; value: string; good?: boolean }) {
  return <div className="flex items-center justify-between gap-4"><span className="text-muted-foreground">{label}</span><Badge variant={good ? 'success' : 'outline'}>{value}</Badge></div>
}
