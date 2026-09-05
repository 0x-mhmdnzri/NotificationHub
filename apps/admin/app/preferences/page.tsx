'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, friendlyError } from '@/lib/ux/labels'

export default function PreferencesPage() {
  const { tenantId } = useTenant()
  const [userId, setUserId] = useState('')
  const [preferredChannel, setPreferredChannel] = useState('push')
  const [start, setStart] = useState('22:00')
  const [end, setEnd] = useState('08:00')
  const [max, setMax] = useState('12')
  const [optIn, setOptIn] = useState({ push: true, email: true, sms: false, webhook: false })
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)
  const [loaded, setLoaded] = useState(false)

  async function save() {
    setBusy(true)
    try {
      await resourcesApi.preferences.save({
        userId,
        tenantId,
        preferredChannel,
        quietHoursStart: start,
        quietHoursEnd: end,
        maxPerDay: Number(max),
        timeZoneId: 'Asia/Tehran',
        channelOptIn: optIn,
        updatedAt: new Date().toISOString(),
      })
      setToast({ tone: 'success', title: 'ترجیحات ذخیره شد' })
    } catch (e) {
      setToast({ tone: 'error', title: 'ذخیره ترجیحات ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function load() {
    setBusy(true)
    try {
      const res = (await resourcesApi.preferences.get(userId, tenantId)) as Record<string, unknown>
      if (res.preferredChannel) setPreferredChannel(String(res.preferredChannel))
      if (res.quietHoursStart) setStart(String(res.quietHoursStart))
      if (res.quietHoursEnd) setEnd(String(res.quietHoursEnd))
      if (res.maxPerDay != null) setMax(String(res.maxPerDay))
      if (res.channelOptIn && typeof res.channelOptIn === 'object') {
        setOptIn((prev) => ({ ...prev, ...(res.channelOptIn as Record<string, boolean>) }))
      }
      setLoaded(true)
      setToast({ tone: 'success', title: 'ترجیحات بارگذاری شد' })
    } catch (e) {
      setToast({ tone: 'error', title: 'بارگذاری ترجیحات ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[900px]">
        <PageHeader
          eyebrow="مخاطبان"
          title="ترجیحات اعلان"
          description="نحوه و زمان دریافت پیام توسط هر فرد را کنترل کنید."
        />
        <Card>
          <CardContent className="space-y-6 p-6">
            <Field label="کاربر">
              <div className="flex gap-2">
                <Input value={userId} onChange={(e) => setUserId(e.target.value)} placeholder="شناسه کاربر" className="flex-1" />
                <Button variant="outline" disabled={!userId || busy} onClick={() => void load()}>
                  بارگذاری
                </Button>
              </div>
            </Field>

            <section>
              <h3 className="mb-3 text-sm font-semibold">کانال‌ها</h3>
              <div className="grid gap-2 sm:grid-cols-2">
                {(['push', 'email', 'sms', 'webhook'] as const).map((c) => (
                  <label key={c} className="flex cursor-pointer items-center gap-3 rounded-xl border p-3 text-sm">
                    <input
                      type="checkbox"
                      checked={!!optIn[c]}
                      onChange={(e) => setOptIn((o) => ({ ...o, [c]: e.target.checked }))}
                      className="h-4 w-4 accent-primary"
                    />
                    اجازه {formatChannel(c)}
                  </label>
                ))}
              </div>
            </section>

            <Field label="کانال ترجیحی">
              <Select value={preferredChannel} onChange={(e) => setPreferredChannel(e.target.value)} className="w-full">
                <option value="push">پوش</option>
                <option value="email">ایمیل</option>
                <option value="sms">پیامک</option>
                <option value="webhook">وب‌هوک</option>
              </Select>
            </Field>

            <section>
              <h3 className="mb-3 text-sm font-semibold">ساعات سکوت</h3>
              <p className="mb-3 text-xs text-muted-foreground">پیام‌ها در این بازه نگه داشته می‌شوند (قوانین منطقه زمانی سرور اعمال می‌شود).</p>
              <div className="grid grid-cols-2 gap-3">
                <Field label="از"><Input value={start} onChange={(e) => setStart(e.target.value)} placeholder="22:00" /></Field>
                <Field label="تا"><Input value={end} onChange={(e) => setEnd(e.target.value)} placeholder="08:00" /></Field>
              </div>
            </section>

            <Field label="سقف روزانه" hint="حداکثر پیام در روز">
              <Input type="number" min={0} value={max} onChange={(e) => setMax(e.target.value)} />
            </Field>

            <Button disabled={busy || !userId} onClick={() => void save()}>
              ذخیره ترجیحات
            </Button>
            {loaded && <p className="text-xs text-muted-foreground">آخرین مقادیر بارگذاری‌شده برای این کاربر نمایش داده می‌شود.</p>}
          </CardContent>
        </Card>
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
