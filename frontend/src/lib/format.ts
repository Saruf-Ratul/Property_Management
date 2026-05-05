import { format, parseISO } from 'date-fns'

export function fmtDate(d?: string | null, pattern = 'MMM d, yyyy') {
  if (!d) return '—'
  try { return format(parseISO(d), pattern) } catch { return d }
}

export function fmtDateTime(d?: string | null) {
  return fmtDate(d, 'MMM d, yyyy h:mm a')
}

export function fmtMoney(n: number | null | undefined) {
  if (n === null || n === undefined) return '—'
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
}

export function fmtNumber(n: number | null | undefined) {
  if (n === null || n === undefined) return '—'
  return new Intl.NumberFormat('en-US').format(n)
}
