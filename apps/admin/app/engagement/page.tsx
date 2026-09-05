'use client'

import { useState } from 'react'
import { BarChart3 } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { StatCard } from '@/components/stat-card'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, formatDateTime, formatStatus, friendlyError } from '@/lib/ux/labels'

const eventLabel: Record<string, string> = {
  delivered: 'تحویل‌شده',
  opened: 'بازشده',
  clicked: 'کلیک‌شده',
  failed: 'ناموفق',
  unsubscribed: 'لغو عضویت',
  bounced: 'برگشت‌خورده',
}

export default function EngagementPage() {
  const { tenantId } = useTenant()
  const [notificationId, setNotificationId] = useState('')
  const [eventType, setEventType] = useState('opened')
  const [recipient, setRecipient] = useState('')
  const [channel, setChannel] = useState('email')
  const [range, setRange] = useState<'7d' | '30d' | 'custom'>('7d')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [stats, setStats] = useState<Record<string, number> | null>(null)
  const [events, setEvents] = useState<Array<Record<string, unknown>>>([])
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  function rangeDates() {
    const now = new Date()
    if (range === '7d') return { from: new Date(now.getTime() - 7 * 864e5).toISOString(), to: now.toISOString() }
    if (range === '30d') return { from: new Date(now.getTime() - 30 * 864e5).toISOString(), to: now.toISOString() }
    return {
      from: from ? new Date(from).toISOString() : undefined,
      to: to ? new Date(to).toISOString() : undefined,
    }
  }

  async function loadStats() {
    setBusy(true)
    try {
      const { from: f, to: t } = rangeDates()
      const res = (await resourcesApi.engagement.stats({ from: f, to: t, tenantId })) as Record<string, unknown>
      const nums: Record<string, number> = {}
      for (const [k, v] of Object.entries(res || {})) {
        if (typeof v === 'number') nums[k] = v
      }
      setStats(nums)
    } catch (e) {
      setToast({ tone: 'error', title: 'بارگذاری آمار ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function track() {
    setBusy(true)
    try {
      await resourcesApi.engagement.track({
        notificationId: notificationId || undefined,
        eventType,
        recipient,
        channel,
        tenantId,
        occurredAt: new Date().toISOString(),
      })
      setToast({ tone: 'success', title: 'رویداد ثبت شد', description: eventLabel[eventType] || eventType })
    } catch (e) {
      setToast({ tone: 'error', title: 'ثبت رویداد ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function loadEvents() {
    if (!notificationId) return
    setBusy(true)
    try {
      const res = (await resourcesApi.engagement.list(notificationId)) as Array<Record<string, unknown>>
      setEvents(Array.isArray(res) ? res : [])
    } catch (e) {
      setToast({ tone: 'error', title: 'بارگذاری رویدادها ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1200px]">
        <PageHeader
          eyebrow="تحلیل‌ها"
          title="تعامل"
          description="ببینید افراد چگونه با پیام‌های شما تعامل می‌کنند."
        />

        <div className="mb-5 flex flex-wrap items-end gap-3">
          <div className="flex gap-1 rounded-xl border bg-muted/30 p-1">
            {([['7d', '۷ روز اخیر'], ['30d', '۳۰ روز اخیر'], ['custom', 'سفارشی']] as const).map(([k, label]) => (
              <button
                key={k}
                type="button"
                onClick={() => setRange(k)}
                className={`rounded-lg px-3 py-2 text-xs font-medium ${range === k ? 'bg-background shadow-sm' : 'text-muted-foreground'}`}
              >
                {label}
              </button>
            ))}
          </div>
          {range === 'custom' && (
            <>
              <Input type="datetime-local" value={from} onChange={(e) => setFrom(e.target.value)} />
              <Input type="datetime-local" value={to} onChange={(e) => setTo(e.target.value)} />
            </>
          )}
          <Button disabled={busy} onClick={() => void loadStats()}>بروزرسانی نمای کلی</Button>
        </div>

        <div className="grid gap-4 md:grid-cols-3">
          <StatCard label="باز شدن" value={String(stats?.opened ?? stats?.opens ?? '—')} change="از بازه انتخابی" icon={<BarChart3 size={18} />} />
          <StatCard label="کلیک" value={String(stats?.clicked ?? stats?.clicks ?? '—')} change="از بازه انتخابی" icon={<BarChart3 size={18} />} />
          <StatCard label="تحویل‌شده" value={String(stats?.delivered ?? '—')} change="از بازه انتخابی" icon={<BarChart3 size={18} />} />
        </div>

        <div className="mt-5 grid gap-5 lg:grid-cols-2">
          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">ثبت رویداد</h2>
              <Field label="مرجع اعلان"><Input value={notificationId} onChange={(e) => setNotificationId(e.target.value)} /></Field>
              <Field label="چه اتفاقی افتاد">
                <Select value={eventType} onChange={(e) => setEventType(e.target.value)} className="w-full">
                  {Object.entries(eventLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                </Select>
              </Field>
              <div className="grid grid-cols-2 gap-3">
                <Field label="گیرنده"><Input value={recipient} onChange={(e) => setRecipient(e.target.value)} /></Field>
                <Field label="کانال">
                  <Select value={channel} onChange={(e) => setChannel(e.target.value)} className="w-full">
                    <option value="email">ایمیل</option>
                    <option value="sms">پیامک</option>
                    <option value="push">پوش</option>
                  </Select>
                </Field>
              </div>
              <Button disabled={busy} onClick={() => void track()}>ذخیره رویداد</Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">رویدادهای یک اعلان</h2>
              <Field label="مرجع اعلان"><Input value={notificationId} onChange={(e) => setNotificationId(e.target.value)} /></Field>
              <Button disabled={busy || !notificationId} variant="outline" onClick={() => void loadEvents()}>
                نمایش خط زمانی
              </Button>
              <div className="space-y-2">
                {events.length === 0 && <p className="text-sm text-muted-foreground">رویدادی بارگذاری نشده.</p>}
                {events.map((ev, i) => (
                  <div key={i} className="flex items-center justify-between rounded-xl border p-3 text-sm">
                    <div>
                      <Badge variant="outline">{eventLabel[String(ev.eventType)] || formatStatus(String(ev.eventType))}</Badge>
                      <div className="mt-1 text-xs text-muted-foreground">
                        {formatChannel(String(ev.channel ?? ''))} · {String(ev.recipient ?? '—')}
                      </div>
                    </div>
                    <span className="text-xs text-muted-foreground">{formatDateTime(String(ev.occurredAt ?? ''))}</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
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
