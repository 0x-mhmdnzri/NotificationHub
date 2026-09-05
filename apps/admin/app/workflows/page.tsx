'use client'

import { useMemo, useState } from 'react'
import { motion, Reorder } from 'framer-motion'
import { Plus, Trash2, GitBranch, Clock, Send, Save, Play } from 'lucide-react'
import Link from 'next/link'
import { Activity } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { ToastHost } from '@/components/toast-host'
import { KeyValueEditor, pairsToRecord, type KeyValuePair } from '@/components/key-value-editor'
import { resourcesApi } from '@/lib/api/resources'
import { useTemplates } from '@/hooks/use-templates'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, friendlyError, humanizeKey, templateTitle } from '@/lib/ux/labels'
import type { WorkflowDefinition, WorkflowStep } from '@/types/api'

const stepTypes = [
  { type: 'notification', label: 'ارسال پیام', icon: Send },
  { type: 'delay', label: 'انتظار', icon: Clock },
  { type: 'condition', label: 'شاخه', icon: GitBranch },
] as const

const stepLabel = (t: string) => stepTypes.find((x) => x.type === t)?.label ?? t

export default function WorkflowsPage() {
  const { tenantId } = useTenant()
  const templates = useTemplates()
  const [name, setName] = useState('پرداخت دریافت شد')
  const [key, setKey] = useState('payment-received')
  const [active, setActive] = useState(true)
  const [steps, setSteps] = useState<WorkflowStep[]>([
    { id: 'step-1', type: 'notification', channel: 'push', templateKey: '', next: 'step-2' },
    { id: 'step-2', type: 'delay', delaySeconds: 3600, next: 'step-3' },
    { id: 'step-3', type: 'condition', conditionExpression: 'amount > 1000000', nextOnTrue: '', nextOnFalse: '' },
  ])
  const [recipient, setRecipient] = useState('')
  const [runPairs, setRunPairs] = useState<KeyValuePair[]>([{ key: 'amount', value: '' }])
  const [runId, setRunId] = useState('')
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  const payload = useMemo<WorkflowDefinition>(
    () => ({ key, tenantId, isActive: active, steps }),
    [key, tenantId, active, steps],
  )

  const add = (type: string) =>
    setSteps((s) => [...s, { id: `step-${Date.now()}`, type, channel: type === 'notification' ? 'push' : undefined }])

  const update = (id: string, p: Partial<WorkflowStep>) => setSteps((s) => s.map((x) => (x.id === id ? { ...x, ...p } : x)))
  const remove = (id: string) => setSteps((s) => s.filter((x) => x.id !== id))

  async function save() {
    setBusy(true)
    try {
      await resourcesApi.workflows.save(payload)
      setToast({ tone: 'success', title: 'ورک‌فلو ذخیره شد', description: name || humanizeKey(key) })
    } catch (e) {
      setToast({ tone: 'error', title: 'ذخیره ورک‌فلو ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function start() {
    setBusy(true)
    try {
      const r = (await resourcesApi.workflows.start({
        workflowKey: key,
        recipient,
        tenantId,
        data: pairsToRecord(runPairs),
      })) as { id?: string; runId?: string }
      const id = String(r?.id ?? r?.runId ?? '')
      setRunId(id)
      setToast({ tone: 'success', title: 'ورک‌فلو شروع شد' })
    } catch (e) {
      setToast({ tone: 'error', title: 'شروع ورک‌فلو ممکن نشد', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1500px]">
        <PageHeader
          eyebrow="اتوماسیون"
          title="ورک‌فلوها"
          description="سفرهای چندمرحله‌ای پیام بسازید: ارسال، انتظار و شاخه‌بندی."
          action={
            <Link href="/workflows/runs">
              <Button variant="outline">مشاهده اجراها</Button>
            </Link>
          }
        />

        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-card px-4 py-3">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Activity size={16} className="text-teal-600" />
            مسیر تحویل زنده — صف، افزونه‌ها، تأخیر و خطاهای قابل‌فهم
          </div>
          <Link
            href="/workflows/live"
            className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          >
            باز کردن جریان تحویل
          </Link>
        </div>

        <div className="mt-5 grid gap-5 xl:grid-cols-[1fr_360px]">
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <CardTitle>مراحل</CardTitle>
                <div className="flex flex-wrap gap-2">
                  {stepTypes.map((t) => (
                    <Button key={t.type} size="sm" variant="outline" onClick={() => add(t.type)}>
                      <Plus size={13} /> {t.label}
                    </Button>
                  ))}
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="mb-5 grid gap-3 md:grid-cols-[1fr_1fr_auto]">
                <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="نام ورک‌فلو" />
                <Input value={key} onChange={(e) => setKey(e.target.value)} placeholder="کد داخلی" />
                <Button variant={active ? 'default' : 'outline'} onClick={() => setActive((v) => !v)}>
                  {active ? 'فعال' : 'پیش‌نویس'}
                </Button>
              </div>

              <Reorder.Group axis="y" values={steps} onReorder={setSteps} className="space-y-3">
                {steps.map((s, i) => {
                  const meta = stepTypes.find((x) => x.type === s.type)
                  const Icon = meta?.icon ?? GitBranch
                  return (
                    <Reorder.Item key={s.id} value={s} as="div">
                      <motion.div layout className="rounded-2xl border bg-card p-4 shadow-sm">
                        <div className="flex items-center gap-3">
                          <div className="grid h-10 w-10 place-items-center rounded-xl bg-primary/10 text-primary">
                            <Icon size={17} />
                          </div>
                          <div className="flex-1">
                            <div className="text-sm font-medium">{stepLabel(s.type)}</div>
                            <div className="text-xs text-muted-foreground">مرحله {i + 1}</div>
                          </div>
                          <Button size="icon" variant="ghost" onClick={() => remove(s.id)}>
                            <Trash2 size={15} />
                          </Button>
                        </div>

                        <div className="mt-4 grid gap-3 md:grid-cols-2">
                          {s.type === 'notification' && (
                            <>
                              <Field label="کانال">
                                <Select value={s.channel ?? ''} onChange={(e) => update(s.id, { channel: e.target.value })}>
                                  <option value="">انتخاب کانال</option>
                                  <option value="push">پوش</option>
                                  <option value="email">ایمیل</option>
                                  <option value="sms">پیامک</option>
                                </Select>
                              </Field>
                              <Field label="قالب">
                                <Select value={s.templateKey ?? ''} onChange={(e) => update(s.id, { templateKey: e.target.value })}>
                                  <option value="">انتخاب قالب</option>
                                  {templates.data?.map((t) => (
                                    <option key={`${t.key}-${t.channel}-${t.locale}`} value={t.key}>
                                      {templateTitle(t)} · {formatChannel(t.channel)}
                                    </option>
                                  ))}
                                </Select>
                              </Field>
                            </>
                          )}
                          {s.type === 'delay' && (
                            <Field label="انتظار (دقیقه)">
                              <Input
                                type="number"
                                min={0}
                                value={Math.round((s.delaySeconds ?? 0) / 60)}
                                onChange={(e) => update(s.id, { delaySeconds: Number(e.target.value) * 60 })}
                              />
                            </Field>
                          )}
                          {s.type === 'condition' && (
                            <Field label="شرط" hint="عبارت ساده، مثلاً amount > 1000">
                              <Input
                                value={s.conditionExpression ?? ''}
                                onChange={(e) => update(s.id, { conditionExpression: e.target.value })}
                                placeholder="amount > 1000000"
                              />
                            </Field>
                          )}
                          {showAdvanced && s.type !== 'condition' && (
                            <Field label="شناسه مرحله بعدی">
                              <Input value={s.next ?? ''} onChange={(e) => update(s.id, { next: e.target.value })} />
                            </Field>
                          )}
                          {showAdvanced && s.type === 'condition' && (
                            <>
                              <Field label="اگر درست → مرحله"><Input value={s.nextOnTrue ?? ''} onChange={(e) => update(s.id, { nextOnTrue: e.target.value })} /></Field>
                              <Field label="اگر نادرست → مرحله"><Input value={s.nextOnFalse ?? ''} onChange={(e) => update(s.id, { nextOnFalse: e.target.value })} /></Field>
                            </>
                          )}
                        </div>
                      </motion.div>
                    </Reorder.Item>
                  )
                })}
              </Reorder.Group>

              <button type="button" className="mt-4 text-xs text-primary" onClick={() => setShowAdvanced((v) => !v)}>
                {showAdvanced ? 'پنهان کردن اتصال مراحل' : 'نمایش اتصال مراحل (پیشرفته)'}
              </button>

              <Button className="mt-4" disabled={busy} onClick={() => void save()}>
                <Save size={15} /> ذخیره ورک‌فلو
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>شروع برای یک نفر</CardTitle></CardHeader>
            <CardContent className="space-y-3">
              <Field label="گیرنده">
                <Input value={recipient} onChange={(e) => setRecipient(e.target.value)} placeholder="شناسه کاربر" />
              </Field>
              <Field label="مقادیر زمینه">
                <KeyValueEditor pairs={runPairs} onChange={setRunPairs} />
              </Field>
              <Button className="w-full" disabled={!recipient || busy} onClick={() => void start()}>
                <Play size={15} /> شروع سفر
              </Button>
              {runId && (
                <div className="rounded-xl border bg-muted/40 p-3 text-sm">
                  سفر شروع شد.
                  <Link className="mt-2 block text-primary" href={`/workflows/runs?runId=${encodeURIComponent(runId)}`}>
                    جزئیات اجرا
                  </Link>
                </div>
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
    <label className="block space-y-1.5 text-xs">
      <span className="font-medium text-muted-foreground">
        {label}{hint && <span className="mr-1 font-normal">· {hint}</span>}
      </span>
      {children}
    </label>
  )
}
