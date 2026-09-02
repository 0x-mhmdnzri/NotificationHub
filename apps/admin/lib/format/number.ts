const FA = '۰۱۲۳۴۵۶۷۸۹'
const EN = '0123456789'

/** Convert Western digits → Persian digits */
export function toFaDigits(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return ''
  return String(value).replace(/[0-9]/g, (d) => FA[Number(d)]!)
}

/** Convert Persian/Arabic digits → Western */
export function toEnDigits(value: string): string {
  return value
    .replace(/[۰-۹]/g, (d) => EN[FA.indexOf(d)]!)
    .replace(/[٠-٩]/g, (d) => EN[d.charCodeAt(0) - '٠'.charCodeAt(0)]!)
}

/**
 * Format number with fa-IR grouping and Persian digits.
 * Example: 1234567 → ۱٬۲۳۴٬۵۶۷
 */
export function formatFaNumber(
  value: number | string | null | undefined,
  options?: Intl.NumberFormatOptions,
): string {
  if (value === null || value === undefined || value === '') return ''
  const n = typeof value === 'number' ? value : Number(toEnDigits(String(value)).replace(/,/g, ''))
  if (!Number.isFinite(n)) return toFaDigits(String(value))
  return new Intl.NumberFormat('fa-IR', options).format(n)
}

/** Parse user input (Persian digits, separators) → number | null */
export function parseFaNumber(raw: string): number | null {
  const cleaned = toEnDigits(raw)
    .replace(/[٬,\s]/g, '')
    .replace(/[^\d.-]/g, '')
  if (!cleaned || cleaned === '-' || cleaned === '.') return null
  const n = Number(cleaned)
  return Number.isFinite(n) ? n : null
}
