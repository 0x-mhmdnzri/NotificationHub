'use client'

import { useMemo, useState } from 'react'
import { Eye, Loader2, Send, ChevronRight, ChevronLeft } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { SectionCard } from '@/components/section-card'
import { ToastHost } from '@/components/toast-host'
import { StatusPill } from '@/components/ux/status-pill'
import {
  PersonalizationFields,
  pairsToRecord,
  type KvPair,
} from '@/components/ux/personalization-fields'
import { useTemplates } from '@/hooks/use-templates'
import { ApiError } from '@/lib/api/errors'
import { notificationsApi } from '@/lib/api/notifications'
import { formatChannel, friendlyError, humanTemplateName } from '@/lib/ux/labels'
import type { NotificationPriority, NotificationRequest } from '@/types/api'

const priorityMap: Record<string, NotificationPriority> = {
  low: 0,
  normal: 1,
  high: 2,
  critical: 3,
}

const steps = ['Who', 'What', 'Personalize', 'Delivery', 'Review'] as const

export default function NotificationsPage() {
  const [step, setStep] = useState(0)
  const [channel, setChannel] = useState('push')
  const [recipient, setRecipient] = useState('')
  const [templateKey, setTemplateKey] = useState('')
  const [priority, setPriority] = useState('normal')
  const [locale, setLocale] = useState('fa-IR')
  const [category, setCategory] = useState('transactional')
  const [pairs, setPairs] = useState<KvPair[]>([
    { key: 'amount', value: '1,250,000' },
    { key: 'reference', value: 'PAY-29381' },
  ])
  const [scheduledAt, setScheduledAt] = useState('')
  const [timeZoneId, setTimeZoneId] = useState('Asia/Tehran')
  const [allowFallback, setAllowFallback] = useState(true)
  const [preferredProvider, setPreferredProvider] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState('')
  const [collapseKey, setCollapseKey] = useState('')
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [busy, setBusy] = useState<'send' | 'wait' | 'preview' | null>(null)
  const [toast, setToast] = useState<{
    tone: 'success' | 'error'
    title: string
    description?: string
  } | null>(null)

  const templatesQuery = useTemplates(channel)
  const selectedTemplate = templatesQuery.data?.find((t) => t.key === templateKey)

  const payload = useMemo((): NotificationRequest | null => {
    if (!recipient.trim() || !templateKey.trim()) return null
    return {
      recipient: recipient.trim(),
      templateKey: templateKey.trim(),
      channel,
      priority: priorityMap[priority],
      data: pairsToRecord(pairs),
      idempotencyKey: idempotencyKey.trim() || undefined,
      preferredProvider: preferredProvider.trim() || undefined,
      allowFallback,
      category,
      locale,
      collapseKey: collapseKey.trim() || undefined,
      scheduledAt: scheduledAt ? new Date(scheduledAt).toISOString() : undefined,
      timeZoneId: scheduledAt ? timeZoneId : undefined,
    }
  }, [
    recipient,
    templateKey,
    channel,
    priority,
    pairs,
    idempotencyKey,
    preferredProvider,
    allowFallback,
    category,
    locale,
    collapseKey,
    scheduledAt,
    timeZoneId,
  ])

  function canNext() {
    if (step === 0) return !!recipient.trim()
    if (step === 1) return !!templateKey.trim()
    return true
  }

  async function execute(action: 'send' | 'wait' | 'preview') {
    if (!payload) {
      setToast({ tone: 'error', title: 'Missing details', description: 'Recipient and content are required.' })
      return
    }
    setBusy(action)
    setToast(null)
    try {
      if (action === 'send') await notificationsApi.send(payload)
      if (action === 'wait') await notificationsApi.sendSync(payload)
      if (action === 'preview') await notificationsApi.preview(payload)
      setToast({
        tone: 'success',
        title:
          action === 'preview'
            ? 'Preview ready'
            : action === 'wait'
              ? 'Notification delivered'
              : 'Notification queued',
        description:
          action === 'preview'
            ? 'You can review how the message will look with the current personalization.'
            : action === 'wait'
              ? 'The message was processed and a delivery result is available.'
              : 'The message was accepted and will be delivered shortly.',
      })
    } catch (error) {
      setToast({
        tone: 'error',
        title: 'Could not send',
        description: friendlyError(error instanceof ApiError ? error.message : undefined),
      })
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[960px]">
        <PageHeader
          eyebrow="Send"
          title="Send a notification"
          description="Choose who receives the message, pick content, personalize, then review before sending."
        />

        <div className="mb-6 flex flex-wrap gap-2">
          {steps.map((label, i) => (
            <button
              key={label}
              type="button"
              onClick={() => i <= step && setStep(i)}
              className={`rounded-full px-3 py-1.5 text-xs font-medium transition ${
                i === step
                  ? 'bg-primary text-primary-foreground'
                  : i < step
                    ? 'bg-primary/10 text-primary'
                    : 'bg-muted text-muted-foreground'
              }`}
            >
              {i + 1}. {label}
            </button>
          ))}
        </div>

        <Card>
          <CardContent className="space-y-6 p-6">
            {step === 0 && (
              <div className="space-y-4">
                <h2 className="text-lg font-semibold">Who should receive this?</h2>
                <Field label="Recipient">
                  <Input
                    value={recipient}
                    onChange={(e) => setRecipient(e.target.value)}
                    placeholder="User ID, phone number, or email"
                  />
                </Field>
                <Field label="Channel">
                  <Select value={channel} onChange={(e) => setChannel(e.target.value)} className="w-full">
                    <option value="push">Push</option>
                    <option value="sms">SMS</option>
                    <option value="email">Email</option>
                    <option value="webhook">Webhook</option>
                  </Select>
                </Field>
              </div>
            )}

            {step === 1 && (
              <div className="space-y-4">
                <h2 className="text-lg font-semibold">What should they receive?</h2>
                <Field label="Message template">
                  <Select
                    value={templateKey}
                    onChange={(e) => setTemplateKey(e.target.value)}
                    className="w-full"
                  >
                    <option value="">Select a template</option>
                    {templatesQuery.data?.map((t) => (
                      <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>
                        {humanTemplateName(t.key, t.subject)} · {formatChannel(t.channel)} · {t.locale}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label="Language">
                  <Input value={locale} onChange={(e) => setLocale(e.target.value)} placeholder="fa-IR" />
                </Field>
                {selectedTemplate && (
                  <div className="rounded-xl border bg-muted/30 p-4 text-sm">
                    <div className="font-medium">{humanTemplateName(selectedTemplate.key, selectedTemplate.subject)}</div>
                    <p className="mt-2 text-muted-foreground line-clamp-3">{selectedTemplate.body}</p>
                  </div>
                )}
              </div>
            )}

            {step === 2 && (
              <div className="space-y-4">
                <h2 className="text-lg font-semibold">Personalize the message</h2>
                <PersonalizationFields pairs={pairs} onChange={setPairs} />
              </div>
            )}

            {step === 3 && (
              <div className="space-y-4">
                <h2 className="text-lg font-semibold">How should it be delivered?</h2>
                <div className="grid gap-4 md:grid-cols-2">
                  <Field label="Priority">
                    <Select value={priority} onChange={(e) => setPriority(e.target.value)} className="w-full">
                      <option value="low">Low</option>
                      <option value="normal">Normal</option>
                      <option value="high">High</option>
                      <option value="critical">Critical</option>
                    </Select>
                  </Field>
                  <Field label="Category">
                    <Select value={category} onChange={(e) => setCategory(e.target.value)} className="w-full">
                      <option value="transactional">Transactional</option>
                      <option value="marketing">Marketing</option>
                      <option value="otp">Verification</option>
                      <option value="alert">Alert</option>
                    </Select>
                  </Field>
                  <Field label="Send later (optional)">
                    <Input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} />
                  </Field>
                  <Field label="Timezone">
                    <Input
                      value={timeZoneId}
                      onChange={(e) => setTimeZoneId(e.target.value)}
                      disabled={!scheduledAt}
                    />
                  </Field>
                </div>
                <label className="flex cursor-pointer items-start gap-3 rounded-xl border p-4">
                  <input
                    type="checkbox"
                    checked={allowFallback}
                    onChange={(e) => setAllowFallback(e.target.checked)}
                    className="mt-1 h-4 w-4 accent-primary"
                  />
                  <span>
                    <span className="block text-sm font-medium">Try another provider if the first one fails</span>
                    <span className="mt-1 block text-xs text-muted-foreground">
                      Recommended for important messages.
                    </span>
                  </span>
                </label>
                <button
                  type="button"
                  className="text-xs font-medium text-primary"
                  onClick={() => setShowAdvanced((v) => !v)}
                >
                  {showAdvanced ? 'Hide advanced options' : 'Show advanced options'}
                </button>
                {showAdvanced && (
                  <div className="grid gap-4 rounded-xl border border-dashed p-4 md:grid-cols-2">
                    <Field label="Prevent duplicate sends">
                      <Input
                        value={idempotencyKey}
                        onChange={(e) => setIdempotencyKey(e.target.value)}
                        placeholder="e.g. payment-29381-success"
                      />
                    </Field>
                    <Field label="Preferred provider">
                      <Input
                        value={preferredProvider}
                        onChange={(e) => setPreferredProvider(e.target.value)}
                        placeholder="Automatic"
                      />
                    </Field>
                    <Field label="Collapse related messages">
                      <Input
                        value={collapseKey}
                        onChange={(e) => setCollapseKey(e.target.value)}
                        placeholder="Optional group key"
                      />
                    </Field>
                  </div>
                )}
              </div>
            )}

            {step === 4 && (
              <div className="space-y-4">
                <h2 className="text-lg font-semibold">Review before sending</h2>
                <div className="divide-y rounded-xl border">
                  <ReviewRow label="Recipient" value={recipient || '—'} />
                  <ReviewRow label="Channel" value={formatChannel(channel)} />
                  <ReviewRow
                    label="Content"
                    value={humanTemplateName(templateKey, selectedTemplate?.subject)}
                  />
                  <ReviewRow label="Language" value={locale} />
                  <ReviewRow label="Priority" value={priority} />
                  <ReviewRow
                    label="When"
                    value={scheduledAt ? `Scheduled · ${scheduledAt} (${timeZoneId})` : 'Send immediately'}
                  />
                  <ReviewRow
                    label="Fallback"
                    value={allowFallback ? 'Enabled' : 'Disabled'}
                  />
                  <ReviewRow
                    label="Personalization"
                    value={
                      Object.entries(pairsToRecord(pairs))
                        .map(([k, v]) => `${k}: ${v}`)
                        .join(' · ') || 'None'
                    }
                  />
                </div>
                <p className="text-xs text-muted-foreground">
                  This will send a real notification to the recipient on the selected channel.
                </p>
              </div>
            )}

            <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-5">
              <Button
                variant="ghost"
                disabled={step === 0 || !!busy}
                onClick={() => setStep((s) => Math.max(0, s - 1))}
              >
                <ChevronLeft size={16} /> Back
              </Button>
              <div className="flex flex-wrap gap-2">
                {step < steps.length - 1 ? (
                  <Button disabled={!canNext()} onClick={() => setStep((s) => s + 1)}>
                    Continue <ChevronRight size={16} />
                  </Button>
                ) : (
                  <>
                    <Button variant="ghost" disabled={!!busy} onClick={() => execute('preview')}>
                      {busy === 'preview' ? <Loader2 className="animate-spin" size={16} /> : <Eye size={16} />}
                      Preview
                    </Button>
                    <Button variant="outline" disabled={!!busy} onClick={() => execute('wait')}>
                      {busy === 'wait' ? <Loader2 className="animate-spin" size={16} /> : <Send size={16} />}
                      Send and wait for result
                    </Button>
                    <Button disabled={!!busy} onClick={() => execute('send')}>
                      {busy === 'send' ? <Loader2 className="animate-spin" size={16} /> : <Send size={16} />}
                      Send now
                    </Button>
                  </>
                )}
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="mt-6">
          <SectionCard title="Recent activity" subtitle="Sample deliveries for orientation">
            <div className="space-y-3">
              {[
                ['OTP verification', 'push', 'Delivered', '•••2214'],
                ['Payment successful', 'sms', 'Delivered', '•••0871'],
                ['Invoice ready', 'email', 'Queued', 'user@domain.com'],
              ].map((n) => (
                <div key={n[0]} className="flex items-center justify-between rounded-xl border p-3 text-sm">
                  <div>
                    <div className="font-medium">{n[0]}</div>
                    <div className="text-xs text-muted-foreground">
                      {formatChannel(n[1])} · {n[3]}
                    </div>
                  </div>
                  <StatusPill status={n[2]} />
                </div>
              ))}
            </div>
          </SectionCard>
        </div>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-2 text-sm">
      <span className="font-medium">{label}</span>
      {children}
    </label>
  )
}

function ReviewRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-1 px-4 py-3 sm:flex-row sm:justify-between">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-medium sm:text-right">{value}</span>
    </div>
  )
}
