import { LogOut, ChevronDown, Menu } from 'lucide-react'
import { useState, useRef, useEffect } from 'react'
import { useAuth } from '@/lib/auth'
import { useNavigate } from 'react-router-dom'

interface TopbarProps {
  /** Toggle the mobile sidebar drawer (only used on screens < lg). */
  onMenuClick?: () => void
}

export function Topbar({ onMenuClick }: TopbarProps) {
  const { user, logout } = useAuth()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const nav = useNavigate()

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
    <header className="bg-white ring-1 ring-slate-200 px-4 sm:px-6 lg:px-8">
      <div className="h-14 flex items-center justify-between gap-3">
        <div className="flex items-center gap-2 min-w-0">
          {/* Hamburger — only visible on screens < lg */}
          {onMenuClick && (
            <button
              type="button"
              aria-label="Open menu"
              onClick={onMenuClick}
              className="lg:hidden rounded-lg p-2 text-slate-600 hover:bg-slate-100"
            >
              <Menu size={18} />
            </button>
          )}
          <div className="text-sm text-slate-500 truncate">
            {user ? <>Welcome, <span className="font-medium text-slate-800">{user.firstName}</span></> : null}
          </div>
        </div>
        <div className="relative" ref={ref}>
          <button
            className="flex items-center gap-2 rounded-lg p-1.5 hover:bg-slate-100"
            onClick={() => setOpen(o => !o)}
          >
            <div className="size-8 rounded-full bg-brand-600 text-white text-xs font-semibold flex items-center justify-center">
              {initials}
            </div>
            <div className="hidden sm:block text-left">
              <div className="text-sm font-medium text-slate-800">{user?.fullName}</div>
              <div className="text-[11px] text-slate-500">{user?.roles.join(', ')}</div>
            </div>
            <ChevronDown size={14} className="text-slate-500" />
          </button>
          {open && (
            <div className="absolute right-0 mt-2 w-56 card overflow-hidden">
              <div className="p-3 text-xs text-slate-500 border-b border-slate-100">
                <div className="font-medium text-slate-700">{user?.email}</div>
                <div className="mt-0.5">Roles: {user?.roles.join(', ')}</div>
              </div>
              <button
                className="w-full text-left px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 flex items-center gap-2"
                onClick={() => { logout(); nav('/login') }}
              >
                <LogOut size={14} /> Sign out
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
