import { NavLink, Outlet, Link } from 'react-router-dom'
import clsx from 'clsx'
import {
  LayoutDashboard, Briefcase, Bell, LogOut, Building2, ChevronDown,
} from 'lucide-react'
import { useState, useRef, useEffect } from 'react'
import { useAuth } from '@/lib/auth'
import { useQuery } from '@tanstack/react-query'
import { portalApi } from '@/lib/portal'
import { fmtDateTime } from '@/lib/format'

export function ClientPortalLayout() {
  const { user } = useAuth()
  const dashboardQ = useQuery({
    queryKey: ['portal-dashboard-header'],
    queryFn: portalApi.dashboard,
    enabled: !!user,
    staleTime: 60_000,
  })
  const clientName = dashboardQ.data?.clientName

  return (
    <div className="h-full flex flex-col bg-slate-50">
      {/* Header bar */}
      <header className="bg-white border-b border-slate-200">
        <div className="max-w-[1280px] mx-auto px-4 sm:px-6 lg:px-8 h-14 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="rounded-lg bg-emerald-600 text-white p-2"><Building2 size={16} /></div>
            <div className="leading-tight">
              <div className="text-sm font-semibold">Client Portal</div>
              <div className="text-[11px] text-slate-500">{clientName ?? 'Property Management'}</div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <NotificationsButton />
            <UserMenu />
          </div>
        </div>
        <PortalNav />
      </header>

      {/* Body */}
      <main className="flex-1 overflow-y-auto">
        <div className="max-w-[1280px] mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <Outlet />
        </div>
      </main>

      <footer className="border-t border-slate-200 bg-white">
        <div className="max-w-[1280px] mx-auto px-4 sm:px-6 lg:px-8 py-3 text-[11px] text-slate-400 flex justify-between">
          <span>© Property Management Platform — Client Portal</span>
          <span>v0.1.0</span>
        </div>
      </footer>
    </div>
  )
}

// ─── Top navigation (Dashboard / Cases / Notifications) ────────────────────
function PortalNav() {
  const items = [
    { to: '/portal',               label: 'Dashboard',     icon: LayoutDashboard, end: true },
    { to: '/portal/cases',         label: 'My Cases',      icon: Briefcase },
    { to: '/portal/notifications', label: 'Notifications', icon: Bell },
  ]
  return (
    <nav className="border-t border-slate-100">
      <div className="max-w-[1280px] mx-auto px-4 sm:px-6 lg:px-8 flex gap-1">
        {items.map(i => (
          <NavLink key={i.to} to={i.to} end={i.end}
            className={({ isActive }) => clsx(
              'inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium border-b-2 transition',
              isActive ? 'border-emerald-600 text-emerald-700'
                       : 'border-transparent text-slate-600 hover:text-slate-900 hover:border-slate-300',
            )}
          >
            <i.icon size={14} /> {i.label}
          </NavLink>
        ))}
      </div>
    </nav>
  )
}

// ─── Notifications popover ─────────────────────────────────────────────────
function NotificationsButton() {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const q = useQuery({
    queryKey: ['portal-notifications-popover'],
    queryFn: () => portalApi.notifications(15),
    refetchOnWindowFocus: true,
    staleTime: 30_000,
  })
  const items = q.data ?? []
  const unread = items.filter(i => i.isHighlighted).length

  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  return (
    <div className="relative" ref={ref}>
      <button
        className="relative rounded-lg p-2 text-slate-600 hover:bg-slate-100"
        onClick={() => setOpen(o => !o)}
        aria-label="Notifications"
      >
        <Bell size={16} />
        {unread > 0 && (
          <span className="absolute top-1 right-1 size-2 rounded-full bg-rose-500" />
        )}
      </button>
      {open && (
        <div className="absolute right-0 top-full mt-2 w-80 card overflow-hidden z-50">
          <div className="px-3 py-2 border-b border-slate-100 flex items-center justify-between">
            <div className="text-sm font-semibold">Notifications</div>
            <Link to="/portal/notifications" className="text-xs text-emerald-700 hover:underline" onClick={() => setOpen(false)}>
              View all
            </Link>
          </div>
          <ul className="max-h-80 overflow-y-auto divide-y divide-slate-100">
            {items.length === 0 && (
              <li className="px-3 py-6 text-center text-xs text-slate-500">No notifications</li>
            )}
            {items.slice(0, 8).map(n => (
              <li key={n.id} className="px-3 py-2 text-sm">
                <Link to={`/portal/cases/${n.caseId}`}
                  className="block hover:bg-slate-50 -mx-3 px-3 py-1 rounded"
                  onClick={() => setOpen(false)}>
                  <div className="flex items-center gap-1.5">
                    {n.isHighlighted && <span className="size-1.5 rounded-full bg-rose-500" />}
                    <span className="text-slate-800 line-clamp-2">{n.summary}</span>
                  </div>
                  <div className="text-[11px] text-slate-500 mt-0.5">
                    {n.caseNumber} · {fmtDateTime(n.occurredAtUtc)}
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

// ─── User menu ──────────────────────────────────────────────────────────────
function UserMenu() {
  const { user, logout } = useAuth()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  const initials = user
    ? `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase()
    : '?'

  return (
    <div className="relative" ref={ref}>
      <button className="flex items-center gap-2 rounded-lg p-1.5 hover:bg-slate-100"
              onClick={() => setOpen(o => !o)}>
        <div className="size-8 rounded-full bg-emerald-600 text-white text-xs font-semibold flex items-center justify-center">
          {initials}
        </div>
        <div className="hidden sm:block text-left">
          <div className="text-sm font-medium text-slate-800">{user?.fullName}</div>
          <div className="text-[11px] text-slate-500">{user?.roles.join(', ')}</div>
        </div>
        <ChevronDown size={14} className="text-slate-500" />
      </button>
      {open && (
        <div className="absolute right-0 top-full mt-2 w-56 card overflow-hidden">
          <div className="p-3 text-xs text-slate-500 border-b border-slate-100">
            <div className="font-medium text-slate-700">{user?.email}</div>
            <div className="mt-0.5">Roles: {user?.roles.join(', ')}</div>
          </div>
          <button className="w-full text-left px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 flex items-center gap-2"
                  onClick={() => { logout(); window.location.assign('/login') }}>
            <LogOut size={14} /> Sign out
          </button>
        </div>
      )}
    </div>
  )
}
