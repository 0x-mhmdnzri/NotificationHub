'use client'

import { useMemo, useState } from 'react'
import { Check, ChevronLeft, ChevronRight, Eye, Loader2, Send, ShieldCheck } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Badge } from '@/components/ui/badge'
import { SectionCard } from '@/components/section-card'
import { ToastHost } from '@/components/toast-host'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { KeyValueEditor, pairsToRecord, type KeyValuePair } from '@/components/key-value-editor'
import { useTemplates } from '@/hooks/use-templates'
import { ApiError } from '@/lib/api/errors'
import { notificationsApi } from '@/lib/api/notifications'
import {
  formatChannel,
  formatStatus,
  friendlyError,
  priorityLabel,
  templateTitle,
} from '@/lib/ux/labels'
import type { NotificationPriority, NotificationRequest } from '@/types/api'

const STEPS = ['Audience', 'Content', 'Personalize', 'Delivery', 'Review'] as const
const priorityMap: Record<string, NotificationPriority> = { low: 0, normal: 1, high: 2, critical: 3 }

export default function NotificationsPage() {
  const [step, setStep] = useState(0)
  const [channel, setChannel] = useState('push')
  const [recipient, setRecipient] = useState('')
  const [templateKey, setTemplateKey] = useState('')
  const [priority, setPriority] = useState('normal')
  const [locale, setLocale] = useState('fa-IR')
  const [category, setCategory] = useState('transactional')
  const [pairs, setPairs] = useState<KeyValuePair[]>([{ key: 'amount', value: '' }, { key: 'reference', value: '' }])
  const [scheduledAt, setScheduledAt] = useState('')
  const [timeZoneId, setTimeZoneId] = useState('Asia/Tehran')
  const [allowFallback, setAllowFallback] = useState(true)
  const [idempotencyKey, setIdempotencyKey] = useState('')
  const [preferredProvider, setPreferredProvider] = useState('')
  const [collapseKey, setCollapseKey] = useState('')
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [previewText, setPreviewText] = useState<string | null>(null)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)

  const templatesQuery = useTemplates(channel)
  const selectedTemplate = templatesQuery.data?.find((t) => t.key === templateKey)

  const canNext = useMemo(() => {
    if (step === 0) return recipient.trim().length > 0
    if (step === 1) return templateKey.trim().length > 0
    return true
  }, [step, recipient, templateKey])

  function buildPayload(): NotificationRequest | null {
    if (!recipient.trim() || !templateKey.trim()) {
      setToast({ tone: 'error', title: 'Missing information', description: 'Choose a recipient and a template before continuing.' })
      return null
    }
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
  }

  async function send() {
    const payload = buildPayload()
    if (!payload) return
    setBusy(true)
    setToast(null)
    try {
      await notificationsApi.send(payload)
      setConfirmOpen(false)
      setToast({
        tone: 'success',
        title: scheduledAt ? 'Notification scheduled' : 'Notification queued',
        description: `Message for ${recipient.trim()} is on its way via ${formatChannel(channel)}.`,
      })
      setStep(0)
    } catch (error) {
      setToast({ tone: 'error', title: 'Could not send', description: friendlyError(error instanceof ApiError ? error : error) })
    } finally {
      setBusy(false)
    }
  }

  async function preview() {
    const payload = buildPayload()
    if (!payload) return
    setBusy(true)
    try {
      const res = await notificationsApi.preview(payload)
      const text =
        typeof res === 'string'
          ? res
          : (res as { body?: string; subject?: string; content?: string })?.body ||
            (res as { subject?: string })?.subject ||
            (res as { content?: string })?.content ||
            'Preview generated successfully.'
      setPreviewText(String(text))
      setToast({ tone: 'success', title: 'Preview ready' })
    } catch (error) {
      setToast({ tone: 'error', title: 'Preview failed', description: friendlyError(error) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="Send"
          title="Send a notification"
          description="Choose who receives it, what they see, and when it should go out."
        />

        {/* Step indicator */}
        <div className="mb-8 flex flex-wrap gap-2">
          {STEPS.map((label, i) => (
            <button
              key={label}
              type="button"
              onClick={() => i <= step && setStep(i)}
              className={`flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-medium transition ${
                i === step
                  ? 'bg-primary text-primary-foreground'
                  : i < step
                    ? 'bg-primary/15 text-primary'
                    : 'bg-muted text-muted-foreground'
              }`}
            >
              {i < step ? <Check size={12} /> : <span className="opacity-70">{i + 1}</span>}
              {label}
            </button>
          ))}
        </div>

        <div className="grid gap-5 lg:grid-cols-[1.4fr_.6fr]">
          <Card>
            <CardContent className="space-y-6 p-6">
              {step === 0 && (
                <>
                  <h2 className="text-base font-semibold">Who should receive this?</h2>
                  <Field label="Recipient" required hint="User ID, phone number, or email">
                    <Input
                      value={recipient}
                      onChange={(e) => setRecipient(e.target.value)}
                      placeholder="e.g. user-1024 or +98912…"
                      autoFocus
                    />
                  </Field>
                  <Field label="Channel">
                    <Select value={channel} onChange={(e) => { setChannel(e.target.value); setTemplateKey('') }} className="w-full">
                      <option value="push">Push</option>
                      <option value="sms">SMS</option>
                      <option value="email">Email</option>
                      <option value="webhook">Webhook</option>
                    </Select>
                  </Field>
                </>
              )}

              {step === 1 && (
                <>
                  <h2 className="text-base font-semibold">What should they receive?</h2>
                  <Field label="Template" required>
                    <Select value={templateKey} onChange={(e) => setTemplateKey(e.target.value)} className="w-full">
                      <option value="">Choose a template</option>
                      {templatesQuery.data?.map((t) => (
                        <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>
                          {templateTitle(t)} · {formatChannel(t.channel)} · {t.locale}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <Field label="Language">
                      <Input value={locale} onChange={(e) => setLocale(e.target.value)} placeholder="fa-IR" />
                    </Field>
                    <Field label="Category">
                      <Select value={category} onChange={(e) => setCategory(e.target.value)} className="w-full">
                        <option value="transactional">Transactional</option>
                        <option value="marketing">Marketing</option>
                        <option value="security">Security</option>
                        <option value="system">System</option>
                      </Select>
                    </Field>
                  </div>
                  {selectedTemplate?.subject && (
                    <div className="rounded-xl border bg-muted/30 p-4 text-sm">
                      <div className="text-xs text-muted-foreground">Subject preview</div>
                      <div className="mt-1 font-medium">{selectedTemplate.subject}</div>
                    </div>
                  )}
                </>
              )}

              {step === 2 && (
                <>
                  <h2 className="text-base font-semibold">Personalization</h2>
                  <p className="text-sm text-muted-foreground">
                    Fill in the values your template uses (amounts, names, codes). Leave blank if not needed.
                  </p>
                  <KeyValueEditor pairs={pairs} onChange={setPairs} keyPlaceholder="Variable" valuePlaceholder="Value for this send" />
                </>
              )}

              {step === 3 && (
                <>
                  <h2 className="text-base font-semibold">When and how to deliver</h2>
                  <Field label="Priority">
                    <Select value={priority} onChange={(e) => setPriority(e.target.value)} className="w-full">
                      <option value="low">Low</option>
                      <option value="normal">Normal</option>
                      <option value="high">High</option>
                      <option value="critical">Urgent</option>
                    </Select>
                  </Field>
                  <Field label="Schedule" hint="Leave empty to send immediately">
                    <Input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} />
                  </Field>
                  {scheduledAt && (
                    <Field label="Time zone">
                      <Input value={timeZoneId} onChange={(e) => setTimeZoneId(e.target.value)} />
                    </Field>
                  )}
                  <label className="flex cursor-pointer items-start gap-3 rounded-xl border p-4 transition hover:bg-muted/40">
                    <input
                      type="checkbox"
                      checked={allowFallback}
                      onChange={(e) => setAllowFallback(e.target.checked)}
                      className="mt-1 h-4 w-4 accent-primary"
                    />
                    <span>
                      <span className="block text-sm font-medium">Try another provider if the first fails</span>
                      <span className="mt-1 block text-xs leading-5 text-muted-foreground">
                        Recommended for important messages so a single provider outage does not block delivery.
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
                    <div className="grid gap-4 rounded-xl border border-dashed p-4 sm:grid-cols-2">
                      <Field label="Duplicate protection key" hint="Prevents double-send">
                        <Input value={idempotencyKey} onChange={(e) => setIdempotencyKey(e.target.value)} placeholder="payment-29381" />
                      </Field>
                      <Field label="Preferred provider">
                        <Input value={preferredProvider} onChange={(e) => setPreferredProvider(e.target.value)} placeholder="Automatic" />
                      </Field>
                      <Field label="Collapse key" hint="Replace older messages">
                        <Input value={collapseKey} onChange={(e) => setCollapseKey(e.target.value)} />
                      </Field>
                    </div>
                  )}
                </>
              )}

              {step === 4 && (
                <>
                  <h2 className="text-base font-semibold">Review before sending</h2>
                  <dl className="space-y-3 text-sm">
                    <ReviewRow label="Recipient" value={recipient} />
                    <ReviewRow label="Channel" value={formatChannel(channel)} />
                    <ReviewRow label="Template" value={selectedTemplate ? templateTitle(selectedTemplate) : templateKey} />
                    <ReviewRow label="Language" value={locale} />
                    <ReviewRow label="Priority" value={priorityLabel[priority] ?? priority} />
                    <ReviewRow label="When" value={scheduledAt ? `Scheduled · ${scheduledAt} (${timeZoneId})` : 'Immediately'} />
                    <ReviewRow label="Fallback providers" value={allowFallback ? 'Yes' : 'No'} />
                    {Object.keys(pairsToRecord(pairs)).length > 0 && (
                      <div className="rounded-xl border bg-muted/20 p-3">
                        <div className="mb-2 text-xs text-muted-foreground">Personalization</div>
                        <ul className="space-y-1">
                          {pairs.filter((p) => p.key.trim()).map((p) => (
                            <li key={p.key} className="flex justify-between gap-4">
                              <span className="text-muted-foreground">{p.key}</span>
                              <span className="font-medium">{p.value || '—'}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                  </dl>
                  {previewText && (
                    <div className="rounded-xl border bg-background p-4">
                      <div className="mb-2 text-xs font-medium text-muted-foreground">Content preview</div>
                      <p className="whitespace-pre-wrap text-sm leading-6">{previewText}</p>
                    </div>
                  )}
                </>
              )}

              <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-5">
                <Button variant="ghost" disabled={step === 0 || busy} onClick={() => setStep((s) => s - 1)}>
                  <ChevronLeft size={16} /> Back
                </Button>
                <div className="flex flex-wrap gap-2">
                  {step === 4 && (
                    <Button variant="outline" disabled={busy} onClick={() => void preview()}>
                      {busy ? <Loader2 className="animate-spin" size={16} /> : <Eye size={16} />}
                      Preview
                    </Button>
                  )}
                  {step < 4 ? (
                    <Button disabled={!canNext || busy} onClick={() => setStep((s) => s + 1)}>
                      Continue <ChevronRight size={16} />
                    </Button>
                  ) : (
                    <Button disabled={busy} onClick={() => setConfirmOpen(true)}>
                      <Send size={16} /> {scheduledAt ? 'Schedule' : 'Send'}
                    </Button>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="space-y-5">
            <SectionCard title="Delivery checks">
              <div className="space-y-3 text-sm">
                <Row label="Consent" value="Checked on server" good />
                <Row label="Provider fallback" value={allowFallback ? 'On' : 'Off'} good={allowFallback} />
                <Row label="Duplicate protection" value={idempotencyKey ? 'On' : 'Off'} good={!!idempotencyKey} />
              </div>
            </SectionCard>
            <Card className="bg-primary text-primary-foreground shadow-lg shadow-primary/20">
              <CardContent className="p-5">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium">
                  <ShieldCheck size={16} /> Safe by design
                </div>
                <p className="text-xs leading-5 text-primary-foreground/80">
                  Tenant isolation, consent, and authorization are enforced by the server. This screen only helps you prepare the message.
                </p>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={scheduledAt ? 'Schedule this notification?' : 'Send this notification?'}
        confirmLabel={scheduledAt ? 'Yes, schedule it' : 'Yes, send it'}
        busy={busy}
        onConfirm={send}
        description={
          <ul className="list-disc space-y-1 pl-4">
            <li>Recipient: <strong>{recipient}</strong></li>
            <li>Channel: <strong>{formatChannel(channel)}</strong></li>
            <li>Template: <strong>{selectedTemplate ? templateTitle(selectedTemplate) : templateKey}</strong></li>
            <li>Timing: <strong>{scheduledAt || 'Immediately'}</strong></li>
          </ul>
        }
      />
    </div>
  )
}

function Field({ label, required, hint, children }: { label: string; required?: boolean; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-2 text-sm">
      <span className="flex items-center gap-2 font-medium">
        {label}
        {required && <span className="text-destructive">*</span>}
        {hint && <span className="text-[10px] font-normal text-muted-foreground">{hint}</span>}
      </span>
      {children}
    </label>
  )
}

function ReviewRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 border-b border-dashed pb-2">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium">{value || '—'}</dd>
    </div>
  )
}

function Row({ label, value, good }: { label: string; value: string; good?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <Badge variant={good ? 'success' : 'outline'}>{value}</Badge>
    </div>
  )
}
