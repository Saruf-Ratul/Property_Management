import { Inbox } from 'lucide-react'
import type { ReactNode } from 'react'
import clsx from 'clsx'

interface EmptyStateProps {
  title?: string
  description?: ReactNode
  icon?: ReactNode
  action?: ReactNode
  className?: string
}

export function EmptyState({ title = 'No data', description, icon, action, className }: EmptyStateProps) {
  return (
    <div className={clsx('flex flex-col items-center justify-center text-center py-10 px-6', className)}>
      <div className="rounded-full bg-slate-100 p-3 text-slate-400 mb-3">
        {icon ?? <Inbox size={20} />}
      </div>
      <h4 className="text-sm font-semibold text-slate-800">{title}</h4>
      {description && <p className="text-xs text-slate-500 mt-1 max-w-sm">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}
