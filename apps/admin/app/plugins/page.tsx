'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { formatStatus, friendlyError, statusTone } from '@/lib/ux/labels'

export default function PluginsPage() {
  const [plugins, setPlugins] = useState<Array<Record<string, unknown>>>([])
  const [health, setHealth] = useState<Record<string, unknown> | null>(null)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  async function loadPlugins() {
    setBusy(true)
    try {
      const res = (await resourcesApi.plugins()) as Array<Record<string, unknown>>
      setPlugins(Array.isArray(res) ? res : [])
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not load integrations', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function checkHealth() {
    setBusy(true)
    try {
      const res = (await resourcesApi.messagingHealth()) as Record<string, unknown>
      setHealth(res)
      setToast({ tone: 'success', title: 'Health check complete' })
    } catch (e) {
      setToast({ tone: 'error', title: 'Health check failed', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  const overall = String(health?.status ?? health?.overall ?? health?.State ?? '')

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1000px]">
        <PageHeader
          eyebrow="Integrations"
          title="Providers & health"
          description="See which delivery providers are available and whether messaging is healthy."
        />

        <div className="mb-5 flex flex-wrap gap-2">
          <Button disabled={busy} onClick={() => void loadPlugins()}>Refresh providers</Button>
          <Button disabled={busy} variant="outline" onClick={() => void checkHealth()}>Check messaging health</Button>
        </div>

        {health && (
          <Card className="mb-5">
            <CardContent className="flex items-center justify-between gap-4 p-6">
              <div>
                <div className="text-sm text-muted-foreground">Messaging infrastructure</div>
                <div className="mt-1 text-lg font-semibold">{formatStatus(overall) || 'Checked'}</div>
              </div>
              <Badge variant={statusTone(overall)}>{formatStatus(overall) || 'OK'}</Badge>
            </CardContent>
          </Card>
        )}

        <div className="grid gap-3 sm:grid-cols-2">
          {plugins.length === 0 && (
            <p className="text-sm text-muted-foreground col-span-full">No providers loaded yet. Press “Refresh providers”.</p>
          )}
          {plugins.map((p, i) => {
            const name = String(p.name ?? p.Name ?? p.id ?? p.Id ?? `Provider ${i + 1}`)
            const channel = String(p.channel ?? p.Channel ?? p.capability ?? '')
            const status = String(p.status ?? p.Status ?? p.health ?? 'available')
            return (
              <Card key={i}>
                <CardContent className="p-5">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-semibold">{name}</div>
                      {channel && <div className="mt-1 text-xs text-muted-foreground">{channel}</div>}
                    </div>
                    <Badge variant={statusTone(status)}>{formatStatus(status)}</Badge>
                  </div>
                </CardContent>
              </Card>
            )
          })}
        </div>
      </div>
    </div>
  )
}
