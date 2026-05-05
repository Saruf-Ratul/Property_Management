import { ReactNode, useMemo } from 'react'
import clsx from 'clsx'
import { ChevronLeft, ChevronRight, ChevronUp, ChevronDown, ChevronsUpDown } from 'lucide-react'
import { SkeletonRow } from './Skeleton'
import { EmptyState } from './EmptyState'

export interface Column<T> {
  key: string
  header: ReactNode
  render: (row: T) => ReactNode
  className?: string
  /** Optional sort accessor (client-side sort fallback when no server sort is wired). */
  sortKey?: keyof T | ((row: T) => string | number | Date | null | undefined)
  align?: 'left' | 'right' | 'center'
}

export interface SortState {
  by: string
  dir: 'asc' | 'desc'
}

interface DataTableProps<T> {
  rows: T[]
  columns: Column<T>[]
  rowKey: (row: T) => string

  loading?: boolean
  loadingRows?: number

  /** Pagination metadata. Hidden if undefined. */
  page?: number
  pageSize?: number
  totalCount?: number
  onPageChange?: (page: number) => void

  /** Sorting state (controlled). When set, headers become sortable buttons. */
  sort?: SortState
  onSortChange?: (s: SortState | undefined) => void

  /** Multi-select. Provide setter to enable. */
  selectedIds?: string[]
  onSelectedChange?: (ids: string[]) => void

  onRowClick?: (row: T) => void

  empty?: { title?: string; description?: ReactNode; icon?: ReactNode; action?: ReactNode }
}

