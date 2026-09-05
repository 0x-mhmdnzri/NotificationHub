'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { isAllowedHttpsUrl } from '@/lib/api/config'
import { useTenant } from '@/providers/tenant-provider'
import { friendlyError } from '@/lib/ux/labels'

const EVENT_OPTIONS = [
  { id: 'notification.sent', label: 'پیام پذیرفته شد' },
  { id: 'notification.delivered', label: 'پیام تحویل شد' },
  { id: 'notification.failed', label: 'پیام ناموفق بود' },
  { id: 'notification.opened', label: 'پیام باز شد' },
]

export default function WebhooksPage() {
  const { tenantId } = useTenant()
  const [url, setUrl] = useState('')
  const [secret, setSecret] = useState('')
  const [events, setEvents] = useState<string[]>([
    'notification.sent',
    'notification.delivered',
    'notification.failed',
  ])
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  function toggle(id: string) {
    setEvents((e) => (e.includes(id) ? e.filter((x) => x !== id) : [...e, id]))
  }

  async function create() {
    if (!isAllowedHttpsUrl(url)) {
      setToast({
        tone: 'error',
        title: 'آدرس نامعتبر',
        description: 'از یک URL عمومی https:// استفاده کنید. localhost و شبکه خصوصی مجاز نیستند.',
      })
      return
    }
    setBusy(true)
    try {
      await resourcesApi.webhooks.create({
        url,
        secret: secret || undefined,
        events,
        tenantId,
        isActive: true,
      })
      setToast({
        tone: 'success',
        title: 'وب‌هوک متصل شد',
        description: 'نقطه پایانی شما رویدادهای انتخاب‌شده را دریافت می‌کند.',
      })
      setSecret('')
    } catch (e) {
      setToast({ tone: 'error', title: 'ایجاد وب‌هوک ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[800px]">
        <PageHeader
          eyebrow="یکپارچه‌سازی"
          title="وب‌هوک‌ها"
          description="وقتی رویدادهای تحویل رخ می‌دهد، سیستم‌های خود را مطلع کنید."
        />
        <Card>
          <CardContent className="space-y-5 p-6">
            <Field label="آدرس نقطه پایانی شما">
              <Input
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://api.yourapp.com/hooks/notifications"
              />
            </Field>
            <Field label="رمز امضا" hint="فقط هنگام ایجاد نمایش داده می‌شود — محرمانه نگه دارید">
              <Input
                type="password"
                value={secret}
                onChange={(e) => setSecret(e.target.value)}
                autoComplete="new-password"
              />
            </Field>
            <div>
              <div className="mb-2 text-sm font-medium">رویدادهای ارسالی</div>
              <div className="space-y-2">
                {EVENT_OPTIONS.map((ev) => (
                  <label key={ev.id} className="flex cursor-pointer items-center gap-3 rounded-xl border p-3 text-sm">
                    <input
                      type="checkbox"
                      checked={events.includes(ev.id)}
                      onChange={() => toggle(ev.id)}
                      className="h-4 w-4 accent-primary"
                    />
                    {ev.label}
                  </label>
                ))}
              </div>
            </div>
            <Button disabled={busy || !url || !events.length} onClick={() => void create()}>
              اتصال وب‌هوک
            </Button>
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
