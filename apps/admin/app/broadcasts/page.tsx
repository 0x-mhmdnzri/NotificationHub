'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { ToastHost } from '@/components/toast-host'
import { KeyValueEditor, pairsToRecord, type KeyValuePair } from '@/components/key-value-editor'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { friendlyError } from '@/lib/ux/labels'
import { useTemplates } from '@/hooks/use-templates'

export default function BroadcastsPage() {
  const { tenantId } = useTenant()
  const templates = useTemplates()
  const [name, setName] = useState('')
  const [templateKey, setTemplateKey] = useState('')
  const [channel, setChannel] = useState('push')
  const [audienceKey, setAudienceKey] = useState('')
  const [recipients, setRecipients] = useState('')
  const [locale, setLocale] = useState('fa-IR')
  const [pairs, setPairs] = useState<KeyValuePair[]>([])
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  async function send() {
    setBusy(true)
    try {
      const body: Record<string, unknown> = {
        name,
        templateKey,
        channel,
        tenantId,
        locale,
        data: pairsToRecord(pairs),
      }
      if (audienceKey.trim()) body.audienceKey = audienceKey.trim()
      const lines = recipients.split('\n').map((x) => x.trim()).filter(Boolean)
      if (lines.length) body.recipients = lines
      await resourcesApi.broadcasts.send(body)
      setToast({ tone: 'success', title: 'پخش همگانی ثبت شد' })
      setConfirmOpen(false)
    } catch (e) {
      setToast({ tone: 'error', title: 'ارسال پخش همگانی ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <div className="mx-auto max-w-[900px]">
        <PageHeader
          title="پخش همگانی"
          description="یک پیام را هم‌زمان برای افراد زیادی بفرستید. قبل از ارسال با دقت بررسی کنید."
        />
        <ToastHost toast={toast} onClose={() => setToast(null)} />
        <Card>
          <CardContent className="space-y-4 p-5">
            <label className="block space-y-1 text-sm">
              <span>نام پخش همگانی</span>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Product announcement" />
            </label>
            <label className="block space-y-1 text-sm">
              <span>قالب</span>
              <Select value={templateKey} onChange={(e) => setTemplateKey(e.target.value)}>
                <option value="">انتخاب قالب</option>
                {(templates.data ?? []).map((t: { key: string }) => (
                  <option key={t.key} value={t.key}>{t.key}</option>
                ))}
              </Select>
            </label>
            <label className="block space-y-1 text-sm">
              <span>کانال</span>
              <Select value={channel} onChange={(e) => setChannel(e.target.value)}>
                <option value="push">Push</option>
                <option value="email">Email</option>
                <option value="sms">SMS</option>
              </Select>
            </label>
            <label className="block space-y-1 text-sm">
              <span>کد مخاطبان ذخیره‌شده</span>
              <Input value={audienceKey} onChange={(e) => setAudienceKey(e.target.value)} placeholder="e.g. high-value-users" />
              <span className="text-xs text-muted-foreground">اختیاری — به‌جای فهرست دستی</span>
            </label>
            <label className="block space-y-1 text-sm">
              <span>گیرندگان</span>
              <textarea
                className="min-h-[100px] w-full rounded-xl border bg-background p-3 text-sm"
                value={recipients}
                onChange={(e) => setRecipients(e.target.value)}
                placeholder="user-1\nuser-2"
              />
              <span className="text-xs text-muted-foreground">هر خط یک نفر — در صورت استفاده از مخاطبان ذخیره‌شده اختیاری است</span>
            </label>
            <label className="block space-y-1 text-sm">
              <span>زبان</span>
              <Input value={locale} onChange={(e) => setLocale(e.target.value)} />
            </label>
            <div>
              <div className="mb-2 text-sm font-medium">شخصی‌سازی مشترک</div>
              <KeyValueEditor pairs={pairs} onChange={setPairs} />
            </div>
            <div className="flex justify-end">
              <Button onClick={() => setConfirmOpen(true)} disabled={busy || !templateKey}>
                ارسال
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="این پخش همگانی ارسال شود؟"
        description={
          <div className="space-y-1 text-sm">
            <div>نام: {name || '—'}</div>
            <div>کانال: {channel}</div>
            <div>مخاطب: {audienceKey || recipients.split('\n').filter(Boolean).length + ' recipients'}</div>
          </div>
        }
        busy={busy}
        onConfirm={send}
      />
    </div>
  )
}
