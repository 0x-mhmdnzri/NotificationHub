'use client'

import { useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { FileText, Plus, Search, SlidersHorizontal, Sparkles } from 'lucide-react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { TemplateEditor } from '@/components/template-editor'
import { useTemplates } from '@/hooks/use-templates'
import { formatChannel, templateTitle } from '@/lib/ux/labels'
import type { TemplateDefinition } from '@/types/api'

export default function TemplatesPage() {
  const [channel, setChannel] = useState('all')
  const [q, setQ] = useState('')
  const [open, setOpen] = useState(false)
  const [selected, setSelected] = useState<TemplateDefinition>()
  const result = useTemplates(channel === 'all' ? undefined : channel)

  const rows = useMemo(() => {
    const data = result.data ?? []
    return data.filter(
      (x) =>
        (channel === 'all' || x.channel === channel) &&
        `${templateTitle(x)} ${x.key} ${x.locale}`.toLowerCase().includes(q.toLowerCase()),
    )
  }, [result.data, channel, q])

  const loading = result.isLoading

  const edit = (x: TemplateDefinition) => {
    setSelected(x)
    setOpen(true)
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <div className="mx-auto max-w-[1500px]">
        <PageHeader
          eyebrow="Content"
          title="Templates"
          description="Reusable message content for notifications and campaigns."
          action={
            <Button
              onClick={() => {
                setSelected(undefined)
                setOpen(true)
              }}
            >
              <Plus size={16} /> New template
            </Button>
          }
        />

        <div className="mb-5 grid gap-3 md:grid-cols-3">
          {loading ? (
            <>
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-[72px] w-full rounded-xl" />
              ))}
            </>
          ) : (
            <>
              <Metric icon={FileText} label="Templates" value={String(rows.length)} />
              <Metric icon={Sparkles} label="Active" value={String(rows.filter((x) => x.isActive !== false).length)} />
              <Metric icon={SlidersHorizontal} label="Channels" value={String(new Set(rows.map((x) => x.channel)).size)} />
            </>
          )}
        </div>

        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="flex flex-col gap-3 border-b p-4 lg:flex-row">
              <div className="relative flex-1">
                <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <Input
                  className="pl-9"
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                  placeholder="Search by name or language…"
                />
              </div>
              <div className="flex gap-1 rounded-xl border bg-muted/30 p-1">
                {['all', 'email', 'sms', 'push', 'webhook'].map((x) => (
                  <button
                    key={x}
                    type="button"
                    onClick={() => setChannel(x)}
                    className={`rounded-lg px-3 py-2 text-xs font-medium transition ${
                      channel === x ? 'bg-background text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    {x === 'all' ? 'All' : formatChannel(x)}
                  </button>
                ))}
              </div>
            </div>

            <div className="hidden grid-cols-[1.6fr_.7fr_.7fr_.45fr_.5fr_auto] gap-4 border-b bg-muted/20 px-5 py-3 text-[10px] font-semibold uppercase tracking-widest text-muted-foreground md:grid">
              <span>Name</span>
              <span>Channel</span>
              <span>Language</span>
              <span>Version</span>
              <span>Status</span>
              <span />
            </div>

            <div>
              {loading && (
                <div className="space-y-0">
                  {[0, 1, 2, 3, 4, 5].map((i) => (
                    <div key={i} className="border-b px-5 py-4">
                      <Skeleton className="h-12 w-full" />
                    </div>
                  ))}
                </div>
              )}

              {!loading &&
                rows.map((x, i) => (
                  <motion.button
                    key={`${x.key}-${x.channel}-${x.locale}`}
                    type="button"
                    onClick={() => edit(x)}
                    initial={{ opacity: 0, y: 6 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: i * 0.025 }}
                    className="grid w-full gap-2 border-b px-5 py-4 text-left transition hover:bg-muted/30 md:grid-cols-[1.6fr_.7fr_.7fr_.45fr_.5fr_auto] md:items-center"
                  >
                    <div>
                      <div className="text-sm font-semibold">{templateTitle(x)}</div>
                      <div className="mt-1 truncate text-xs text-muted-foreground">{x.subject || 'No subject'}</div>
                    </div>
                    <div>
                      <Badge variant="outline">{formatChannel(x.channel)}</Badge>
                    </div>
                    <div className="text-sm text-muted-foreground">{x.locale}</div>
                    <div className="text-sm">v{x.version ?? 1}</div>
                    <div>
                      <Badge variant={x.isActive === false ? 'default' : 'success'}>
                        {x.isActive === false ? 'Inactive' : 'Active'}
                      </Badge>
                    </div>
                    <div className="text-xs text-muted-foreground">Edit</div>
                  </motion.button>
                ))}

              {!loading && !rows.length && (
                <div className="p-12 text-center text-sm text-muted-foreground">
                  {result.isError
                    ? 'Could not load templates. Check API auth and tenant.'
                    : 'No templates yet. Create one to get started.'}
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      </div>
      <TemplateEditor open={open} onOpenChange={setOpen} initial={selected} />
    </div>
  )
}

function Metric({ icon: Icon, label, value }: { icon: React.ComponentType<{ size?: number }>; label: string; value: string }) {
  return (
    <Card>
      <CardContent className="flex items-center gap-3 p-4">
        <div className="grid h-9 w-9 place-items-center rounded-xl bg-primary/10 text-primary">
          <Icon size={17} />
        </div>
        <div>
          <div className="text-xs text-muted-foreground">{label}</div>
          <div className="text-lg font-semibold">{value}</div>
        </div>
      </CardContent>
    </Card>
  )
}
