'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Play, Pause, RefreshCw } from 'lucide-react'
import { flowApi, type FlowEventDto, type FlowNodeState } from '@/lib/api/flow'
import { cn } from '@/lib/utils'
import { formatChannel, formatStatus } from '@/lib/ux/labels'
import { Button } from '@/components/ui/button'

/** Colors aligned to site palette */
const CAT: Record<string, string> = {
  trigger: '#7C6BF0',
  api: '#38BDF8',
  prep: '#84CC16',
  ai: '#A78BFA',
  success: '#14B8A6',
  retry: '#F43F5E',
}

type Wire = { from: string; to: string; kind: string; label?: string }

const WIRES: Wire[] = [
  { from: 'app', to: 'plugin', kind: 'trigger', label: 'emit' },
  { from: 'plugin', to: 'queue', kind: 'api', label: 'enqueue' },
  { from: 'queue', to: 'dispatch', kind: 'prep', label: 'worker' },
  { from: 'dispatch', to: 'delivered', kind: 'success', label: 'ok' },
  { from: 'dispatch', to: 'failed', kind: 'retry', label: 'error' },
]

/** Balanced layout — nodes have clear gaps so wire tails land on edges */
const LAYOUT: Record<string, { x: number; y: number; w: number; h: number }> = {
  app: { x: 24, y: 110, w: 168, h: 84 },
  plugin: { x: 240, y: 110, w: 188, h: 84 },
  queue: { x: 476, y: 110, w: 168, h: 84 },
  dispatch: { x: 692, y: 110, w: 168, h: 84 },
  delivered: { x: 920, y: 36, w: 168, h: 84 },
  failed: { x: 920, y: 200, w: 168, h: 84 },
}

const CANVAS_W = 1120
const CANVAS_H = 320

