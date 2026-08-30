'use client'

import { useRef, useState } from 'react'
import { Loader2, Megaphone, Play, Plus, Upload, XCircle } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/progress'
import { Dialog } from '@/components/ui/dialog'
import { EmptyState } from '@/components/ux/empty-state'
import { StatusPill } from '@/components/ux/status-pill'
import { ConfirmDialog } from '@/components/ux/confirm-dialog'
import {
  PersonalizationFields,
  pairsToRecord,
  type KvPair,
} from '@/components/ux/personalization-fields'
import { useTemplates } from '@/hooks/use-templates'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, humanTemplateName, maskId } from '@/lib/ux/labels'
import type { CreateCampaignRequest } from '@/types/api'

export default function CampaignsPage() {
  const { tenantId } = useTenant()
  const templates = useTemplates()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [templateKey, setTemplateKey] = useState('')
  const [channels, setChannels] = useState<string[]>(['email'])
  const [scheduled, setScheduled] = useState('')
  const [pairs, setPairs] = useState<KvPair[]>([{ key: '', value: '' }])
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
  const [confirmCancel, setConfirmCancel] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)

  const toggle = (c: string) =>
    setChannels((x) => (x.includes(c) ? x.filter((v) => v !== c) : [...x, c]))

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
      const res = (await resourcesApi.campaigns.create(payload)) as {
        id?: string
        campaignId?: string
      }
      setCampaignId(String(res?.id ?? res?.campaignId ?? ''))
      setCampaignName(name)
      setOpen(false)
    } finally {
      setBusy(false)
    }
  }

  const addRecipients = async () => {
    if (!campaignId || !recipients.trim()) return
    const addresses = recipients
      .split(/[\n,;]+/)
      .map((x) => x.trim())
      .filter(Boolean)
    await resourcesApi.campaigns.recipients(campaignId, { addresses, channels })
    setRecipients('')
  }

  const importCsv = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file || !campaignId) return
    const form = new FormData()
    form.append('file', file)
    await resourcesApi.campaigns.importCsv(campaignId, form)
    if (fileRef.current) fileRef.current.value = ''
  }

  const inspect = async () => {
    if (!campaignId) return
    const p = (await resourcesApi.campaigns.progress(campaignId)) as typeof progress
    setProgress(p)
  }

  const send = async () => {
    if (campaignId) await resourcesApi.campaigns.send(campaignId)
    await inspect()
  }

  const cancel = async () => {
    if (campaignId) await resourcesApi.campaigns.cancel(campaignId)
    setConfirmCancel(false)
    await inspect()
  }

  const pct = Number(progress?.percentage ?? progress?.percent ?? 0)

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <div className="mx-auto max-w-[1500px]">
        <PageHeader
          eyebrow="Send"
          title="Campaigns"
          description="Plan, schedule, and monitor large notification campaigns."
          action={
            <Button onClick={() => setOpen(true)}>
              <Plus size={16} /> New campaign
            </Button>
          }
        />

        <div className="grid gap-5 lg:grid-cols-[1fr_380px]">
          <Card>
            <CardHeader>
              <CardTitle>Campaign workspace</CardTitle>
            </CardHeader>
            <CardContent>
              {!campaignId ? (
                <EmptyState
                  icon={Megaphone}
                  title="No campaign selected"
                  description="Create a campaign, add your audience, then start sending when you are ready."
                  actionLabel="Create campaign"
                  onAction={() => setOpen(true)}
                />
              ) : (
                <div className="space-y-4 text-sm">
                  <div>
                    <div className="text-xs text-muted-foreground">Active campaign</div>
                    <div className="mt-1 text-lg font-semibold">{campaignName || 'Campaign'}</div>
                    <div className="mt-1 text-xs text-muted-foreground">Ref {maskId(campaignId)}</div>
                  </div>
                  {progress && (
                    <div className="rounded-xl border p-4">
                      <div className="mb-2 flex items-center justify-between">
                        <StatusPill status={progress.status ?? (pct >= 100 ? 'Completed' : 'Running')} />
                        <span className="text-xs text-muted-foreground">{pct}%</span>
                      </div>
                      <Progress value={pct} />
                      <div className="mt-4 grid grid-cols-2 gap-3 text-xs sm:grid-cols-4">
                        <Metric label="Total" value={progress.total} />
                        <Metric label="Processed" value={progress.processed} />
                        <Metric label="Succeeded" value={progress.successful} />
                        <Metric label="Failed" value={progress.failed} />
                      </div>
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Audience & actions</CardTitle>
            </CardHeader>
            <CardContent>
              {campaignId ? (
                <div className="space-y-5">
                  <div className="space-y-3 rounded-xl border bg-muted/20 p-4">
                    <div className="text-sm font-medium">Add recipients</div>
                    <textarea
                      value={recipients}
                      onChange={(e) => setRecipients(e.target.value)}
                      placeholder="One recipient per line"
                      className="min-h-24 w-full rounded-xl border bg-background p-3 text-sm"
                    />
                    <div className="flex flex-wrap gap-2">
                      <Button variant="outline" onClick={addRecipients} disabled={!recipients.trim()}>
                        <Plus size={14} /> Add list
                      </Button>
                      <Button variant="outline" onClick={() => fileRef.current?.click()}>
                        <Upload size={14} /> Import CSV
                      </Button>
                      <input
                        ref={fileRef}
                        type="file"
                        accept=".csv,text/csv"
                        className="hidden"
                        onChange={importCsv}
                      />
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button onClick={send}>
                      <Play size={15} /> Start campaign
                    </Button>
                    <Button variant="outline" onClick={inspect}>
                      Refresh progress
                    </Button>
                    <Button
                      variant="ghost"
                      className="text-destructive"
                      onClick={() => setConfirmCancel(true)}
                    >
                      <XCircle size={15} /> Cancel campaign
                    </Button>
                  </div>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Create a campaign to manage audience and lifecycle here.
                </p>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      <Dialog
        open={open}
        onOpenChange={setOpen}
        title="New campaign"
        description="Name the campaign, choose content and channels, then review."
      >
        <div className="space-y-5 p-5">
          <Field label="Campaign name">
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Weekend payment reminders" />
          </Field>
          <Field label="Message template">
            <Select value={templateKey} onChange={(e) => setTemplateKey(e.target.value)}>
              <option value="">Select a template</option>
              {templates.data?.map((t) => (
                <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>
                  {humanTemplateName(t.key, t.subject)} · {formatChannel(t.channel)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Channels">
            <div className="flex flex-wrap gap-2">
              {['email', 'sms', 'push', 'webhook'].map((c) => (
                <button
                  key={c}
                  type="button"
                  onClick={() => toggle(c)}
                  className={`rounded-xl border px-3 py-2 text-xs transition ${
                    channels.includes(c)
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'hover:bg-muted'
                  }`}
                >
                  {formatChannel(c)}
                </button>
              ))}
            </div>
          </Field>
          <Field label="Schedule (optional)">
            <Input type="datetime-local" value={scheduled} onChange={(e) => setScheduled(e.target.value)} />
          </Field>
          <Field label="Personalization">
            <PersonalizationFields pairs={pairs} onChange={setPairs} />
          </Field>
          <div className="flex justify-end border-t pt-4">
            <Button onClick={create} disabled={busy || !name || !templateKey || !channels.length}>
              {busy ? <Loader2 className="animate-spin" size={15} /> : <Megaphone size={15} />}
              Create campaign
            </Button>
          </div>
        </div>
      </Dialog>

      <ConfirmDialog
        open={confirmCancel}
        onOpenChange={setConfirmCancel}
        title="Cancel this campaign?"
        description={`Stopping “${campaignName || 'this campaign'}” will prevent remaining messages from being sent. Messages already delivered cannot be recalled.`}
        confirmLabel="Cancel campaign"
        destructive
        onConfirm={cancel}
      />
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium">{label}</span>
      {children}
    </label>
  )
}

function Metric({ label, value }: { label: string; value?: number }) {
  return (
    <div>
      <div className="text-muted-foreground">{label}</div>
      <div className="text-sm font-semibold">{value ?? '—'}</div>
    </div>
  )
}
