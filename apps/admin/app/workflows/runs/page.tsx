'use client'

import { Suspense, useEffect, useState } from 'react'
import { useSearchParams } from 'next/navigation'
import { Activity, Clock3, Search, GitBranch } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useWorkflowRun, useWorkflowTimeline } from '@/hooks/use-workflow-run'

function WorkflowRunsContent() {
  const [input, setInput] = useState('')
  const [runId, setRunId] = useState('')
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
  const runData: any = run.data ?? {}
  const events: any[] = Array.isArray(timeline.data)
    ? timeline.data
    : Array.isArray((timeline.data as any)?.items)
      ? (timeline.data as any).items
      : []
  const status = String(runData.status ?? 'Unknown')
  const terminal = ['completed', 'failed', 'cancelled', 'completedwitherrors'].includes(
    status.toLowerCase().replace(/\s/g, '')
  )
  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <div className="mx-auto max-w-[1250px]">
        <PageHeader
          eyebrow="Orchestration / Operations"
          title="Workflow run inspector"
          description="Inspect execution through the WorkflowRun and Timeline endpoints exposed by the backend contract."
        />
        <Card>
          <CardContent className="p-5">
            <div className="flex gap-2">
              <Input
                className="font-mono"
                value={input}
                onChange={(e) => setInput(e.target.value)}
                placeholder="Workflow run UUID"
              />
              <Button disabled={!input.trim()} onClick={() => setRunId(input.trim())}>
                <Search size={16} />
                Inspect run
              </Button>
            </div>
          </CardContent>
        </Card>
        {runId && (
          <div className="mt-5 grid gap-5 lg:grid-cols-[340px_1fr]">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center justify-between">
                  Run{' '}
                  <Badge
                    variant={
                      terminal ? (status.toLowerCase() === 'completed' ? 'success' : 'danger') : 'warning'
                    }
                  >
                    {status}
                  </Badge>
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-5 text-sm">
                <div>
                  <div className="text-xs text-muted-foreground">Run ID</div>
                  <div className="mt-1 break-all font-mono text-xs">{runId}</div>
                </div>
                <Row icon={Activity} label="Run endpoint" value="GET /api/v1/workflows/runs/{runId}" />
                <Row icon={Clock3} label="Timeline" value="GET /api/v1/workflows/runs/{runId}/timeline" />
                <Row icon={Activity} label="Refresh" value={terminal ? 'Stopped' : '2.5s'} />
                {run.isError && (
                  <p className="text-destructive">The backend did not return a successful run response.</p>
                )}
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>Execution timeline</CardTitle>
              </CardHeader>
              <CardContent>
                {events.length === 0 ? (
                  <div className="rounded-2xl border border-dashed p-10 text-center text-sm text-muted-foreground">
                    No timeline items returned by the API yet.
                  </div>
                ) : (
                  <div className="relative space-y-3">
                    {events.map((event: any, i) => (
                      <div key={i} className="flex gap-4 rounded-2xl border p-4">
                        <div className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
                          <GitBranch size={15} />
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="font-medium">
                              {String(event.stepId ?? event.id ?? event.type ?? `Event ${i + 1}`)}
                            </div>
                            <span className="text-xs text-muted-foreground">
                              {String(event.occurredAt ?? event.createdAt ?? '')}
                            </span>
                          </div>
                          <pre className="mt-2 overflow-auto rounded-xl bg-muted p-3 text-[11px]">
                            {JSON.stringify(event, null, 2)}
                          </pre>
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
    </div>
  )
}

export default function WorkflowRunsPage() {
  return (
    <Suspense fallback={<div className="p-8 text-sm text-muted-foreground">Loading run inspector…</div>}>
      <WorkflowRunsContent />
    </Suspense>
  )
}

function Row({ icon: Icon, label, value }: { icon: any; label: string; value: string }) {
  return (
    <div className="flex items-start gap-3">
      <Icon size={16} className="mt-0.5 text-muted-foreground" />
      <div>
        <div className="text-xs text-muted-foreground">{label}</div>
        <div className="mt-0.5 font-medium">{value}</div>
      </div>
    </div>
  )
}
