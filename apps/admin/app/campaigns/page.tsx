'use client'

import { useRef, useState } from 'react'
import { Loader2, Megaphone, Play, Plus, Upload, XCircle } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Progress } from '@/components/ui/progress'
import { Dialog } from '@/components/ui/dialog'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { KeyValueEditor, pairsToRecord, type KeyValuePair } from '@/components/key-value-editor'
import { ToastHost } from '@/components/toast-host'
import { useTemplates } from '@/hooks/use-templates'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, formatStatus, friendlyError, statusTone, templateTitle } from '@/lib/ux/labels'
import type { CreateCampaignRequest } from '@/types/api'

export default function CampaignsPage() {
  const { tenantId } = useTenant()
  const templates = useTemplates()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [templateKey, setTemplateKey] = useState('')
  const [channels, setChannels] = useState<string[]>(['email'])
  const [scheduled, setScheduled] = useState('')
  const [pairs, setPairs] = useState<KeyValuePair[]>([])
  const [busy, setBusy] = useState(false)
  const [campaignId, setCampaignId] = useState('')
  const [campaignName, setCampaignName] = useState('')
  const [progress, setProgress] = useState<{
    percentage?: number
    percent?: number
    total?: number
    processed?: number
    successful?: number
    failed?: number
    pending?: number
    status?: string
  } | null>(null)
  const [recipients, setRecipients] = useState('')
  const [confirmSend, setConfirmSend] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const toggle = (c: string) => setChannels((x) => (x.includes(c) ? x.filter((v) => v !== c) : [...x, c]))

  const create = async () => {
    if (!name || !templateKey || !channels.length) return
    setBusy(true)
    try {
      const payload: CreateCampaignRequest = {
        name,
        templateKey,
        channels,
        tenantId,
        data: pairsToRecord(pairs),
        scheduledAtUtc: scheduled ? new Date(scheduled).toISOString() : undefined,
      }
      const res = (await resourcesApi.campaigns.create(payload)) as { id?: string; campaignId?: string }
      const id = String(res?.id ?? res?.campaignId ?? '')
      setCampaignId(id)
      setCampaignName(name)
      setOpen(false)
      setToast({ tone: 'success', title: 'کمپین ایجاد شد', description: `«${name}» آماده دریافت مخاطب است.` })
    } catch (e) {
      setToast({ tone: 'error', title: 'ایجاد کمپین ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  const addRecipients = async () => {
    if (!campaignId || !recipients.trim()) return
    const addresses = recipients.split(/[\n,;]+/).map((x) => x.trim()).filter(Boolean)
    try {
      await resourcesApi.campaigns.recipients(campaignId, { addresses, channels })
      setRecipients('')
      setToast({ tone: 'success', title: `${addresses.length} گیرنده اضافه شد` })
    } catch (e) {
      setToast({ tone: 'error', title: 'افزودن گیرنده ممکن نشد', description: friendlyError(e) })
    }
  }

  const importCsv = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file || !campaignId) return
    const form = new FormData()
    form.append('file', file)
    try {
      await resourcesApi.campaigns.importCsv(campaignId, form)
      setToast({ tone: 'success', title: 'ورود فایل شروع شد', description: file.name })
    } catch (err) {
      setToast({ tone: 'error', title: 'ورود فایل ناموفق بود', description: friendlyError(err) })
    }
    if (fileRef.current) fileRef.current.value = ''
  }

  const inspect = async () => {
    if (!campaignId) return
    try {
      const p = (await resourcesApi.campaigns.progress(campaignId)) as typeof progress
      setProgress(p)
    } catch (e) {
      setToast({ tone: 'error', title: 'بروزرسانی پیشرفت ممکن نشد', description: friendlyError(e) })
    }
  }

  const send = async () => {
    if (!campaignId) return
    setBusy(true)
    try {
      await resourcesApi.campaigns.send(campaignId)
      setConfirmSend(false)
      setToast({ tone: 'success', title: 'کمپین شروع شد' })
      await inspect()
    } catch (e) {
      setToast({ tone: 'error', title: 'شروع کمپین ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  const cancel = async () => {
    if (!campaignId) return
    setBusy(true)
    try {
      await resourcesApi.campaigns.cancel(campaignId)
      setConfirmCancel(false)
      setToast({ tone: 'success', title: 'کمپین لغو شد' })
      await inspect()
    } catch (e) {
      setToast({ tone: 'error', title: 'لغو ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  const pct = Number(progress?.percentage ?? progress?.percent ?? 0)

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1500px]">
        <PageHeader
          eyebrow="ارسال"
          title="کمپین‌ها"
          description="با یک پیام به افراد زیادی برسید. مخاطب را بسازید، بررسی کنید، سپس شروع کنید."
          action={
            <Button onClick={() => setOpen(true)}>
              <Plus size={16} /> کمپین جدید
            </Button>
          }
        />

        <div className="grid gap-5 lg:grid-cols-[1fr_380px]">
          <Card>
            <CardHeader>
              <CardTitle>{campaignName || 'فضای کار کمپین'}</CardTitle>
            </CardHeader>
            <CardContent>
              {!campaignId ? (
                <div className="rounded-2xl border border-dashed p-12 text-center">
                  <div className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-primary/10 text-primary">
                    <Megaphone size={22} />
                  </div>
                  <h3 className="mt-4 font-semibold">کمپینی انتخاب نشده</h3>
                  <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">
                    یک کمپین بسازید، افراد را با فهرست یا فایل اضافه کنید، سپس وقتی آماده بودید ارسال را شروع کنید.
                  </p>
                  <Button className="mt-5" onClick={() => setOpen(true)}>
                    <Plus size={15} /> ایجاد کمپین
                  </Button>
                </div>
              ) : (
                <div className="space-y-6">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={statusTone(progress?.status)}>{formatStatus(progress?.status) || 'آماده'}</Badge>
                    <span className="text-xs text-muted-foreground">کمپین در حال کار</span>
                  </div>

                  <div className="rounded-xl border bg-muted/20 p-4">
                    <div className="mb-3 text-sm font-medium">مخاطب</div>
                    <textarea
                      value={recipients}
                      onChange={(e) => setRecipients(e.target.value)}
                      placeholder="هر خط یک گیرنده (شناسه کاربر، ایمیل یا موبایل)"
                      className="min-h-28 w-full rounded-xl border bg-background p-3 text-sm"
                    />
                    <div className="mt-3 flex flex-wrap gap-2">
                      <Button variant="outline" onClick={() => void addRecipients()} disabled={!recipients.trim()}>
                        <Plus size={14} /> افزودن فهرست
                      </Button>
                      <Button variant="outline" onClick={() => fileRef.current?.click()}>
                        <Upload size={14} /> ورود از فایل
                      </Button>
                      <input ref={fileRef} type="file" accept=".csv,text/csv" className="hidden" onChange={(e) => void importCsv(e)} />
                    </div>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <Button onClick={() => setConfirmSend(true)}>
                      <Play size={15} /> شروع کمپین
                    </Button>
                    <Button variant="outline" onClick={() => void inspect()}>
                      بروزرسانی پیشرفت
                    </Button>
                    <Button variant="ghost" className="text-destructive" onClick={() => setConfirmCancel(true)}>
                      <XCircle size={15} /> لغو کمپین
                    </Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>پیشرفت</CardTitle>
            </CardHeader>
            <CardContent>
              {!campaignId && <p className="text-sm text-muted-foreground">پس از ایجاد کمپین، پیشرفت اینجا نمایش داده می‌شود.</p>}
              {campaignId && !progress && (
                <p className="text-sm text-muted-foreground">برای بارگذاری آخرین اعداد، «بروزرسانی پیشرفت» را بزنید.</p>
              )}
              {progress && (
                <div className="space-y-4">
                  <div>
                    <div className="mb-2 flex justify-between text-xs">
                      <span>تکمیل</span>
                      <span className="font-medium">{pct}%</span>
                    </div>
                    <Progress value={pct} />
                  </div>
                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <Stat label="مجموع" value={progress.total} />
                    <Stat label="پردازش‌شده" value={progress.processed} />
                    <Stat label="موفق" value={progress.successful} good />
                    <Stat label="ناموفق" value={progress.failed} bad />
                    <Stat label="در انتظار" value={progress.pending} />
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      <Dialog
        open={open}
        onOpenChange={setOpen}
        title="کمپین جدید"
        description="نام کمپین، قالب و کانال‌ها را انتخاب کنید، سپس مخاطب را اضافه کنید."
      >
        <div className="space-y-5 p-5">
          <Field label="نام کمپین">
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="یادآوری پرداخت آخر هفته" />
          </Field>
          <Field label="قالب پیام">
            <Select value={templateKey} onChange={(e) => setTemplateKey(e.target.value)}>
              <option value="">انتخاب قالب</option>
              {templates.data?.map((t) => (
                <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>
                  {templateTitle(t)} · {formatChannel(t.channel)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="کانال‌ها">
            <div className="flex flex-wrap gap-2">
              {['email', 'sms', 'push', 'webhook'].map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => toggle(c)}
                  className={`rounded-xl border px-3 py-2 text-xs transition ${
                    channels.includes(c) ? 'border-primary bg-primary/10 text-primary' : 'hover:bg-muted'
                  }`}
                >
                  {formatChannel(c)}
                </button>
              ))}
            </div>
          </Field>
          <Field label="زمان‌بندی" hint="اختیاری — خالی بگذارید تا دستی شروع شود">
            <Input type="datetime-local" value={scheduled} onChange={(e) => setScheduled(e.target.value)} />
          </Field>
          <Field label="شخصی‌سازی مشترک" hint="مقادیر اختیاری که به همه گیرنده‌ها اعمال می‌شود">
            <KeyValueEditor pairs={pairs} onChange={setPairs} />
          </Field>
          <div className="flex justify-end border-t pt-4">
            <Button onClick={() => void create()} disabled={busy || !name || !templateKey || !channels.length}>
              {busy ? <Loader2 className="animate-spin" size={15} /> : <Megaphone size={15} />}
              ایجاد کمپین
            </Button>
          </div>
        </div>
      </Dialog>

      <ConfirmDialog
        open={confirmSend}
        onOpenChange={setConfirmSend}
        title="این کمپین شروع شود؟"
        confirmLabel="بله، ارسال شروع شود"
        busy={busy}
        onConfirm={send}
        description={
          <p>
            پیام‌ها به مخاطب <strong>{campaignName || 'این کمپین'}</strong> ارسال می‌شوند.
            قبل از ادامه مطمئن شوید گیرندگان و محتوا درست هستند.
          </p>
        }
      />
      <ConfirmDialog
        open={confirmCancel}
        onOpenChange={setConfirmCancel}
        title="این کمپین لغو شود؟"
        confirmLabel="بله، لغو شود"
        destructive
        busy={busy}
        onConfirm={cancel}
        description="پیام‌های در صف که هنوز ارسال نشده‌اند متوقف می‌شوند. پیام‌های تحویل‌شده قابل بازگردانی نیستند."
      />
    </div>
  )
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
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

function Stat({ label, value, good, bad }: { label: string; value?: number; good?: boolean; bad?: boolean }) {
  return (
    <div className="rounded-xl border p-3">
      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className={`mt-1 text-lg font-semibold ${good ? 'text-emerald-600' : bad ? 'text-destructive' : ''}`}>
        {value ?? '—'}
      </div>
    </div>
  )
}
