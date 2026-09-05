'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, friendlyError, humanizeKey } from '@/lib/ux/labels'

export default function TopicsPage() {
  const { tenantId } = useTenant()
  const [key, setKey] = useState('product-updates')
  const [name, setName] = useState('به‌روزرسانی محصول')
  const [subscriberId, setSubscriberId] = useState('')
  const [channel, setChannel] = useState('push')
  const [address, setAddress] = useState('')
  const [subscribers, setSubscribers] = useState<Array<Record<string, unknown>>>([])
  const [confirmUnsub, setConfirmUnsub] = useState(false)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  async function saveTopic() {
    setBusy(true)
    try {
      await resourcesApi.topics.save({ key, name, tenantId, isActive: true })
      setToast({ tone: 'success', title: 'تاپیک ذخیره شد', description: name })
    } catch (e) {
      setToast({ tone: 'error', title: 'ذخیره تاپیک ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function subscribe() {
    setBusy(true)
    try {
      await resourcesApi.topics.subscribe(key, { subscriberId, channel, address: address || undefined, tenantId })
      setToast({ tone: 'success', title: 'عضویت ثبت شد', description: subscriberId })
    } catch (e) {
      setToast({ tone: 'error', title: 'عضویت ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function unsubscribe() {
    setBusy(true)
    try {
      await resourcesApi.topics.unsubscribe(key, subscriberId, tenantId)
      setConfirmUnsub(false)
      setToast({ tone: 'success', title: 'عضویت لغو شد' })
      await loadSubs()
    } catch (e) {
      setToast({ tone: 'error', title: 'لغو عضویت ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function loadSubs() {
    setBusy(true)
    try {
      const res = (await resourcesApi.topics.subscribers(key, tenantId)) as Array<Record<string, unknown>>
      setSubscribers(Array.isArray(res) ? res : [])
    } catch (e) {
      setToast({ tone: 'error', title: 'بارگذاری اعضا ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="محتوا"
          title="تاپیک‌ها"
          description="موضوعاتی که افراد می‌توانند در آن‌ها عضو شوند — به‌روزرسانی محصول، هشدارها و بیشتر."
        />
        <div className="grid gap-5 lg:grid-cols-2">
          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">تاپیک</h2>
              <Field label="نام"><Input value={name} onChange={(e) => setName(e.target.value)} /></Field>
              <Field label="کد" hint="شناسه داخلی"><Input value={key} onChange={(e) => setKey(e.target.value)} /></Field>
              <Button disabled={busy || !key} onClick={() => void saveTopic()}>ذخیره تاپیک</Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">عضویت</h2>
              <Field label="عضو"><Input value={subscriberId} onChange={(e) => setSubscriberId(e.target.value)} placeholder="شناسه کاربر" /></Field>
              <Field label="کانال">
                <Select value={channel} onChange={(e) => setChannel(e.target.value)} className="w-full">
                  <option value="push">پوش</option>
                  <option value="email">ایمیل</option>
                  <option value="sms">پیامک</option>
                </Select>
              </Field>
              <Field label="آدرس" hint="ایمیل یا موبایل در صورت نیاز"><Input value={address} onChange={(e) => setAddress(e.target.value)} /></Field>
              <div className="flex flex-wrap gap-2">
                <Button disabled={busy || !subscriberId} onClick={() => void subscribe()}>عضویت</Button>
                <Button disabled={busy || !subscriberId} variant="outline" onClick={() => setConfirmUnsub(true)}>لغو عضویت</Button>
                <Button disabled={busy} variant="ghost" onClick={() => void loadSubs()}>نمایش اعضا</Button>
              </div>
              <div className="space-y-2">
                {subscribers.map((s, i) => (
                  <div key={i} className="flex justify-between rounded-xl border p-3 text-sm">
                    <span>{String(s.subscriberId ?? s.userId ?? s.id ?? 'عضو')}</span>
                    <Badge variant="outline">{formatChannel(String(s.channel ?? ''))}</Badge>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      <ConfirmDialog
        open={confirmUnsub}
        onOpenChange={setConfirmUnsub}
        title="عضویت این فرد لغو شود؟"
        confirmLabel="بله، لغو شود"
        destructive
        busy={busy}
        onConfirm={unsubscribe}
        description={`دیگر «${name || humanizeKey(key)}» را از طریق ${formatChannel(channel)} دریافت نمی‌کند.`}
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
