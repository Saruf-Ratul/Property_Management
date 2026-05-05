import { ReactNode } from 'react'
import clsx from 'clsx'

interface Tab<T extends string> {
  id: T
  label: ReactNode
  badge?: ReactNode
}

interface Props<T extends string> {
  tabs: Tab<T>[]
  active: T
  onChange: (id: T) => void
  className?: string
}

export function Tabs<T extends string>({ tabs, active, onChange, className }: Props<T>) {
  return (
    <div className={clsx('border-b border-slate-200', className)}>
      <nav className="-mb-px flex gap-4 overflow-x-auto" role="tablist">
        {tabs.map(t => (
          <button
            key={t.id}
            role="tab"
            type="button"
            aria-selected={active === t.id}
            onClick={() => onChange(t.id)}
            className={clsx(
              'whitespace-nowrap border-b-2 py-2.5 px-0.5 text-sm font-medium inline-flex items-center gap-2 transition',
              active === t.id
                ? 'border-brand-600 text-brand-700'
                : 'border-transparent text-slate-500 hover:text-slate-800 hover:border-slate-300',
            )}
          >
            {t.label}
            {t.badge}
          </button>
        ))}
      </nav>
    </div>
  )
}
