/** Presentation-layer labels. Never leak DTO/enum/route names to operators. */

export const channelLabel: Record<string, string> = {
  push: 'Push',
  sms: 'SMS',
  email: 'Email',
  webhook: 'Webhook',
  chat: 'Chat',
  inapp: 'In-app',
}

export function formatChannel(value?: string | null) {
  if (!value) return '—'
  return channelLabel[value.toLowerCase()] ?? value
}

export function formatStatus(value?: string | null) {
  if (!value) return 'Unknown'
  const map: Record<string, string> = {
    delivered: 'Delivered',
    pending: 'Pending',
    queued: 'Queued',
    processing: 'Sending',
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
  return map[value.toLowerCase()] ?? value.replace(/[_-]/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

export function statusTone(value?: string | null): 'success' | 'warning' | 'danger' | 'default' | 'outline' {
  const v = (value ?? '').toLowerCase()
  if (['delivered', 'completed', 'active', 'success', 'succeeded'].includes(v)) return 'success'
  if (['pending', 'queued', 'processing', 'running', 'scheduled', 'draft'].includes(v)) return 'warning'
  if (['failed', 'cancelled', 'canceled', 'inactive', 'error'].includes(v)) return 'danger'
  return 'default'
}

export function formatDateTime(value?: string | Date | null) {
  if (!value) return '—'
  const d = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(d.getTime())) return '—'
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(d)
}

export function formatRelative(value?: string | Date | null) {
  if (!value) return '—'
  const d = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(d.getTime())) return '—'
  const diff = Date.now() - d.getTime()
  const sec = Math.round(diff / 1000)
  if (sec < 60) return 'Just now'
  const min = Math.round(sec / 60)
  if (min < 60) return `${min}m ago`
  const hr = Math.round(min / 60)
  if (hr < 24) return `${hr}h ago`
  const day = Math.round(hr / 24)
  if (day < 7) return `${day}d ago`
  return formatDateTime(d)
}

export function templateTitle(t: { key?: string; subject?: string | null; body?: string | null }) {
  return t.subject?.trim() || t.body?.slice(0, 48) || humanizeKey(t.key) || 'Untitled template'
}

export function humanizeKey(key?: string | null) {
  if (!key) return '—'
  return key
    .replace(/[_.-]+/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
}

export function friendlyError(error: unknown): string {
  if (!error) return 'Something went wrong. Please try again.'
  if (typeof error === 'string') return error
  const msg = (error as { message?: string }).message ?? ''
  if (/network|fetch|reach/i.test(msg)) return 'Could not reach the server. Check your connection and try again.'
  if (/401|unauthor/i.test(msg)) return 'Your session expired. Please sign in again.'
  if (/403|forbidden/i.test(msg)) return 'You do not have permission to do this.'
  if (/404|not found/i.test(msg)) return 'That item could not be found. It may have been removed.'
  if (/valid/i.test(msg)) return msg
  if (msg.length > 0 && msg.length < 160 && !/exception|stack|http/i.test(msg)) return msg
  return 'The operation could not be completed. Please try again or contact support.'
}

export const priorityLabel: Record<string, string> = {
  low: 'Low',
  normal: 'Normal',
  high: 'High',
  critical: 'Urgent',
}
