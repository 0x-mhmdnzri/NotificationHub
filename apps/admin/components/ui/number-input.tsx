'use client'

import { useEffect, useState } from 'react'
import { cn } from '@/lib/utils'
import { formatFaNumber, parseFaNumber, toFaDigits } from '@/lib/format/number'

type Props = {
  value: number | null | undefined
  onChange: (value: number | null) => void
  className?: string
  placeholder?: string
  disabled?: boolean
  min?: number
  max?: number
  maximumFractionDigits?: number
  id?: string
  name?: string
}

/** Numeric input with Persian digits + thousand separators (٬). */
export function NumberInput({
  value,
  onChange,
  className,
  placeholder,
  disabled,
  min,
  max,
  maximumFractionDigits = 0,
  id,
  name,
}: Props) {
  const [text, setText] = useState(() =>
    value === null || value === undefined ? '' : formatFaNumber(value, { maximumFractionDigits }),
  )

  useEffect(() => {
    if (value === null || value === undefined) {
      setText('')
      return
    }
    const parsed = parseFaNumber(text)
    if (parsed !== value) {
      setText(formatFaNumber(value, { maximumFractionDigits }))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value])

  function handleChange(raw: string) {
    const normalized = raw.replace(/[^\d\u06f0-\u06f9\u0660-\u0669\u066c,.\-]/g, '')
    setText(toFaDigits(normalized).replace(/,/g, '\u066c'))
    const n = parseFaNumber(normalized)
    if (n === null) {
      onChange(null)
      return
    }
    let v = n
    if (min !== undefined && v < min) v = min
    if (max !== undefined && v > max) v = max
    onChange(v)
  }

  function handleBlur() {
    if (value === null || value === undefined) {
      setText('')
      return
    }
    setText(formatFaNumber(value, { maximumFractionDigits }))
  }

  return (
    <input
      id={id}
      name={name}
      inputMode="decimal"
      disabled={disabled}
      placeholder={placeholder ? toFaDigits(placeholder) : undefined}
      value={text}
      onChange={(e) => handleChange(e.target.value)}
      onBlur={handleBlur}
      className={cn(
        'num-input flex h-10 w-full rounded-xl border border-input bg-background px-3 py-2 text-sm ring-offset-background',
        'placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        'disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
    />
  )
}
