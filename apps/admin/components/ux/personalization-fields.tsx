'use client'

import { Plus, Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

export type KvPair = { key: string; value: string }

export function pairsToRecord(pairs: KvPair[]): Record<string, string> {
  const out: Record<string, string> = {}
  for (const p of pairs) {
    const k = p.key.trim()
    if (k) out[k] = p.value
  }
  return out
}

export function recordToPairs(data?: Record<string, unknown> | null): KvPair[] {
  if (!data || !Object.keys(data).length) return [{ key: '', value: '' }]
  return Object.entries(data).map(([key, value]) => ({ key, value: String(value ?? '') }))
}

/** Key/value personalization — never a JSON textarea for ordinary operators. */
export function PersonalizationFields({
  pairs,
  onChange,
  hint = 'Values that fill placeholders in the template (for example amount or name).',
}: {
  pairs: KvPair[]
  onChange: (next: KvPair[]) => void
  hint?: string
}) {
  function update(i: number, patch: Partial<KvPair>) {
    onChange(pairs.map((p, idx) => (idx === i ? { ...p, ...patch } : p)))
  }

  return (
    <div className="space-y-3">
      <p className="text-xs text-muted-foreground">{hint}</p>
      {pairs.map((p, i) => (
        <div key={i} className="flex gap-2">
          <Input
            placeholder="Field name"
            value={p.key}
            onChange={(e) => update(i, { key: e.target.value })}
            className="flex-1"
          />
          <Input
            placeholder="Value"
            value={p.value}
            onChange={(e) => update(i, { value: e.target.value })}
            className="flex-[1.4]"
          />
          <Button
            type="button"
            size="icon"
            variant="ghost"
            disabled={pairs.length <= 1}
            onClick={() => onChange(pairs.filter((_, idx) => idx !== i))}
          >
            <Trash2 size={14} />
          </Button>
        </div>
      ))}
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => onChange([...pairs, { key: '', value: '' }])}
      >
        <Plus size={14} /> Add field
      </Button>
    </div>
  )
}
