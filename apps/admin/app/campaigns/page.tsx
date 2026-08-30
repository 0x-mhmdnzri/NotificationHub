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
      setToast({ tone: 'success', title: 'Campaign created', description: `“${name}” is ready for recipients.` })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not create campaign', description: friendlyError(e) })
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
      setToast({ tone: 'success', title: `${addresses.length} recipients added` })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not add recipients', description: friendlyError(e) })
    }
  }

  const importCsv = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file || !campaignId) return
    const form = new FormData()
    form.append('file', file)
    try {
      await resourcesApi.campaigns.importCsv(campaignId, form)
      setToast({ tone: 'success', title: 'Import started', description: file.name })
    } catch (err) {
      setToast({ tone: 'error', title: 'Import failed', description: friendlyError(err) })
    }
    if (fileRef.current) fileRef.current.value = ''
  }

  const inspect = async () => {
    if (!campaignId) return
    try {
      const p = (await resourcesApi.campaigns.progress(campaignId)) as typeof progress
      setProgress(p)
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not refresh progress', description: friendlyError(e) })
    }
  }

  const send = async () => {
    if (!campaignId) return
    setBusy(true)
    try {
      await resourcesApi.campaigns.send(campaignId)
      setConfirmSend(false)
      setToast({ tone: 'success', title: 'Campaign started' })
      await inspect()
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not start campaign', description: friendlyError(e) })
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
      setToast({ tone: 'success', title: 'Campaign cancelled' })
      await inspect()
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not cancel', description: friendlyError(e) })
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
          eyebrow="Send"
          title="Campaigns"
          description="Reach many people with one message. Build the audience, review, then start."
          action={
            <Button onClick={() => setOpen(true)}>
              <Plus size={16} /> New campaign
            </Button>
          }
        />

        <div className="grid gap-5 lg:grid-cols-[1fr_380px]">
          <Card>
            <CardHeader>
              <CardTitle>{campaignName || 'Campaign workspace'}</CardTitle>
            </CardHeader>
            <CardContent>
              {!campaignId ? (
                <div className="rounded-2xl border border-dashed p-12 text-center">
                  <div className="mx-auto grid h-12 w-12 place-items-center rounded-2xl bg-primary/10 text-primary">
                    <Megaphone size={22} />
                  </div>
                  <h3 className="mt-4 font-semibold">No campaign selected</h3>
                  <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">
                    Create a campaign, add people by list or spreadsheet, then start sending when you are ready.
                  </p>
                  <Button className="mt-5" onClick={() => setOpen(true)}>
                    <Plus size={15} /> Create campaign
                  </Button>
                </div>
              ) : (
                <div className="space-y-6">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={statusTone(progress?.status)}>{formatStatus(progress?.status) || 'Ready'}</Badge>
                    <span className="text-xs text-muted-foreground">Working campaign</span>
                  </div>

                  <div className="rounded-xl border bg-muted/20 p-4">
                    <div className="mb-3 text-sm font-medium">Audience</div>
                    <textarea
                      value={recipients}
                      onChange={(e) => setRecipients(e.target.value)}
                      placeholder="One recipient per line (user ID, email, or phone)"
                      className="min-h-28 w-full rounded-xl border bg-background p-3 text-sm"
                    />
                    <div className="mt-3 flex flex-wrap gap-2">
                      <Button variant="outline" onClick={() => void addRecipients()} disabled={!recipients.trim()}>
                        <Plus size={14} /> Add list
                      </Button>
                      <Button variant="outline" onClick={() => fileRef.current?.click()}>
                        <Upload size={14} /> Import spreadsheet
                      </Button>
                      <input ref={fileRef} type="file" accept=".csv,text/csv" className="hidden" onChange={(e) => void importCsv(e)} />
                    </div>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <Button onClick={() => setConfirmSend(true)}>
                      <Play size={15} /> Start campaign
                    </Button>
                    <Button variant="outline" onClick={() => void inspect()}>
                      Refresh progress
                    </Button>
                    <Button variant="ghost" className="text-destructive" onClick={() => setConfirmCancel(true)}>
                      <XCircle size={15} /> Cancel campaign
                    </Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Progress</CardTitle>
            </CardHeader>
            <CardContent>
              {!campaignId && <p className="text-sm text-muted-foreground">Progress appears after you create a campaign.</p>}
              {campaignId && !progress && (
                <p className="text-sm text-muted-foreground">Press “Refresh progress” to load the latest numbers.</p>
              )}
              {progress && (
                <div className="space-y-4">
                  <div>
                    <div className="mb-2 flex justify-between text-xs">
                      <span>Completion</span>
                      <span className="font-medium">{pct}%</span>
                    </div>
                    <Progress value={pct} />
                  </div>
                  <div className="grid grid-cols-2 gap-3 text-sm">
                    <Stat label="Total" value={progress.total} />
                    <Stat label="Processed" value={progress.processed} />
                    <Stat label="Succeeded" value={progress.successful} good />
                    <Stat label="Failed" value={progress.failed} bad />
                    <Stat label="Waiting" value={progress.pending} />
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
        title="New campaign"
        description="Name the campaign, pick a template and channels, then add your audience."
      >
        <div className="space-y-5 p-5">
          <Field label="Campaign name">
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Weekend payment reminders" />
          </Field>
          <Field label="Message template">
            <Select value={templateKey} onChange={(e) => setTemplateKey(e.target.value)}>
              <option value="">Choose a template</option>
              {templates.data?.map((t) => (
                <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>
                  {templateTitle(t)} · {formatChannel(t.channel)}
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
                    channels.includes(c) ? 'border-primary bg-primary/10 text-primary' : 'hover:bg-muted'
                  }`}
                >
                  {formatChannel(c)}
                </button>
              ))}
            </div>
          </Field>
          <Field label="Schedule" hint="Optional — leave empty to start manually">
            <Input type="datetime-local" value={scheduled} onChange={(e) => setScheduled(e.target.value)} />
          </Field>
          <Field label="Shared personalization" hint="Optional values applied to all recipients">
            <KeyValueEditor pairs={pairs} onChange={setPairs} />
          </Field>
          <div className="flex justify-end border-t pt-4">
            <Button onClick={() => void create()} disabled={busy || !name || !templateKey || !channels.length}>
              {busy ? <Loader2 className="animate-spin" size={15} /> : <Megaphone size={15} />}
              Create campaign
            </Button>
          </div>
        </div>
      </Dialog>

      <ConfirmDialog
        open={confirmSend}
        onOpenChange={setConfirmSend}
        title="Start this campaign?"
        confirmLabel="Yes, start sending"
        busy={busy}
        onConfirm={send}
        description={
          <p>
            Messages will begin going out to the audience of <strong>{campaignName || 'this campaign'}</strong>.
            Make sure recipients and content are correct before continuing.
          </p>
        }
      />
      <ConfirmDialog
        open={confirmCancel}
        onOpenChange={setConfirmCancel}
        title="Cancel this campaign?"
        confirmLabel="Yes, cancel it"
        destructive
        busy={busy}
        onConfirm={cancel}
        description="Queued messages that have not been sent yet will stop. Messages already delivered cannot be recalled."
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
