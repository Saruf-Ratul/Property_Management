import clsx from 'clsx'
import { ReactNode } from 'react'

export function Stat({ label, value, icon, tone = 'brand' }: {
  label: string
  value: ReactNode
  icon?: ReactNode
  tone?: 'brand' | 'green' | 'amber' | 'rose' | 'gray'
}) {
  const tones = {
    brand: 'bg-brand-50 text-brand-700',
    green: 'bg-emerald-50 text-emerald-700',
    amber: 'bg-amber-50 text-amber-700',
    rose: 'bg-rose-50 text-rose-700',
    gray: 'bg-slate-100 text-slate-600',
  } as const
  return (
    <div className="card p-5 flex items-start justify-between gap-4">
      <div>
        <div className="text-xs text-slate-500 font-medium uppercase tracking-wide">{label}</div>
        <div className="mt-2 text-2xl font-semibold text-slate-900">{value}</div>
      </div>
      {icon && <div className={clsx('rounded-lg p-2.5', tones[tone])}>{icon}</div>}
    </div>
  )
}
