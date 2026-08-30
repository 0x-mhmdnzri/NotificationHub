'use client'

import { useState } from 'react'
import { Activity, CheckCircle2, Search, XCircle } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useNotificationStatus } from '@/hooks/use-notification-status'
import { formatChannel, formatDateTime, formatStatus, statusTone } from '@/lib/ux/labels'

export default function StatusPage() {
  const [id, setId] = useState('')
  const [query, setQuery] = useState('')
  const q = useNotificationStatus(query || undefined)
  const data = (q.data ?? {}) as Record<string, unknown>
  const status = String(data.status ?? 'Unknown')
  const terminal = ['delivered', 'failed', 'cancelled', 'rejected'].includes(status.toLowerCase())

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="Operations"
          title="Delivery status"
          description="Look up a single message and see whether it was delivered."
        />
        <Card>
          <CardContent className="p-5">
            <div className="flex gap-2">
              <Input value={id} onChange={(e) => setId(e.target.value)} placeholder="Message reference" />
              <Button onClick={() => setQuery(id.trim())} disabled={!id.trim()}>
                <Search size={16} /> Look up
              </Button>
            </div>
          </CardContent>
        </Card>

        {query && (
          <div className="mt-5 grid gap-5 md:grid-cols-[1fr_280px]">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle>Message</CardTitle>
                  <Badge variant={statusTone(status)}>{formatStatus(status)}</Badge>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex items-center gap-4 py-6">
                  <StateIcon status={status} />
                  <div>
                    <div className="text-xl font-semibold">{formatStatus(status)}</div>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {q.isFetching
                        ? 'Refreshing…'
                        : q.isError
                          ? 'This message could not be found or loaded.'
                          : terminal
                            ? 'Final state'
                            : 'Still in progress — this view updates automatically'}
                    </p>
                  </div>
                </div>
                <dl className="grid gap-3 border-t pt-4 text-sm sm:grid-cols-2">
                  <Row label="Recipient" value={String(data.recipient ?? '—')} />
                  <Row label="Channel" value={formatChannel(String(data.channel ?? ''))} />
                  <Row label="Template" value={String(data.templateKey ?? data.template ?? '—')} />
                  <Row label="Created" value={formatDateTime(String(data.createdAt ?? data.scheduledAt ?? ''))} />
                  <Row label="Provider" value={String(data.provider ?? data.preferredProvider ?? '—')} />
                </dl>
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle>Tracking</CardTitle></CardHeader>
              <CardContent className="space-y-3 text-sm text-muted-foreground">
                <p>{terminal ? 'Updates stopped — delivery finished.' : 'Checking every few seconds for changes.'}</p>
              </CardContent>
            </Card>
          </div>
        )}
      </div>
    </div>
  )
}

function StateIcon({ status }: { status: string }) {
  const s = status.toLowerCase()
  if (s === 'delivered') return <CheckCircle2 className="text-emerald-500" size={38} />
  if (['failed', 'rejected', 'cancelled'].includes(s)) return <XCircle className="text-destructive" size={38} />
  return <Activity className="animate-pulse text-primary" size={38} />
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 font-medium">{value || '—'}</dd>
    </div>
  )
}
