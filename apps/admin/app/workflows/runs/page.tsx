'use client'

import { Suspense, useEffect, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { Activity, Search, GitBranch } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { ToastHost } from '@/components/toast-host'
import { useWorkflowRun, useWorkflowTimeline } from '@/hooks/use-workflow-run'
import { resourcesApi } from '@/lib/api/resources'
import { formatDateTime, formatStatus, friendlyError, statusTone } from '@/lib/ux/labels'

function WorkflowRunsContent() {
  const [input, setInput] = useState('')
  const [runId, setRunId] = useState('')
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const params = useSearchParams()

  useEffect(() => {
    const id = params.get('runId')
    if (id) {
      setInput(id)
      setRunId(id)
    }
  }, [params])

  const run = useWorkflowRun(runId)
  const timeline = useWorkflowTimeline(runId)
  const runData = (run.data ?? {}) as Record<string, unknown>
  const events: Array<Record<string, unknown>> = Array.isArray(timeline.data)
    ? (timeline.data as Array<Record<string, unknown>>)
    : Array.isArray((timeline.data as { items?: unknown[] })?.items)
      ? ((timeline.data as { items: Array<Record<string, unknown>> }).items)
      : []

  const status = String(runData.status ?? 'Unknown')
  const terminal = ['completed', 'failed', 'cancelled', 'completedwitherrors'].includes(
    status.toLowerCase().replace(/\s/g, ''),
  )

  async function cancel() {
    setBusy(true)
    try {
      await resourcesApi.workflows.cancel(runId)
      setConfirmCancel(false)
      setToast({ tone: 'success', title: 'Run cancelled' })
      void run.refetch?.()
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not cancel', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1250px]">
        <PageHeader
          eyebrow="Automation"
          title="Workflow runs"
          description="See where a journey is, what already ran, and what is next."
        />
        <Card>
          <CardContent className="p-5">
            <div className="flex gap-2">
              <Input value={input} onChange={(e) => setInput(e.target.value)} placeholder="Paste a run reference" />
              <Button disabled={!input.trim()} onClick={() => setRunId(input.trim())}>
                <Search size={16} /> Look up
              </Button>
            </div>
          </CardContent>
        </Card>

        {runId && (
          <div className="mt-5 grid gap-5 lg:grid-cols-[320px_1fr]">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center justify-between gap-2">
                  Status
                  <Badge variant={statusTone(status)}>{formatStatus(status)}</Badge>
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4 text-sm">
                <div>
                  <div className="text-xs text-muted-foreground">Recipient</div>
                  <div className="mt-1 font-medium">{String(runData.recipient ?? '—')}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Workflow</div>
                  <div className="mt-1 font-medium">{String(runData.workflowKey ?? runData.workflow ?? '—')}</div>
                </div>
                <div className="flex items-center gap-2 text-muted-foreground">
                  <Activity size={14} />
                  {terminal ? 'Finished' : 'Updating live'}
                </div>
                {!terminal && (
                  <Button variant="ghost" className="text-destructive" onClick={() => setConfirmCancel(true)}>
                    Cancel run
                  </Button>
                )}
                {run.isError && <p className="text-destructive">This run could not be loaded.</p>}
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle>Timeline</CardTitle></CardHeader>
              <CardContent>
                {events.length === 0 ? (
                  <div className="rounded-2xl border border-dashed p-10 text-center text-sm text-muted-foreground">
                    No steps recorded yet for this run.
                  </div>
                ) : (
                  <div className="space-y-3">
                    {events.map((event, i) => (
                      <div key={i} className="flex gap-4 rounded-2xl border p-4">
                        <div className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
                          <GitBranch size={15} />
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="font-medium">
                              {String(event.name ?? event.stepType ?? event.type ?? event.stepId ?? `Step ${i + 1}`)}
                            </div>
                            <Badge variant={statusTone(String(event.status ?? ''))}>
                              {formatStatus(String(event.status ?? event.state ?? ''))}
                            </Badge>
                          </div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {formatDateTime(String(event.occurredAt ?? event.createdAt ?? ''))}
                          </div>
                          {event.message != null && (
                            <p className="mt-2 text-sm text-muted-foreground">{String(event.message)}</p>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        )}
      </div>

      <ConfirmDialog
        open={confirmCancel}
        onOpenChange={setConfirmCancel}
        title="Cancel this run?"
        confirmLabel="Yes, cancel"
        destructive
        busy={busy}
        onConfirm={cancel}
        description="Remaining steps will not run. Steps already completed stay as they are."
      />
    </div>
  )
}

export default function WorkflowRunsPage() {
  return (
    <Suspense fallback={<div className="p-8 text-sm text-muted-foreground">Loading…</div>}>
      <WorkflowRunsContent />
    </Suspense>
  )
}
