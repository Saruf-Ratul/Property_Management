import { NavLink } from 'react-router-dom'
import {
  LayoutDashboard, Briefcase, Building2, Users, FileText,
  Database, ShieldCheck, FolderKanban, Scale, Receipt, X,
} from 'lucide-react'
import clsx from 'clsx'
import { useAuth } from '@/lib/auth'
import type { Role } from '@/types'

interface NavItem {
  to: string
  label: string
  icon: typeof LayoutDashboard
  roles?: Role[]
}

const NAV: NavItem[] = [
  { to: '/',                 label: 'Dashboard',       icon: LayoutDashboard },
  { to: '/cases',            label: 'Cases',           icon: Briefcase },
  { to: '/properties',       label: 'Properties',      icon: Building2,  roles: ['FirmAdmin', 'Lawyer', 'Paralegal', 'ClientAdmin'] },
  { to: '/tenants',          label: 'Tenants',         icon: Users,      roles: ['FirmAdmin', 'Lawyer', 'Paralegal', 'ClientAdmin'] },
  { to: '/delinquent',       label: 'Delinquent',      icon: Receipt,    roles: ['FirmAdmin', 'Lawyer', 'Paralegal', 'ClientAdmin'] },
  { to: '/clients',          label: 'Clients',         icon: FolderKanban, roles: ['FirmAdmin', 'Lawyer', 'Paralegal'] },
  { to: '/pms-integrations', label: 'PMS Integrations',icon: Database,   roles: ['FirmAdmin', 'Lawyer', 'Paralegal'] },
  { to: '/forms',            label: 'NJ LT Forms',     icon: FileText,   roles: ['FirmAdmin', 'Lawyer', 'Paralegal'] },
  { to: '/audit',            label: 'Audit Logs',      icon: ShieldCheck,roles: ['FirmAdmin', 'Auditor'] },
]

interface SidebarProps {
  /** Mobile drawer open state (hamburger toggle in Topbar). Desktop ignores this. */
  mobileOpen?: boolean
  onMobileClose?: () => void
}

export function Sidebar({ mobileOpen = false, onMobileClose }: SidebarProps) {
  const { user, hasAnyRole } = useAuth()
  if (!user) return null

  const visible = NAV.filter(n => !n.roles || hasAnyRole(n.roles))

  const inner = (
    <>
      <div className="flex items-center gap-2 px-5 py-5 border-b border-slate-100">
        <div className="rounded-lg bg-brand-600 text-white p-2"><Scale size={18} /></div>
        <div className="flex-1">
          <div className="text-sm font-semibold leading-tight">Property Mgmt</div>
          <div className="text-[11px] text-slate-500 leading-tight">Case Management</div>
        </div>
        <button
          type="button"
          aria-label="Close menu"
          onClick={onMobileClose}
          className="lg:hidden text-slate-500 hover:text-slate-900 p-1 rounded hover:bg-slate-100"
        >
          <X size={18} />
        </button>
      </div>
      <nav className="flex-1 p-3 space-y-0.5 overflow-y-auto">
        {visible.map(n => (
          <NavLink
            key={n.to}
            to={n.to}
            end={n.to === '/'}
            onClick={() => onMobileClose?.()}
            className={({ isActive }) => clsx(
              'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition',
              isActive ? 'bg-brand-50 text-brand-700' : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900',
            )}
          >
            <n.icon size={16} />
            <span>{n.label}</span>
          </NavLink>
        ))}
      </nav>
      <div className="border-t border-slate-100 p-3 text-[11px] text-slate-400">
        v0.1.0 — Phase 0 MVP
      </div>
    </>
  )

  return (
    <>
      {/* ── Desktop fixed left rail (>= lg) ──────────────────────────────── */}
      <aside className="hidden lg:flex w-64 flex-col bg-white ring-1 ring-slate-200 shrink-0">
        {inner}
      </aside>

      {/* ── Mobile slide-in drawer (< lg) ────────────────────────────────── */}
      <div
        className={clsx(
          'fixed inset-0 z-40 lg:hidden transition',
          mobileOpen ? 'visible' : 'invisible pointer-events-none',
        )}
        role="dialog"
        aria-modal="true"
      >
        <div
          className={clsx(
            'absolute inset-0 bg-slate-900/40 transition-opacity',
            mobileOpen ? 'opacity-100' : 'opacity-0',
          )}
          onClick={onMobileClose}
        />
        <aside
          className={clsx(
            'absolute left-0 top-0 h-full w-72 max-w-[85vw] bg-white shadow-2xl flex flex-col transition-transform duration-200',
            mobileOpen ? 'translate-x-0' : '-translate-x-full',
          )}
        >
          {inner}
        </aside>
      </div>
    </>
  )
}
