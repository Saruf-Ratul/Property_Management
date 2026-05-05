import clsx from 'clsx'
import { ReactNode } from 'react'

interface Column<T> {
  key: string
  header: ReactNode
  className?: string
  render: (row: T) => ReactNode
}

interface Props<T> {
  rows: T[]
  columns: Column<T>[]
  rowKey: (row: T) => string
  onRowClick?: (row: T) => void
  empty?: ReactNode
  loading?: boolean
}

export function Table<T>({ rows, columns, rowKey, onRowClick, empty, loading }: Props<T>) {
  return (
    <div className="overflow-hidden rounded-xl ring-1 ring-slate-200 bg-white">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50">
            <tr>
              {columns.map(c => (
                <th key={c.key} className={clsx('px-4 py-2.5 text-left font-medium text-slate-500 text-xs uppercase tracking-wide', c.className)}>
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {loading && (
              <tr><td colSpan={columns.length} className="px-4 py-8 text-center text-slate-500">Loading…</td></tr>
            )}
            {!loading && rows.length === 0 && (
              <tr><td colSpan={columns.length} className="px-4 py-10 text-center text-slate-500">{empty || 'No records found'}</td></tr>
            )}
            {!loading && rows.map(r => (
              <tr key={rowKey(r)} onClick={onRowClick ? () => onRowClick(r) : undefined}
                  className={clsx('hover:bg-slate-50/80', onRowClick && 'cursor-pointer')}>
                {columns.map(c => (
                  <td key={c.key} className={clsx('px-4 py-3 text-slate-700', c.className)}>{c.render(r)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
