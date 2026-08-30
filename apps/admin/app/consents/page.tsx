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
  const [source, setSource] = useState('admin panel')
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
        title: granted ? 'Consent recorded as granted' : 'Consent recorded as withdrawn',
      })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not record consent', description: friendlyError(e) })
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
      setToast({ tone: 'error', title: 'Could not evaluate consent', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="Audience"
          title="Consent"
          description="Record and check whether someone may receive a type of message."
        />
        <div className="grid gap-5 lg:grid-cols-2">
          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">Record consent</h2>
              <Field label="Person"><Input value={subjectId} onChange={(e) => setSubjectId(e.target.value)} placeholder="User or subject ID" /></Field>
              <Field label="Purpose">
                <Select value={purpose} onChange={(e) => setPurpose(e.target.value)} className="w-full">
                  <option value="marketing">Marketing</option>
                  <option value="transactional">Transactional</option>
                  <option value="security">Security alerts</option>
                  <option value="product">Product updates</option>
                </Select>
              </Field>
              <Field label="Channel">
                <Select value={channel} onChange={(e) => setChannel(e.target.value)} className="w-full">
                  <option value="email">Email</option>
                  <option value="sms">SMS</option>
                  <option value="push">Push</option>
                  <option value="webhook">Webhook</option>
                </Select>
              </Field>
              <div className="flex gap-2">
                <Button type="button" variant={granted ? 'default' : 'outline'} onClick={() => setGranted(true)}>Granted</Button>
                <Button type="button" variant={!granted ? 'default' : 'outline'} onClick={() => setGranted(false)}>Withdrawn</Button>
              </div>
              <Field label="Source"><Input value={source} onChange={(e) => setSource(e.target.value)} /></Field>
              <Field label="Evidence reference" hint="Optional ticket or form ID">
                <Input value={evidence} onChange={(e) => setEvidence(e.target.value)} />
              </Field>
              <Button disabled={busy || !subjectId} onClick={() => void record()}>Save consent record</Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">Can we send?</h2>
              <p className="text-sm text-muted-foreground">
                Check if this person may receive <strong>{purpose}</strong> messages on <strong>{formatChannel(channel)}</strong>.
              </p>
              <Button disabled={busy || !subjectId} variant="outline" onClick={() => void evaluate()}>
                Check permission
              </Button>
              {evalResult === 'allowed' && (
                <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4">
                  <Badge variant="success">Allowed</Badge>
                  <p className="mt-2 text-sm">Sending is permitted for this purpose and channel.</p>
                </div>
              )}
              {evalResult === 'denied' && (
                <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-4">
                  <Badge variant="danger">Not allowed</Badge>
                  <p className="mt-2 text-sm">Do not send until consent is granted for this purpose.</p>
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