function formatLag(ms?: number | null) {
  if (ms == null || Number.isNaN(ms)) return '—'
  if (ms < 1000) return `${Math.round(ms)}ms`
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`
  return `${(ms / 60_000).toFixed(1)}m`
}

function statusBucket(status: string): 'queued' | 'sending' | 'delivered' | 'failed' | 'other' {
  const s = status.toLowerCase()
  if (['queued', 'scheduled'].includes(s)) return 'queued'
  if (['processing', 'sent', 'sending'].includes(s)) return 'sending'
  if (['delivered', 'read'].includes(s)) return 'delivered'
  if (['failed', 'deadletter', 'cancelled', 'suppressed', 'rejected'].includes(s)) return 'failed'
  return 'other'
}

/** Right-center of source → left-center of target with horizontal-first cubic (clean tails) */
function pathD(from: string, to: string) {
  const a = LAYOUT[from]
  const b = LAYOUT[to]
  if (!a || !b) return ''
  const p1 = { x: a.x + a.w, y: a.y + a.h / 2 }
  const p2 = { x: b.x, y: b.y + b.h / 2 }
  const dx = p2.x - p1.x
  const midX = p1.x + dx * 0.5
  // Elbow-style cubic: horizontal then vertical, endpoints sit exactly on node edges
  return `M ${p1.x} ${p1.y} C ${midX} ${p1.y}, ${midX} ${p2.y}, ${p2.x} ${p2.y}`
}

export function DeliveryFlowCanvas() {
  const [speed, setSpeed] = useState(1)
  const [playing, setPlaying] = useState(true)
  const [selectedNode, setSelectedNode] = useState<string | null>(null)
  const [selectedItem, setSelectedItem] = useState<string | null>(null)

  const q = useQuery({
    queryKey: ['notifications', 'flow'],
    queryFn: () => flowApi.snapshot(80),
    refetchInterval: 4000,
  })

  const snap = q.data
  const nodeMap = useMemo(() => {
    const m = new Map<string, FlowNodeState>()
    for (const n of snap?.nodes ?? []) m.set(n.id, n)
    return m
  }, [snap])

  const filteredItems = useMemo(() => {
    const items = snap?.items ?? []
    if (!selectedNode) return items
    if (selectedNode === 'app' || selectedNode === 'plugin') return items
    if (selectedNode === 'queue') return items.filter((i) => statusBucket(i.status) === 'queued')
    if (selectedNode === 'dispatch') return items.filter((i) => statusBucket(i.status) === 'sending')
    if (selectedNode === 'delivered') return items.filter((i) => statusBucket(i.status) === 'delivered')
    if (selectedNode === 'failed') return items.filter((i) => statusBucket(i.status) === 'failed')
    return items
  }, [snap, selectedNode])

  const filteredEvents = useMemo(() => {
    const events = snap?.events ?? []
    if (!selectedItem) return events
    return events.filter((e) => e.notificationId === selectedItem)
  }, [snap, selectedItem])

  const inspector = useMemo(() => {
    if (selectedItem) {
      const it = snap?.items.find((i) => i.id === selectedItem)
      if (it) {
        return {
          overline: formatStatus(it.status),
          title: it.recipient,
          body: [
            `Channel · ${formatChannel(it.channel)}`,
            `Plugin · ${it.providerId || 'default'}`,
            `Attempts · ${it.attemptCount}`,
            `Latency · ${formatLag(it.latencyMs)}`,
            it.errorHuman ? `Issue · ${it.errorHuman}` : null,
          ]
            .filter(Boolean)
            .join('\n'),
        }
      }
    }
    if (selectedNode) {
      const n = nodeMap.get(selectedNode)
      if (n) {
        const copy: Record<string, string> = {
          app: 'Your application is the origin of every notification in this tenant.',
          plugin: 'The delivery plugin (provider) that actually sends SMS, email, or push.',
          queue: 'Messages accepted and waiting for a worker to pick them up.',
          dispatch: 'Currently being handed to the provider — not final yet.',
          delivered: 'Confirmed delivery to the recipient device or inbox.',
          failed: 'Could not complete delivery. Humanized reason is in the log.',
        }
        return {
          overline: n.category,
          title: n.title,
          body: `${n.subtitle}\n${n.count} in view\n\n${copy[selectedNode] ?? ''}`,
        }
      }
    }
    return {
      overline: 'Overview',
      title: 'Live delivery flow',
      body: 'Select a stage or a recipient to inspect. Animation pulses while work is in flight.',
    }
  }, [selectedItem, selectedNode, nodeMap, snap])

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
          <Stat label="Queued" value={snap?.queued ?? 0} tone="prep" />
          <Stat label="Sending" value={snap?.sending ?? 0} tone="ai" />
          <Stat label="Delivered" value={snap?.delivered ?? 0} tone="success" />
          <Stat label="Failed" value={snap?.failed ?? 0} tone="retry" />
          <Stat label="Avg lag" value={formatLag(snap?.avgLatencyMs)} tone="api" />
        </div>
        <div className="flex items-center gap-2">
          {[0.5, 1, 2].map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => setSpeed(s)}
              className={cn(
                'rounded-full border px-2.5 py-1 text-xs transition',
                speed === s
                  ? 'border-primary/50 bg-primary/10 text-primary'
                  : 'text-muted-foreground hover:bg-muted/50',
              )}
            >
              {s}×
            </button>
          ))}
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => setPlaying((p) => !p)}
            className="gap-1.5"
          >
            {playing ? <Pause size={14} /> : <Play size={14} />}
            {playing ? 'Pause' : 'Play'}
          </Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => void q.refetch()} className="gap-1.5">
            <RefreshCw size={14} className={q.isFetching ? 'animate-spin' : ''} />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1fr_280px]">
        <div className="min-h-[360px] overflow-x-auto overflow-y-hidden rounded-xl border bg-card shadow-sm">
          <FlowSvg
            nodes={snap?.nodes ?? []}
            playing={playing && ((snap?.queued ?? 0) > 0 || (snap?.sending ?? 0) > 0)}
            speed={speed}
            selected={selectedNode}
            onSelect={(id) => {
              setSelectedNode(id)
              setSelectedItem(null)
            }}
          />
        </div>

        <aside className="rounded-xl border bg-card p-4 shadow-sm">
          <div className="mb-2 inline-block rounded bg-primary px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-primary-foreground">
            {inspector.overline}
          </div>
          <h3 className="text-base font-medium tracking-tight">{inspector.title}</h3>
          <p className="mt-2 whitespace-pre-line text-sm leading-relaxed text-muted-foreground">{inspector.body}</p>
        </aside>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className="rounded-xl border bg-card">
          <header className="flex items-center justify-between border-b px-4 py-3">
            <h4 className="text-sm font-medium">Recipients</h4>
            <span className="text-xs text-muted-foreground">{filteredItems.length} shown</span>
          </header>
          <ul className="max-h-72 divide-y overflow-auto text-sm">
            {filteredItems.length === 0 && (
              <li className="px-4 py-8 text-center text-muted-foreground">No messages in this stage</li>
            )}
            {filteredItems.map((it) => (
              <li key={it.id}>
                <button
                  type="button"
                  onClick={() => {
                    setSelectedItem(it.id)
                    setSelectedNode(null)
                  }}
                  className={cn(
                    'flex w-full items-start justify-between gap-3 px-4 py-3 text-left hover:bg-muted/40',
                    selectedItem === it.id && 'bg-muted/60',
                  )}
                >
                  <div className="min-w-0">
                    <div className="truncate font-medium">{it.recipient}</div>
                    <div className="mt-0.5 text-xs text-muted-foreground">
                      {formatChannel(it.channel)} · {it.providerId || 'default'}
                      {it.errorHuman ? ` · ${it.errorHuman}` : ''}
                    </div>
                  </div>
                  <div className="shrink-0 text-right">
                    <div className={cn('text-xs font-medium', toneClass(it.status))}>{formatStatus(it.status)}</div>
                    <div className="text-[10px] text-muted-foreground">{formatLag(it.latencyMs)}</div>
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </section>

        <section className="rounded-xl border bg-card">
          <header className="flex items-center justify-between border-b px-4 py-3">
            <h4 className="text-sm font-medium">Activity log</h4>
            <span className="text-xs text-muted-foreground">humanized</span>
          </header>
          <ul className="max-h-72 space-y-0 overflow-auto p-2">
            {filteredEvents.length === 0 && (
              <li className="px-3 py-8 text-center text-sm text-muted-foreground">No events yet</li>
            )}
            {filteredEvents.map((ev, i) => (
              <EventRow key={`${ev.at}-${i}`} ev={ev} onPick={() => ev.notificationId && setSelectedItem(ev.notificationId)} />
            ))}
          </ul>
        </section>
      </div>

      {q.isError && (
        <p className="text-sm text-destructive">Could not load delivery flow. Check API auth and tenant.</p>
      )}
    </div>
  )
}

function toneClass(status: string) {
  const b = statusBucket(status)
  if (b === 'delivered') return 'text-teal-600 dark:text-teal-400'
  if (b === 'failed') return 'text-destructive'
  if (b === 'queued') return 'text-lime-700 dark:text-lime-400'
  if (b === 'sending') return 'text-violet-600 dark:text-violet-400'
  return 'text-muted-foreground'
}

function Stat({ label, value, tone }: { label: string; value: string | number; tone: string }) {
  return (
    <span className="inline-flex items-center gap-2 rounded-full border bg-card px-2.5 py-1">
      <span className="h-2 w-2 rounded-full" style={{ background: CAT[tone] ?? '#94A3B8' }} />
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium text-foreground">{value}</span>
    </span>
  )
}

function EventRow({ ev, onPick }: { ev: FlowEventDto; onPick: () => void }) {
  const color =
    ev.severity === 'success'
      ? 'bg-teal-500'
      : ev.severity === 'error'
        ? 'bg-destructive'
        : ev.severity === 'warn'
          ? 'bg-amber-500'
          : 'bg-sky-500'
  return (
    <button
      type="button"
      onClick={onPick}
      className="flex w-full gap-3 rounded-lg px-2 py-2 text-left hover:bg-muted/50"
    >
      <span className={cn('mt-1.5 h-2 w-2 shrink-0 rounded-full', color)} />
      <span className="min-w-0 flex-1">
        <span className="block text-sm leading-snug">{ev.message}</span>
        <span className="text-[10px] text-muted-foreground">{new Date(ev.at).toLocaleString()}</span>
      </span>
    </button>
  )
}

function FlowSvg({
  nodes,
  playing,
  speed,
  selected,
  onSelect,
}: {
  nodes: FlowNodeState[]
  playing: boolean
  speed: number
  selected: string | null
  onSelect: (id: string) => void
}) {
  const byId = useMemo(() => new Map(nodes.map((n) => [n.id, n])), [nodes])

  const dotsRef = useRef<SVGCircleElement[]>([])
  const pathsRef = useRef<SVGPathElement[]>([])
  const raf = useRef<number>(0)

  useEffect(() => {
    if (!playing) {
      cancelAnimationFrame(raf.current)
      return
    }
    let start: number | null = null
    const tick = (now: number) => {
      if (start == null) start = now
      const t = ((now - start) * speed) / 1000
      pathsRef.current.forEach((path, i) => {
        if (!path) return
        const len = path.getTotalLength()
        if (len <= 0) return
        const cycle = (t * 0.35 + i * 0.2) % 1
        const pt = path.getPointAtLength(cycle * len)
        const dot = dotsRef.current[i]
        if (dot) {
          dot.setAttribute('cx', String(pt.x))
          dot.setAttribute('cy', String(pt.y))
        }
      })
      raf.current = requestAnimationFrame(tick)
    }
    raf.current = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf.current)
  }, [playing, speed])

  return (
    <div className="relative" style={{ width: CANVAS_W, height: CANVAS_H, minWidth: CANVAS_W }}>
      <svg width={CANVAS_W} height={CANVAS_H} className="absolute inset-0" aria-hidden>
        {WIRES.map((w, i) => {
          const d = pathD(w.from, w.to)
          const color = CAT[w.kind] ?? '#94A3B8'
          return (
            <g key={`${w.from}-${w.to}`}>
              <path
                ref={(el) => {
                  if (el) pathsRef.current[i] = el
                }}
                d={d}
                fill="none"
                stroke={color}
                strokeWidth={w.kind === 'retry' ? 1.6 : 2}
                strokeDasharray={w.kind === 'retry' ? '6 5' : undefined}
                opacity={0.9}
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                ref={(el) => {
                  if (el) dotsRef.current[i] = el
                }}
                r={3.5}
                fill={color}
                opacity={playing ? 1 : 0.35}
              />
            </g>
          )
        })}
      </svg>

      {Object.entries(LAYOUT).map(([id, box]) => {
        const n = byId.get(id)
        const accent = CAT[n?.category ?? 'trigger'] ?? '#94A3B8'
        const active = n?.active
        return (
          <button
            key={id}
            type="button"
            onClick={() => onSelect(id)}
            className={cn(
              'absolute rounded-lg border bg-card px-3 py-2.5 text-left shadow-sm transition',
              'hover:bg-muted/50',
              selected === id && 'ring-2 ring-primary/60',
              active && 'ring-1',
            )}
            style={{
              left: box.x,
              top: box.y,
              width: box.w,
              height: box.h,
              boxShadow: active ? `0 0 0 1px ${accent}` : undefined,
            }}
          >
            <span className="absolute bottom-0 left-0 top-0 w-1 rounded-l-lg" style={{ background: accent }} />
            <div className="pl-1.5">
              <div className="text-[13px] font-medium text-foreground">{n?.title ?? id}</div>
              <div className="mt-1 font-mono text-[11px] text-muted-foreground">
                {n?.subtitle ?? '—'}
                {typeof n?.count === 'number' ? ` · ${n.count}` : ''}
              </div>
            </div>
          </button>
        )
      })}
    </div>
  )
}
