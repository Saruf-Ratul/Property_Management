import { ReactNode } from 'react'
import { Search, FilterX } from 'lucide-react'
import clsx from 'clsx'

interface Props {
  search: string
  onSearchChange: (v: string) => void
  searchPlaceholder?: string
  onClear?: () => void
  hasActiveFilters?: boolean
  children?: ReactNode
  /** Right-side actions like 'Add new' buttons. */
  actions?: ReactNode
  className?: string
}

export function FilterBar({
  search, onSearchChange, searchPlaceholder = 'Search…',
  onClear, hasActiveFilters, children, actions, className,
}: Props) {
  return (
    <div className={clsx('card p-3 flex flex-col gap-3 lg:flex-row lg:items-center', className)}>
      <div className="relative flex-1 min-w-[200px]">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
        <input
          className="input pl-9"
          placeholder={searchPlaceholder}
          value={search}
          onChange={e => onSearchChange(e.target.value)}
        />
      </div>
      {children && (
        <div className="flex flex-wrap items-center gap-2">
          {children}
        </div>
      )}
      <div className="flex items-center gap-2 ml-auto">
        {hasActiveFilters && onClear && (
          <button type="button" className="btn-ghost text-xs" onClick={onClear}>
            <FilterX size={12} /> Clear
          </button>
        )}
        {actions}
      </div>
    </div>
  )
}

interface FilterSelectProps<T extends string | number> {
  label: string
  value: T | ''
  options: { value: T; label: string }[]
  onChange: (v: T | '') => void
  width?: string
}

export function FilterSelect<T extends string | number>({
  label, value, options, onChange, width = 'w-44',
}: FilterSelectProps<T>) {
  return (
    <div className={clsx('flex items-center gap-1.5', width)}>
      <select
        className="input text-xs py-1.5"
        value={value as any}
        onChange={e => onChange(e.target.value as T | '')}
      >
        <option value="">{label}: All</option>
        {options.map(o => (
          <option key={String(o.value)} value={o.value as any}>
            {label}: {o.label}
          </option>
        ))}
      </select>
    </div>
  )
}

export function FilterToggle({
  label, checked, onChange,
}: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="text-xs text-slate-700 inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg ring-1 ring-slate-200 bg-white cursor-pointer hover:bg-slate-50">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      {label}
    </label>
  )
}

export function FilterNumber({
  label, value, onChange, width = 'w-32', step = 100,
}: { label: string; value: number | ''; onChange: (v: number | '') => void; width?: string; step?: number }) {
  return (
    <div className={clsx('flex items-center gap-1.5', width)}>
      <input
        className="input text-xs py-1.5"
        type="number"
        step={step}
        placeholder={label}
        value={value === '' ? '' : value}
        onChange={e => {
          const n = e.target.value === '' ? '' : Number(e.target.value)
          onChange(Number.isNaN(n) ? '' : (n as number | ''))
        }}
      />
    </div>
  )
}
