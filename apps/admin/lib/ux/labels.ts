/** Presentation-layer labels — never leak DTO / enum / route names to operators. */

export const channelLabel: Record<string, string> = {
  push: 'Push',
  sms: 'SMS',
  email: 'Email',
  webhook: 'Webhook',
  inapp: 'In-app',
  chat: 'Chat',
}

export function formatChannel(channel?: string | null) {
  if (!channel) return '—'
  return channelLabel[channel.toLowerCase()] ?? channel
}

export function formatStatus(status?: string | null) {
  if (!status) return 'Unknown'
  const map: Record<string, string> = {
    delivered: 'Delivered',
    pending: 'Pending',
    queued: 'Queued',
    processing: 'Processing',
    failed: 'Failed',
    cancelled: 'Cancelled',
    canceled: 'Cancelled',
    completed: 'Completed',
    running: 'Running',
    scheduled: 'Scheduled',
    draft: 'Draft',
    active: 'Active',
    inactive: 'Inactive',
    success: 'Succeeded',
  }
  return map[status.toLowerCase()] ?? status.replace(/([A-Z])/g, ' $1').replace(/^./, (c) => c.toUpperCase())
}

export type StatusTone = 'success' | 'warning' | 'danger' | 'default' | 'info'

export function statusTone(status?: string | null): StatusTone {
  if (!status) return 'default'
  const s = status.toLowerCase()
  if (['delivered', 'completed', 'active', 'success', 'succeeded', 'granted'].includes(s)) return 'success'
  if (['pending', 'queued', 'processing', 'running', 'scheduled', 'draft'].includes(s)) return 'warning'
  if (['failed', 'cancelled', 'canceled', 'inactive', 'revoked', 'denied'].includes(s)) return 'danger'
  return 'default'
}

export function formatRelative(iso?: string | null) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  const diff = Date.now() - d.getTime()
  const mins = Math.round(diff / 60000)
  if (Math.abs(mins) < 1) return 'Just now'
  if (Math.abs(mins) < 60) return mins > 0 ? `${mins} min ago` : `in ${-mins} min`
  const hrs = Math.round(mins / 60)
  if (Math.abs(hrs) < 24) return hrs > 0 ? `${hrs}h ago` : `in ${-hrs}h`
  return d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

export function formatDateTime(iso?: string | null) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

export function humanTemplateName(key?: string | null, subject?: string | null) {
  if (subject?.trim()) return subject.trim()
  if (!key) return 'Untitled template'
  return key
    .replace(/[._-]+/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
}

export function maskId(id?: string | null, keep = 6) {
  if (!id) return '—'
  if (id.length <= keep + 2) return id
  return `…${id.slice(-keep)}`
}

export function friendlyError(message?: string) {
  if (!message) return 'Something went wrong. Please try again.'
  if (/timeout|network|fetch/i.test(message)) return 'Could not reach the server. Check your connection and try again.'
  if (/unauthorized|401/i.test(message)) return 'Your session expired. Please sign in again.'
  if (/forbidden|403/i.test(message)) return 'You do not have permission to do this.'
  if (/not found|404/i.test(message)) return 'This item was not found or is no longer available.'
  return message
}
