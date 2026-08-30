'use client'

import { useState } from 'react'
import { PageHeader } from '@/components/page-header'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { ToastHost } from '@/components/toast-host'
import { resourcesApi } from '@/lib/api/resources'
import { useTenant } from '@/providers/tenant-provider'
import { formatChannel, friendlyError } from '@/lib/ux/labels'

function maskToken(t?: string) {
  if (!t) return '—'
  if (t.length <= 8) return '••••'
  return `${t.slice(0, 4)}…${t.slice(-4)}`
}

export default function DevicesPage() {
  const { tenantId } = useTenant()
  const [userId, setUserId] = useState('')
  const [platform, setPlatform] = useState('ios')
  const [token, setToken] = useState('')
  const [locale, setLocale] = useState('fa-IR')
  const [lookup, setLookup] = useState('')
  const [devices, setDevices] = useState<Array<{ platform?: string; token?: string; locale?: string }>>([])
  const [revokeToken, setRevokeToken] = useState<string | null>(null)
  const [toast, setToast] = useState<{ tone: 'success' | 'error'; title: string; description?: string } | null>(null)
  const [busy, setBusy] = useState(false)

  async function register() {
    setBusy(true)
    try {
      await resourcesApi.devices.register({ userId, platform, token, locale, tenantId })
      setToast({ tone: 'success', title: 'Device registered' })
      setToken('')
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not register device', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function list() {
    setBusy(true)
    try {
      const res = (await resourcesApi.devices.list(lookup, tenantId)) as Array<Record<string, unknown>>
      setDevices(
        (Array.isArray(res) ? res : []).map((d) => ({
          platform: String(d.platform ?? d.Platform ?? ''),
          token: String(d.token ?? d.Token ?? ''),
          locale: String(d.locale ?? d.Locale ?? ''),
        })),
      )
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not load devices', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function revoke() {
    if (!revokeToken || !lookup) return
    setBusy(true)
    try {
      await resourcesApi.devices.unregister({ userId: lookup, token: revokeToken, tenantId })
      setRevokeToken(null)
      setToast({ tone: 'success', title: 'Device removed' })
      await list()
    } catch (e) {
      setToast({ tone: 'error', title: 'Could not remove device', description: friendlyError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid-bg min-h-full p-5 md:p-8">
      <ToastHost toast={toast} onClose={() => setToast(null)} />
      <div className="mx-auto max-w-[1100px]">
        <PageHeader
          eyebrow="Audience"
          title="Devices"
          description="Manage phones and browsers that can receive push notifications."
        />
        <div className="grid gap-5 lg:grid-cols-2">
          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">Register a device</h2>
              <Field label="User">
                <Input value={userId} onChange={(e) => setUserId(e.target.value)} placeholder="User ID" />
              </Field>
              <Field label="Platform">
                <Select value={platform} onChange={(e) => setPlatform(e.target.value)}>
                  <option value="ios">iPhone / iPad</option>
                  <option value="android">Android</option>
                  <option value="web">Web browser</option>
                </Select>
              </Field>
              <Field label="Device token" hint="Sensitive — stored securely on the server">
                <Input value={token} onChange={(e) => setToken(e.target.value)} type="password" autoComplete="off" />
              </Field>
              <Field label="Language">
                <Input value={locale} onChange={(e) => setLocale(e.target.value)} />
              </Field>
              <Button disabled={busy || !userId || !token} onClick={() => void register()}>
                Register device
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-4 p-6">
              <h2 className="font-semibold">Find devices for a user</h2>
              <Field label="User">
                <Input value={lookup} onChange={(e) => setLookup(e.target.value)} placeholder="User ID" />
              </Field>
              <Button disabled={busy || !lookup} variant="outline" onClick={() => void list()}>
                Show devices
              </Button>
              <div className="space-y-2">
                {devices.length === 0 && (
                  <p className="text-sm text-muted-foreground">No devices loaded yet.</p>
                )}
                {devices.map((d, i) => (
                  <div key={i} className="flex items-center justify-between rounded-xl border p-3 text-sm">
                    <div>
                      <div className="font-medium">{d.platform === 'ios' ? 'iOS' : d.platform === 'android' ? 'Android' : d.platform || 'Device'}</div>
                      <div className="text-xs text-muted-foreground">{maskToken(d.token)} · {d.locale || '—'}</div>
                    </div>
                    <Button size="sm" variant="ghost" className="text-destructive" onClick={() => setRevokeToken(d.token || null)}>
                      Remove
                    </Button>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      <ConfirmDialog
        open={!!revokeToken}
        onOpenChange={(v) => !v && setRevokeToken(null)}
        title="Remove this device?"
        confirmLabel="Yes, remove it"
        destructive
        busy={busy}
        onConfirm={revoke}
        description="This device will no longer receive push notifications until it is registered again."
      />
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
