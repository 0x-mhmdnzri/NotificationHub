'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, friendlyError } from '@/lib/ux/labels'

export default function ConsentsPage() {
  const { tenantId } = useTenant()
  const [subjectId, setSubjectId] = useState('')
  const [purpose, setPurpose] = useState('marketing')
  const [channel, setChannel] = useState('email')
  const [granted, setGranted] = useState(true)
  const [source, setSource] = useState('پنل مدیریت')
  const [evidence, setEvidence] = useState('')
  const [evalResult, setEvalResult] = useState<'allowed' | 'denied' | null>(null)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  async function record() {
    setBusy(true)
    try {
      await resourcesApi.consents.record({
        subjectId,
        purpose,
        channel,
        granted,
        source,
        evidence: evidence || undefined,
        tenantId,
        occurredAt: new Date().toISOString(),
      })
      setToast({
        tone: 'success',
        title: granted ? 'رضایت به‌عنوان اعطاشده ثبت شد' : 'رضایت به‌عنوان لغوشده ثبت شد',
      })
    } catch (e) {
      setToast({ tone: 'error', title: 'ثبت رضایت ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function evaluate() {
    setBusy(true)
    setEvalResult(null)
    try {
      const res = (await resourcesApi.consents.evaluate({ subjectId, purpose, channel, tenantId })) as {
        allowed?: boolean
        granted?: boolean
      } | boolean
      const ok = typeof res === 'boolean' ? res : Boolean(res?.allowed ?? res?.granted)
      setEvalResult(ok ? 'allowed' : 'denied')
    } catch (e) {
      setToast({ tone: 'error', title: 'بررسی رضایت ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="مخاطبان"
          title="رضایت"
          description="ثبت و بررسی کنید که آیا فرد می‌تواند نوعی از پیام را دریافت کند."
        />
        <div className="grid gap-5 lg:grid-cols-2">
          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">ثبت رضایت</h2>
              <Field label="فرد"><Input value={subjectId} onChange={(e) => setSubjectId(e.target.value)} placeholder="شناسه کاربر یا موضوع" /></Field>
              <Field label="هدف">
                <Select value={purpose} onChange={(e) => setPurpose(e.target.value)} className="w-full">
                  <option value="marketing">بازاریابی</option>
                  <option value="transactional">تراکنشی</option>
                  <option value="security">هشدار امنیتی</option>
                  <option value="product">به‌روزرسانی محصول</option>
                </Select>
              </Field>
              <Field label="کانال">
                <Select value={channel} onChange={(e) => setChannel(e.target.value)} className="w-full">
                  <option value="email">ایمیل</option>
                  <option value="sms">پیامک</option>
                  <option value="push">پوش</option>
                  <option value="webhook">وب‌هوک</option>
                </Select>
              </Field>
              <div className="flex gap-2">
                <Button type="button" variant={granted ? 'default' : 'outline'} onClick={() => setGranted(true)}>اعطاشده</Button>
                <Button type="button" variant={!granted ? 'default' : 'outline'} onClick={() => setGranted(false)}>لغوشده</Button>
              </div>
              <Field label="منبع"><Input value={source} onChange={(e) => setSource(e.target.value)} /></Field>
              <Field label="مرجع مدرک" hint="شناسه تیکت یا فرم (اختیاری)">
                <Input value={evidence} onChange={(e) => setEvidence(e.target.value)} />
              </Field>
              <Button disabled={busy || !subjectId} onClick={() => void record()}>ذخیره رکورد رضایت</Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">آیا می‌توانیم ارسال کنیم؟</h2>
              <p className="text-sm text-muted-foreground">
                بررسی کنید این فرد می‌تواند پیام‌های <strong>{purpose}</strong> را از طریق <strong>{formatChannel(channel)}</strong> دریافت کند.
              </p>
              <Button disabled={busy || !subjectId} variant="outline" onClick={() => void evaluate()}>
                بررسی مجوز
              </Button>
              {evalResult === 'allowed' && (
                <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4">
                  <Badge variant="success">مجاز</Badge>
                  <p className="mt-2 text-sm">ارسال برای این هدف و کانال مجاز است.</p>
                </div>
              )}
              {evalResult === 'denied' && (
                <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-4">
                  <Badge variant="danger">غیرمجاز</Badge>
                  <p className="mt-2 text-sm">تا اعطای رضایت برای این هدف، ارسال نکنید.</p>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
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
