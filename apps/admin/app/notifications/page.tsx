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
  friendlyError,
  priorityLabel,
  templateTitle,
} from '@/lib/ux/labels'
import type { NotificationPriority, NotificationRequest } from '@/types/api'

const STEPS = ['مخاطب', 'محتوا', 'شخصی‌سازی', 'تحویل', 'بررسی'] as const
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
      setToast({ tone: 'error', title: 'اطلاعات ناقص', description: 'قبل از ادامه گیرنده و قالب را انتخاب کنید.' })
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
        title: scheduledAt ? 'اعلان زمان‌بندی شد' : 'اعلان در صف قرار گرفت',
        description: `پیام برای ${recipient.trim()} از طریق ${formatChannel(channel)} ارسال می‌شود.`,
      })
      setStep(0)
    } catch (error) {
      setToast({ tone: 'error', title: 'ارسال ممکن نشد', description: friendlyError(error instanceof ApiError ? error : error) })
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
            'پیش‌نمایش با موفقیت تولید شد.'
      setPreviewText(String(text))
      setToast({ tone: 'success', title: 'پیش‌نمایش آماده است' })
    } catch (error) {
      setToast({ tone: 'error', title: 'پیش‌نمایش ناموفق بود', description: friendlyError(error) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="ارسال"
          title="ارسال اعلان"
          description="گیرنده، محتوا و زمان ارسال را مشخص کنید."
        />

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
                  <h2 className="text-base font-semibold">این پیام برای چه کسی است؟</h2>
                  <Field label="گیرنده" required hint="شناسه کاربر، شماره موبایل یا ایمیل">
                    <Input
                      value={recipient}
                      onChange={(e) => setRecipient(e.target.value)}
                      placeholder="مثلاً user-1024 یا +98912…"
                      autoFocus
                    />
                  </Field>
                  <Field label="کانال">
                    <Select value={channel} onChange={(e) => { setChannel(e.target.value); setTemplateKey('') }} className="w-full">
                      <option value="push">پوش</option>
                      <option value="sms">پیامک</option>
                      <option value="email">ایمیل</option>
                      <option value="webhook">وب‌هوک</option>
                    </Select>
                  </Field>
                </>
              )}

              {step === 1 && (
                <>
                  <h2 className="text-base font-semibold">چه محتوایی دریافت کنند؟</h2>
                  <Field label="قالب" required>
                    <Select value={templateKey} onChange={(e) => setTemplateKey(e.target.value)} className="w-full">
                      <option value="">انتخاب قالب</option>
                      {templatesQuery.data?.map((t) => (
                        <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>
                          {templateTitle(t)} · {formatChannel(t.channel)} · {t.locale}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <Field label="زبان">
                      <Input value={locale} onChange={(e) => setLocale(e.target.value)} placeholder="fa-IR" />
                    </Field>
                    <Field label="دسته">
                      <Select value={category} onChange={(e) => setCategory(e.target.value)} className="w-full">
                        <option value="transactional">تراکنشی</option>
                        <option value="marketing">بازاریابی</option>
                        <option value="security">امنیتی</option>
                        <option value="system">سیستمی</option>
                      </Select>
                    </Field>
                  </div>
                  {selectedTemplate?.subject && (
                    <div className="rounded-xl border bg-muted/30 p-4 text-sm">
                      <div className="text-xs text-muted-foreground">پیش‌نمایش موضوع</div>
                      <div className="mt-1 font-medium">{selectedTemplate.subject}</div>
                    </div>
                  )}
                </>
              )}

              {step === 2 && (
                <>
                  <h2 className="text-base font-semibold">شخصی‌سازی</h2>
                  <p className="text-sm text-muted-foreground">
                    مقادیر مورد استفاده قالب (مبلغ، نام، کد و …) را وارد کنید. در صورت عدم نیاز خالی بگذارید.
                  </p>
                  <KeyValueEditor pairs={pairs} onChange={setPairs} keyPlaceholder="متغیر" valuePlaceholder="مقدار این ارسال" />
                </>
              )}

              {step === 3 && (
                <>
                  <h2 className="text-base font-semibold">زمان و نحوه تحویل</h2>
                  <Field label="اولویت">
                    <Select value={priority} onChange={(e) => setPriority(e.target.value)} className="w-full">
                      <option value="low">کم</option>
                      <option value="normal">عادی</option>
                      <option value="high">بالا</option>
                      <option value="critical">فوری</option>
                    </Select>
                  </Field>
                  <Field label="زمان‌بندی" hint="خالی بگذارید تا فوری ارسال شود">
                    <Input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} />
                  </Field>
                  {scheduledAt && (
                    <Field label="منطقه زمانی">
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
                      <span className="block text-sm font-medium">در صورت شکست ارائه‌دهنده اول، ارائه‌دهنده دیگر را امتحان کن</span>
                      <span className="mt-1 block text-xs leading-5 text-muted-foreground">
                        برای پیام‌های مهم توصیه می‌شود تا قطعی یک ارائه‌دهنده مانع تحویل نشود.
                      </span>
                    </span>
                  </label>
                  <button
                    type="button"
                    className="text-xs font-medium text-primary"
                    onClick={() => setShowAdvanced((v) => !v)}
                  >
                    {showAdvanced ? 'پنهان کردن گزینه‌های پیشرفته' : 'نمایش گزینه‌های پیشرفته'}
                  </button>
                  {showAdvanced && (
                    <div className="grid gap-4 rounded-xl border border-dashed p-4 sm:grid-cols-2">
                      <Field label="کلید جلوگیری از ارسال تکراری" hint="از ارسال دوبل جلوگیری می‌کند">
                        <Input value={idempotencyKey} onChange={(e) => setIdempotencyKey(e.target.value)} placeholder="payment-29381" />
                      </Field>
                      <Field label="ارائه‌دهنده ترجیحی">
                        <Input value={preferredProvider} onChange={(e) => setPreferredProvider(e.target.value)} placeholder="خودکار" />
                      </Field>
                      <Field label="کلید ادغام" hint="جایگزین پیام‌های قدیمی‌تر">
                        <Input value={collapseKey} onChange={(e) => setCollapseKey(e.target.value)} />
                      </Field>
                    </div>
                  )}
                </>
              )}

              {step === 4 && (
                <>
                  <h2 className="text-base font-semibold">بررسی قبل از ارسال</h2>
                  <dl className="space-y-3 text-sm">
                    <ReviewRow label="گیرنده" value={recipient} />
                    <ReviewRow label="کانال" value={formatChannel(channel)} />
                    <ReviewRow label="قالب" value={selectedTemplate ? templateTitle(selectedTemplate) : templateKey} />
                    <ReviewRow label="زبان" value={locale} />
                    <ReviewRow label="اولویت" value={priorityLabel[priority] ?? priority} />
                    <ReviewRow label="زمان" value={scheduledAt ? `زمان‌بندی‌شده · ${scheduledAt} (${timeZoneId})` : 'فوری'} />
                    <ReviewRow label="ارائه‌دهنده پشتیبان" value={allowFallback ? 'بله' : 'خیر'} />
                    {Object.keys(pairsToRecord(pairs)).length > 0 && (
                      <div className="rounded-xl border bg-muted/20 p-3">
                        <div className="mb-2 text-xs text-muted-foreground">شخصی‌سازی</div>
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
                      <div className="mb-2 text-xs font-medium text-muted-foreground">پیش‌نمایش محتوا</div>
                      <p className="whitespace-pre-wrap text-sm leading-6">{previewText}</p>
                    </div>
                  )}
                </>
              )}

              <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-5">
                <Button variant="ghost" disabled={step === 0 || busy} onClick={() => setStep((s) => s - 1)}>
                  <ChevronLeft size={16} /> بازگشت
                </Button>
                <div className="flex flex-wrap gap-2">
                  {step === 4 && (
                    <Button variant="outline" disabled={busy} onClick={() => void preview()}>
                      {busy ? <Loader2 className="animate-spin" size={16} /> : <Eye size={16} />}
                      پیش‌نمایش
                    </Button>
                  )}
                  {step < 4 ? (
                    <Button disabled={!canNext || busy} onClick={() => setStep((s) => s + 1)}>
                      ادامه <ChevronRight size={16} />
                    </Button>
                  ) : (
                    <Button disabled={busy} onClick={() => setConfirmOpen(true)}>
                      <Send size={16} /> {scheduledAt ? 'زمان‌بندی' : 'ارسال'}
                    </Button>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="space-y-5">
            <SectionCard title="بررسی‌های تحویل">
              <div className="space-y-3 text-sm">
                <Row label="رضایت" value="بررسی روی سرور" good />
                <Row label="ارائه‌دهنده پشتیبان" value={allowFallback ? 'روشن' : 'خاموش'} good={allowFallback} />
                <Row label="جلوگیری از ارسال تکراری" value={idempotencyKey ? 'روشن' : 'خاموش'} good={!!idempotencyKey} />
              </div>
            </SectionCard>
            <Card className="bg-primary text-primary-foreground shadow-lg shadow-primary/20">
              <CardContent className="p-5">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium">
                  <ShieldCheck size={16} /> ایمن بر اساس طراحی
                </div>
                <p className="text-xs leading-5 text-primary-foreground/80">
                  جداسازی مستأجر، رضایت و مجوز توسط سرور اعمال می‌شود. این صفحه فقط برای آماده‌سازی پیام است.
                </p>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={scheduledAt ? 'این اعلان زمان‌بندی شود؟' : 'این اعلان ارسال شود؟'}
        confirmLabel={scheduledAt ? 'بله، زمان‌بندی شود' : 'بله، ارسال شود'}
        busy={busy}
        onConfirm={send}
        description={
          <ul className="list-disc space-y-1 pr-4">
            <li>گیرنده: <strong>{recipient}</strong></li>
            <li>کانال: <strong>{formatChannel(channel)}</strong></li>
            <li>قالب: <strong>{selectedTemplate ? templateTitle(selectedTemplate) : templateKey}</strong></li>
            <li>زمان: <strong>{scheduledAt || 'فوری'}</strong></li>
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
      <dd className="text-left font-medium">{value || '—'}</dd>
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
