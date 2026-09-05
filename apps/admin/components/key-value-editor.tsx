'use client'

import { Plus, Trash2 } from 'lucide-react'
import { Button } from './ui/button'
import { Input } from './ui/input'

export type KeyValuePair = { key: string; value: string }

/** Human key/value personalization — never a JSON textarea as primary input. */
export function KeyValueEditor({
  pairs,
  onChange,
  keyPlaceholder = 'نام فیلد',
  valuePlaceholder = 'مقدار',
}: {
  pairs: KeyValuePair[]
  onChange: (next: KeyValuePair[]) => void
  keyPlaceholder?: string
  valuePlaceholder?: string
}) {
  function update(i: number, patch: Partial<KeyValuePair>) {
    onChange(pairs.map((p, idx) => (idx === i ? { ...p, ...patch } : p)))
  }

  function remove(i: number) {
    onChange(pairs.filter((_, idx) => idx !== i))
  }

  function add() {
    onChange([...pairs, { key: '', value: '' }])
  }

  return (
    <div className="space-y-2">
      {pairs.length === 0 && (
        <p className="rounded-xl border border-dashed p-4 text-center text-xs text-muted-foreground">
          هنوز فیلد شخصی‌سازی نیست. فیلدهایی که قالب انتظار دارد اضافه کنید (مثلاً amount یا name).
        </p>
      )}
      {pairs.map((p, i) => (
        <div key={i} className="flex gap-2">
          <Input
            value={p.key}
            onChange={(e) => update(i, { key: e.target.value })}
            placeholder={keyPlaceholder}
            className="w-[40%]"
          />
          <Input
            value={p.value}
            onChange={(e) => update(i, { value: e.target.value })}
            placeholder={valuePlaceholder}
            className="flex-1"
          />
          <Button type="button" size="icon" variant="ghost" onClick={() => remove(i)} aria-label="حذف فیلد">
            <Trash2 size={15} />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" onClick={add}>
        <Plus size={14} /> افزودن فیلد
      </Button>
    </div>
  )
}

export function pairsToRecord(pairs: KeyValuePair[]): Record<string, string> {
  const out: Record<string, string> = {}
  for (const p of pairs) {
    const k = p.key.trim()
    if (k) out[k] = p.value
  }
  return out
}

export function recordToPairs(data?: Record<string, unknown> | null): KeyValuePair[] {
  if (!data) return []
  return Object.entries(data).map(([key, value]) => ({
    key,
    value: value == null ? '' : String(value),
  }))
}
