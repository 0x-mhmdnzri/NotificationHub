'use client'

import { useState } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { ToastHost } from '@/components/toast-host'
import { KeyValueEditor, pairsToRecord, type KeyValuePair } from '@/components/key-value-editor'
import { friendlyError, humanizeKey } from '@/lib/ux/labels'
import type { SegmentRule } from '@/types/api'

const operatorLabel: Record<string, string> = {
  eq: 'equals',
  neq: 'does not equal',
  gt: 'is greater than',
  gte: 'is at least',
  lt: 'is less than',
  lte: 'is at most',
  contains: 'contains',
}

export default function SegmentsPage() {
  const { tenantId } = useTenant()
  const [name, setName] = useState('High value customers')
  const [key, setKey] = useState('high-value-users')
  const [rules, setRules] = useState<SegmentRule[]>([{ field: 'purchaseAmount', operator: 'gt', value: '5000000' }])
  const [matchAll, setMatchAll] = useState(true)
  const [testPairs, setTestPairs] = useState<KeyValuePair[]>([{ key: 'purchaseAmount', value: '7000000' }])
  const [matchResult, setMatchResult] = useState<boolean | null>(null)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  function updateRule(i: number, patch: Partial<SegmentRule>) {
    setRules((r) => r.map((x, idx) => (idx === i ? { ...x, ...patch } : x)))
  }

  async function save() {
    setBusy(true)
    try {
      await resourcesApi.segments.save({ key, tenantId, matchAll, rules })
      setToast({ tone: 'success', title: 'Audience saved', description: `“${name || humanizeKey(key)}” is ready to use.` })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not save audience', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function testMatch() {
    setBusy(true)
    setMatchResult(null)
    try {
      const res = (await resourcesApi.segments.match(key, pairsToRecord(testPairs), tenantId)) as { matched?: boolean } | boolean
      const matched = typeof res === 'boolean' ? res : Boolean(res?.matched ?? res)
      setMatchResult(matched)
      setToast({
        tone: 'success',
        title: matched ? 'This person matches' : 'This person does not match',
      })
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not test audience', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="Content"
          title="Audiences"
          description="Define who belongs in a group — for campaigns and broadcasts."
        />

        <div className="grid gap-5 lg:grid-cols-2">
          <Card>
            <CardContent className="space-y-5 p-6">
              <h2 className="font-semibold">Audience rules</h2>
              <Field label="Display name">
                <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="High value customers" />
              </Field>
              <Field label="Internal code" hint="Used by the system">
                <Input value={key} onChange={(e) => setKey(e.target.value)} />
              </Field>

              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium">People where</span>
                  <Button type="button" size="sm" variant="outline" onClick={() => setMatchAll((v) => !v)}>
                    Match {matchAll ? 'all' : 'any'} rules
                  </Button>
                </div>
                {rules.map((r, i) => (
                  <div key={i} className="rounded-xl border bg-muted/20 p-3">
                    <div className="mb-2 flex items-center justify-between text-xs text-muted-foreground">
                      <span>Rule {i + 1}{i > 0 ? ` · ${matchAll ? 'AND' : 'OR'}` : ''}</span>
                      <Button type="button" size="icon" variant="ghost" onClick={() => setRules((x) => x.filter((_, j) => j !== i))}>
                        <Trash2 size={14} />
                      </Button>
                    </div>
                    <div className="grid gap-2 sm:grid-cols-3">
                      <Input value={r.field} onChange={(e) => updateRule(i, { field: e.target.value })} placeholder="Attribute" />
                      <Select value={r.operator} onChange={(e) => updateRule(i, { operator: e.target.value })}>
                        {Object.entries(operatorLabel).map(([k, label]) => (
                          <option key={k} value={k}>{label}</option>
                        ))}
                      </Select>
                      <Input value={r.value} onChange={(e) => updateRule(i, { value: e.target.value })} placeholder="Value" />
                    </div>
                  </div>
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setRules((r) => [...r, { field: '', operator: 'eq', value: '' }])}
                >
                  <Plus size={14} /> Add rule
                </Button>
              </div>

              <div className="rounded-xl border border-dashed p-3 text-sm text-muted-foreground">
                Summary:{' '}
                {rules.map((r, i) => (
                  <span key={i}>
                    {i > 0 && <strong> {matchAll ? 'and' : 'or'} </strong>}
                    <strong>{r.field || '…'}</strong> {operatorLabel[r.operator] || r.operator}{' '}
                    <strong>{r.value || '…'}</strong>
                  </span>
                ))}
              </div>

              <Button disabled={busy || !key} onClick={() => void save()}>
                Save audience
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-5 p-6">
              <h2 className="font-semibold">Test a person</h2>
              <p className="text-sm text-muted-foreground">Enter sample attributes to see if they would be included.</p>
              <KeyValueEditor pairs={testPairs} onChange={setTestPairs} keyPlaceholder="Attribute" valuePlaceholder="Value" />
              <Button disabled={busy || !key} variant="outline" onClick={() => void testMatch()}>
                Check match
              </Button>
              {matchResult !== null && (
                <Badge variant={matchResult ? 'success' : 'danger'}>
                  {matchResult ? 'Matches this audience' : 'Does not match'}
                </Badge>
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
