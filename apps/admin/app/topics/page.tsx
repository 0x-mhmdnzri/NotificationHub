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
  const [name, setName] = useState('Product updates')
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
      setToast({ tone: 'success', title: 'Topic saved', description: name })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not save topic', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function subscribe() {
    setBusy(true)
    try {
      await resourcesApi.topics.subscribe(key, { subscriberId, channel, address: address || undefined, tenantId })
      setToast({ tone: 'success', title: 'Subscribed', description: subscriberId })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not subscribe', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function unsubscribe() {
    setBusy(true)
    try {
      await resourcesApi.topics.unsubscribe(key, subscriberId, tenantId)
      setConfirmUnsub(false)
      setToast({ tone: 'success', title: 'Unsubscribed' })
      await loadSubs()
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not unsubscribe', description: friendlyError(e) })
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
      setToast({ tone: 'error', title: 'Could not load subscribers', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="Content"
          title="Topics"
          description="Topics people can subscribe to — product updates, alerts, and more."
        />
        <div className="grid gap-5 lg:grid-cols-2">
          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">Topic</h2>
              <Field label="Name"><Input value={name} onChange={(e) => setName(e.target.value)} /></Field>
              <Field label="Code" hint="Internal identifier"><Input value={key} onChange={(e) => setKey(e.target.value)} /></Field>
              <Button disabled={busy || !key} onClick={() => void saveTopic()}>Save topic</Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">Subscription</h2>
              <Field label="Subscriber"><Input value={subscriberId} onChange={(e) => setSubscriberId(e.target.value)} placeholder="User ID" /></Field>
              <Field label="Channel">
                <Select value={channel} onChange={(e) => setChannel(e.target.value)} className="w-full">
                  <option value="push">Push</option>
                  <option value="email">Email</option>
                  <option value="sms">SMS</option>
                </Select>
              </Field>
              <Field label="Address" hint="Email or phone when needed"><Input value={address} onChange={(e) => setAddress(e.target.value)} /></Field>
              <div className="flex flex-wrap gap-2">
                <Button disabled={busy || !subscriberId} onClick={() => void subscribe()}>Subscribe</Button>
                <Button disabled={busy || !subscriberId} variant="outline" onClick={() => setConfirmUnsub(true)}>Unsubscribe</Button>
                <Button disabled={busy} variant="ghost" onClick={() => void loadSubs()}>Show subscribers</Button>
              </div>
              <div className="space-y-2">
                {subscribers.map((s, i) => (
                  <div key={i} className="flex justify-between rounded-xl border p-3 text-sm">
                    <span>{String(s.subscriberId ?? s.userId ?? s.id ?? 'Subscriber')}</span>
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
        title="Unsubscribe this person?"
        confirmLabel="Yes, unsubscribe"
        destructive
        busy={busy}
        onConfirm={unsubscribe}
        description={`They will stop receiving “${name || humanizeKey(key)}” on ${formatChannel(channel)}.`}
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