export function DataTable<T>({
  rows, columns, rowKey,
  loading, loadingRows = 6,
  page, pageSize, totalCount, onPageChange,
  sort, onSortChange,
  selectedIds, onSelectedChange,
  onRowClick,
  empty,
}: DataTableProps<T>) {
  const showCheckbox = !!onSelectedChange
  const totalPages = (totalCount && pageSize) ? Math.max(1, Math.ceil(totalCount / pageSize)) : 1
  const showPager = !!onPageChange && (totalCount ?? 0) > (pageSize ?? 0)

  // Client-side sort fallback when sortKey is provided but no controlled state.
  const sortedRows = useMemo(() => {
    if (!sort) return rows
    const col = columns.find(c => c.key === sort.by)
    if (!col?.sortKey) return rows
    const accessor = (r: T) => {
      if (typeof col.sortKey === 'function') return col.sortKey(r)
      const v = r[col.sortKey as keyof T] as unknown
      return v as string | number | Date | null | undefined
    }
    return [...rows].sort((a, b) => {
      const av = accessor(a)
      const bv = accessor(b)
      if (av == null && bv == null) return 0
      if (av == null) return -1
      if (bv == null) return 1
      const cmp = av < bv ? -1 : av > bv ? 1 : 0
      return sort.dir === 'asc' ? cmp : -cmp
    })
  }, [rows, columns, sort])

  const allSelected = showCheckbox && rows.length > 0 && rows.every(r => selectedIds!.includes(rowKey(r)))
  const someSelected = showCheckbox && !allSelected && rows.some(r => selectedIds!.includes(rowKey(r)))

  function toggleAll() {
    if (!onSelectedChange) return
    const ids = rows.map(rowKey)
    onSelectedChange(allSelected ? selectedIds!.filter(id => !ids.includes(id)) : Array.from(new Set([...selectedIds!, ...ids])))
  }
  function toggleRow(id: string) {
    if (!onSelectedChange) return
    onSelectedChange(selectedIds!.includes(id) ? selectedIds!.filter(x => x !== id) : [...selectedIds!, id])
  }

  function clickHeader(col: Column<T>) {
    if (!onSortChange || !col.sortKey) return
    if (sort?.by === col.key) {
      onSortChange(sort.dir === 'asc' ? { by: col.key, dir: 'desc' } : undefined)
    } else {
      onSortChange({ by: col.key, dir: 'asc' })
    }
  }

  return (
    <div className="rounded-xl ring-1 ring-slate-200 bg-white overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50">
            <tr>
              {showCheckbox && (
                <th className="px-3 py-2 w-10">
                  <input
                    type="checkbox"
                    checked={allSelected}
                    ref={el => { if (el) el.indeterminate = someSelected }}
                    onChange={toggleAll}
                  />
                </th>
              )}
              {columns.map(c => {
                const sortable = !!onSortChange && !!c.sortKey
                const active = sort?.by === c.key
                return (
                  <th
                    key={c.key}
                    className={clsx(
                      'px-4 py-2.5 font-medium text-slate-500 text-xs uppercase tracking-wide select-none',
                      c.align === 'right' ? 'text-right' : c.align === 'center' ? 'text-center' : 'text-left',
                      c.className,
                      sortable && 'cursor-pointer hover:text-slate-700',
                    )}
                    onClick={() => clickHeader(c)}
                  >
                    <span className="inline-flex items-center gap-1">
                      {c.header}
                      {sortable && (
                        active
                          ? sort!.dir === 'asc'
                            ? <ChevronUp size={12} />
                            : <ChevronDown size={12} />
                          : <ChevronsUpDown size={12} className="opacity-50" />
                      )}
                    </span>
                  </th>
                )
              })}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {loading
              ? Array.from({ length: loadingRows }).map((_, i) => (
                  <SkeletonRow key={i} cols={columns.length + (showCheckbox ? 1 : 0)} />
                ))
              : sortedRows.length === 0
                ? (
                  <tr>
                    <td colSpan={columns.length + (showCheckbox ? 1 : 0)} className="p-0">
                      <EmptyState
                        title={empty?.title ?? 'No records'}
                        description={empty?.description}
                        icon={empty?.icon}
                        action={empty?.action}
                      />
                    </td>
                  </tr>
                )
                : sortedRows.map(r => {
                    const id = rowKey(r)
                    const selected = !!selectedIds?.includes(id)
                    return (
                      <tr
                        key={id}
                        className={clsx(
                          'hover:bg-slate-50/80',
                          onRowClick && 'cursor-pointer',
                          selected && 'bg-brand-50/40',
                        )}
                        onClick={onRowClick ? () => onRowClick(r) : undefined}
                      >
                        {showCheckbox && (
                          <td className="px-3 py-3" onClick={e => e.stopPropagation()}>
                            <input type="checkbox" checked={selected} onChange={() => toggleRow(id)} />
                          </td>
                        )}
                        {columns.map(c => (
                          <td key={c.key}
                            className={clsx(
                              'px-4 py-3 text-slate-700',
                              c.align === 'right' ? 'text-right' : c.align === 'center' ? 'text-center' : '',
                              c.className,
                            )}
                          >
                            {c.render(r)}
                          </td>
                        ))}
                      </tr>
                    )
                  })}
          </tbody>
        </table>
      </div>

      {showPager && (
        <div className="flex items-center justify-between border-t border-slate-100 px-4 py-2.5 text-xs text-slate-500">
          <div>
            <span className="font-medium text-slate-700">
              {(page! - 1) * pageSize! + 1}–{Math.min(page! * pageSize!, totalCount!)}
            </span>{' '}
            of <span className="font-medium text-slate-700">{totalCount}</span>
          </div>
          <div className="flex items-center gap-1">
            <button
              type="button"
              className="btn-ghost text-xs disabled:opacity-30"
              disabled={page! <= 1}
              onClick={() => onPageChange!(page! - 1)}
            >
              <ChevronLeft size={12} /> Prev
            </button>
            <span className="px-2">Page {page} of {totalPages}</span>
            <button
              type="button"
              className="btn-ghost text-xs disabled:opacity-30"
              disabled={page! >= totalPages}
              onClick={() => onPageChange!(page! + 1)}
            >
              Next <ChevronRight size={12} />
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
