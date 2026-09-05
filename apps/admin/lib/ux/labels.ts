/** Presentation-layer labels. Never leak DTO/enum/route names to operators. */

export const channelLabel: Record<string, string> = {
  push: 'پوش',
  sms: 'پیامک',
  email: 'ایمیل',
  webhook: 'وب‌هوک',
  chat: 'چت',
  inapp: 'درون‌برنامه‌ای',
}

export function formatChannel(value?: string | null) {
  if (!value) return '—'
  return channelLabel[value.toLowerCase()] ?? value
}

export function formatStatus(value?: string | null) {
  if (!value) return 'نامشخص'
  const map: Record<string, string> = {
    delivered: 'تحویل‌شده',
    pending: 'در انتظار',
    queued: 'در صف',
    processing: 'در حال ارسال',
    failed: 'ناموفق',
    cancelled: 'لغو شده',
    canceled: 'لغو شده',
    completed: 'تکمیل‌شده',
    running: 'در حال اجرا',
    scheduled: 'زمان‌بندی‌شده',
    draft: 'پیش‌نویس',
    active: 'فعال',
    inactive: 'غیرفعال',
    success: 'موفق',
  }
  return map[value.toLowerCase()] ?? value.replace(/[_-]/g, ' ')
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
  return new Intl.DateTimeFormat('fa-IR', {
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
  if (sec < 60) return 'همین الان'
  const min = Math.round(sec / 60)
  if (min < 60) return `${min} دقیقه پیش`
  const hr = Math.round(min / 60)
  if (hr < 24) return `${hr} ساعت پیش`
  const day = Math.round(hr / 24)
  if (day < 7) return `${day} روز پیش`
  return formatDateTime(d)
}

export function templateTitle(t: { key?: string; subject?: string | null; body?: string | null }) {
  return t.subject?.trim() || t.body?.slice(0, 48) || humanizeKey(t.key) || 'قالب بدون عنوان'
}

export function humanizeKey(key?: string | null) {
  if (!key) return ''
  return key
    .replace(/[_.-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
}

export function friendlyError(err: unknown): string {
  if (!err) return 'خطای ناشناخته'
  if (typeof err === 'string') return err
  if (err instanceof Error) return err.message
  try {
    return JSON.stringify(err)
  } catch {
    return String(err)
  }
}

export const priorityLabel: Record<string, string> = {
  low: 'کم',
  normal: 'عادی',
  high: 'بالا',
  critical: 'فوری',
}
