import { ReactNode, useEffect } from 'react'
import { X } from 'lucide-react'
import clsx from 'clsx'

interface DrawerProps {
  open: boolean
  onClose: () => void
  title: ReactNode
  subtitle?: ReactNode
  children: ReactNode
  width?: 'sm' | 'md' | 'lg' | 'xl'
  footer?: ReactNode
}

export function Drawer({ open, onClose, title, subtitle, children, width = 'lg', footer }: DrawerProps) {
  useEffect(() => {
    if (!open) return
    const onEsc = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onEsc)
    return () => window.removeEventListener('keydown', onEsc)
  }, [open, onClose])

  const widthClass = { sm: 'max-w-md', md: 'max-w-xl', lg: 'max-w-3xl', xl: 'max-w-5xl' }[width]

  return (
    <div className={clsx('fixed inset-0 z-50 transition', open ? 'visible' : 'invisible pointer-events-none')}>
      <div
        className={clsx('absolute inset-0 bg-slate-900/30 transition-opacity', open ? 'opacity-100' : 'opacity-0')}
        onClick={onClose}
      />
      <aside
        className={clsx(
          'absolute right-0 top-0 h-full w-full bg-white shadow-2xl flex flex-col transition-transform duration-200',
          widthClass,
          open ? 'translate-x-0' : 'translate-x-full',
        )}
      >
        <header className="px-6 py-4 border-b border-slate-200 flex items-start justify-between gap-4">
          <div>
            <h2 className="text-base font-semibold text-slate-900">{title}</h2>
            {subtitle && <div className="text-xs text-slate-500 mt-0.5">{subtitle}</div>}
          </div>
          <button onClick={onClose} className="text-slate-500 hover:text-slate-900" aria-label="Close">
            <X size={18} />
          </button>
        </header>
        <div className="flex-1 overflow-y-auto p-6">{children}</div>
        {footer && <footer className="px-6 py-4 border-t border-slate-200 bg-slate-50">{footer}</footer>}
      </aside>
    </div>
  )
}
