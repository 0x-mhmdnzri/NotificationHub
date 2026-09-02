'use client'

import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Activity, CheckCircle2, Send, Users, ArrowRight, Zap, AlertCircle } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { StatCard } from '@/components/stat-card'
import { SectionCard } from '@/components/section-card'
import { Progress } from '@/components/ui/progress'
import { Skeleton } from '@/components/ui/skeleton'
import { flowApi } from '@/lib/api/flow'
import { formatChannel, formatStatus } from '@/lib/ux/labels'

function formatLag(ms?: number | null) {
  if (ms == null || Number.isNaN(ms)) return '—'
  if (ms < 1000) return `${Math.round(ms)}ms`
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`
  return `${(ms / 60_000).toFixed(1)}m`
}

function statusTone(status: string) {
  const s = status.toLowerCase()
  if (['delivered', 'read'].includes(s)) return 'bg-emerald-500/10 text-emerald-600'
  if (['failed', 'deadletter', 'rejected'].includes(s)) return 'bg-destructive/10 text-destructive'
  if (['queued', 'scheduled'].includes(s)) return 'bg-amber-500/10 text-amber-600'
  return 'bg-muted text-muted-foreground'
}

export default function Dashboard() {
  const q = useQuery({
    queryKey: ['notifications', 'flow', 'dashboard'],
    queryFn: () => flowApi.snapshot(40),
    refetchInterval: 15_000,
  })

  const snap = q.data
  const loading = q.isLoading
  const totalInView =
    (snap?.queued ?? 0) + (snap?.sending ?? 0) + (snap?.delivered ?? 0) + (snap?.failed ?? 0)
  const deliveredRate =
    totalInView > 0 ? ((snap?.delivered ?? 0) / totalInView) * 100 : null

  const channelShares = useMemo(() => {
    const items = snap?.items ?? []
    if (!items.length) return [] as { channel: string; pct: number; count: number }[]
    const counts = new Map<string, number>()
    for (const it of items) {
      const c = (it.channel || 'unknown').toLowerCase()
      counts.set(c, (counts.get(c) ?? 0) + 1)
    }
    const total = items.length
    return Array.from(counts.entries())
      .map(([channel, count]) => ({ channel, count, pct: (count / total) * 100 }))
      .sort((a, b) => b.count - a.count)
  }, [snap])

  const recent = snap?.items?.slice(0, 8) ?? []
  const events = snap?.events?.slice(0, 8) ?? []

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <div className="mx-auto max-w-[1500px]">
        <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }}>
          <div className="mb-8 flex flex-col justify-between gap-5 md:flex-row md:items-center">
            <div>
              <div className="mb-2 flex items-center gap-2 text-xs font-medium text-emerald-600">
                <span className="h-2 w-2 animate-pulse rounded-full bg-emerald-500" />
                Live messaging plane
              </div>
              <h1 className="text-3xl font-bold tracking-tight">Overview</h1>
              <p className="mt-2 text-sm text-muted-foreground">
                Live delivery snapshot for this tenant — queue, in-flight, delivered, and failures.
              </p>
            </div>
            <div className="flex gap-2">
              <Link href="/workflows/live">
                <Button variant="outline">Delivery flow</Button>
              </Link>
              <Link href="/notifications">
                <Button>
                  <Send size={16} /> Send notification
                </Button>
              </Link>
            </div>
          </div>
        </motion.div>

        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {loading ? (
            <>
              {[0, 1, 2, 3].map((i) => (
                <Skeleton key={i} className="h-[120px] w-full rounded-xl" />
              ))}
            </>
          ) : (
            <>
              <StatCard
                label="In view (sample)"
                value={String(totalInView)}
                change={`${snap?.queued ?? 0} queued`}
                icon={<Send size={18} />}
              />
              <StatCard
                label="Delivery rate"
                value={deliveredRate == null ? '—' : `${deliveredRate.toFixed(1)}%`}
                change={`${snap?.delivered ?? 0} delivered`}
                icon={<CheckCircle2 size={18} />}
              />
              <StatCard
                label="Failed / dead"
                value={String(snap?.failed ?? 0)}
                change={`${snap?.sending ?? 0} sending`}
                negative={(snap?.failed ?? 0) > 0}
                icon={<Users size={18} />}
              />
              <StatCard
                label="Avg. latency"
                value={formatLag(snap?.avgLatencyMs)}
                change="from flow sample"
                icon={<Zap size={18} />}
              />
            </>
          )}
        </div>

        <div className="mt-4 grid gap-4 xl:grid-cols-[1.6fr_1fr]">
          <SectionCard title="Channel mix" subtitle="Share of recent messages by channel">
            {loading ? (
              <div className="space-y-4">
                {[0, 1, 2].map((i) => (
                  <Skeleton key={i} className="h-8 w-full" />
                ))}
              </div>
            ) : channelShares.length === 0 ? (
              <p className="py-10 text-center text-sm text-muted-foreground">No messages in the current sample.</p>
            ) : (
              <div className="space-y-5">
                {channelShares.map((c) => (
                  <div key={c.channel}>
                    <div className="mb-2 flex justify-between text-sm">
                      <span className="capitalize">{formatChannel(c.channel)}</span>
                      <span className="font-medium">
                        {c.pct.toFixed(0)}% · {c.count}
                      </span>
                    </div>
                    <Progress value={c.pct} />
                  </div>
                ))}
              </div>
            )}
          </SectionCard>

          <SectionCard title="Pipeline health" subtitle="Live stage counts">
            {loading ? (
              <div className="space-y-4">
                {[0, 1, 2, 3].map((i) => (
                  <Skeleton key={i} className="h-8 w-full" />
                ))}
              </div>
            ) : (
              <div className="space-y-5">
                {(
                  [
                    ['Queued', snap?.queued ?? 0],
                    ['Sending', snap?.sending ?? 0],
                    ['Delivered', snap?.delivered ?? 0],
                    ['Failed', snap?.failed ?? 0],
                  ] as const
                ).map(([label, value]) => (
                  <div key={label}>
                    <div className="mb-2 flex justify-between text-sm">
                      <span>{label}</span>
                      <span className="font-medium">{value}</span>
                    </div>
                    <Progress
                      value={totalInView ? (value / totalInView) * 100 : 0}
                    />
                  </div>
                ))}
                <div className="mt-2 rounded-xl bg-muted/60 p-4">
                  <div className="flex items-center gap-2 text-sm font-medium">
                    <Activity size={16} className="text-primary" />
                    Messaging health
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {(snap?.failed ?? 0) > 0
                      ? `${snap?.failed} failure(s) in the current sample — check the delivery flow log.`
                      : 'No failures in the current sample.'}
                  </p>
                </div>
              </div>
            )}
          </SectionCard>
        </div>

        <div className="mt-4 grid gap-4 xl:grid-cols-[1.35fr_.65fr]">
          <SectionCard
            title="Recent notifications"
            subtitle="Latest items from the live flow sample"
            action={
              <Link href="/workflows/live" className="flex items-center gap-1 text-xs font-medium text-primary">
                Open flow <ArrowRight size={13} />
              </Link>
            }
          >
            {loading ? (
              <div className="space-y-3">
                {[0, 1, 2, 3, 4].map((i) => (
                  <Skeleton key={i} className="h-10 w-full" />
                ))}
              </div>
            ) : recent.length === 0 ? (
              <p className="py-10 text-center text-sm text-muted-foreground">No recent notifications.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[650px] text-sm">
                  <thead className="text-left text-xs text-muted-foreground">
                    <tr>
                      <th className="pb-3 font-medium">Recipient</th>
                      <th className="pb-3 font-medium">Channel</th>
                      <th className="pb-3 font-medium">Status</th>
                      <th className="pb-3 font-medium">Provider</th>
                      <th className="pb-3 font-medium">Latency</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recent.map((r) => (
                      <tr key={r.id} className="border-t">
                        <td className="py-3 font-medium">{r.recipient}</td>
                        <td className="py-3 text-muted-foreground">{formatChannel(r.channel)}</td>
                        <td className="py-3">
                          <span className={`rounded-full px-2 py-1 text-[10px] font-medium ${statusTone(r.status)}`}>
                            {formatStatus(r.status)}
                          </span>
                        </td>
                        <td className="py-3 text-muted-foreground">{r.providerId || 'default'}</td>
                        <td className="py-3 text-muted-foreground">{formatLag(r.latencyMs)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </SectionCard>

          <SectionCard title="Activity" subtitle="Humanized delivery events">
            {loading ? (
              <div className="space-y-4">
                {[0, 1, 2, 3].map((i) => (
                  <div key={i} className="flex gap-3">
                    <Skeleton className="h-8 w-8 rounded-lg" />
                    <div className="flex-1 space-y-2">
                      <Skeleton className="h-4 w-3/4" />
                      <Skeleton className="h-3 w-1/2" />
                    </div>
                  </div>
                ))}
              </div>
            ) : events.length === 0 ? (
              <p className="py-10 text-center text-sm text-muted-foreground">No events yet.</p>
            ) : (
              <div className="space-y-5">
                {events.map((ev, i) => {
                  const icon =
                    ev.severity === 'success' ? (
                      <CheckCircle2 size={16} className="text-emerald-500" />
                    ) : ev.severity === 'error' || ev.severity === 'warn' ? (
                      <AlertCircle size={16} className="text-amber-500" />
                    ) : (
                      <Activity size={16} className="text-primary" />
                    )
                  return (
                    <div key={`${ev.at}-${i}`} className="flex gap-3">
                      <div className="mt-0.5 grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-muted">{icon}</div>
                      <div className="min-w-0 flex-1">
                        <div className="text-sm font-medium leading-snug">{ev.message}</div>
                        <div className="mt-1 text-[10px] text-muted-foreground">
                          {new Date(ev.at).toLocaleString()}
                        </div>
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
          </SectionCard>
        </div>

        {q.isError && (
          <p className="mt-4 text-sm text-destructive">
            Could not load live snapshot. Check API auth and tenant context.
          </p>
        )}
      </div>
    </div>
  )
}
