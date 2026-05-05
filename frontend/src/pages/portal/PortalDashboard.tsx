import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { portalApi } from '@/lib/portal'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Stat } from '@/components/ui/Stat'
import { Spinner } from '@/components/ui/Spinner'
import { EmptyState } from '@/components/ui/EmptyState'
import { Badge } from '@/components/ui/Badge'
import {
  Briefcase, CheckCircle2, FileText, AlertCircle, Calendar, ArrowRight,
  Bell, DollarSign, Clock,
} from 'lucide-react'
import { fmtDate, fmtDateTime, fmtMoney, fmtNumber } from '@/lib/format'
import { useAuth } from '@/lib/auth'

export function PortalDashboard() {
  const { user } = useAuth()
  const q = useQuery({
    queryKey: ['portal-dashboard'],
    queryFn: portalApi.dashboard,
  })

  if (q.isLoading) return <div className="py-16 flex justify-center"><Spinner /></div>
  if (q.isError || !q.data) return <EmptyState title="Failed to load dashboard" icon={<AlertCircle size={20} />} />

  const d = q.data
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Welcome back, {user?.firstName}</h1>
        <p className="text-sm text-slate-500">Here's what's happening across {d.clientName}'s active cases.</p>
      </div>

      {/* KPI grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <Stat label="Total cases"        value={fmtNumber(d.totalCases)}        icon={<Briefcase size={18}/>}    tone="brand" />
        <Stat label="Active"             value={fmtNumber(d.activeCases)}       icon={<Briefcase size={18}/>}    tone="green" />
        <Stat label="Closed"             value={fmtNumber(d.closedCases)}       icon={<CheckCircle2 size={18}/>} tone="gray" />
        <Stat label="Amount in dispute"  value={fmtMoney(d.totalAmountInControversy)} icon={<DollarSign size={18}/>} tone="amber" />
      </div>

      {/* Phase breakdown */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <PhaseStat label="In filing"          count={d.casesInFiling}          tone="sky" />
        <PhaseStat label="In trial / judgment" count={d.casesInTrialOrJudgment} tone="amber" />
        <PhaseStat label="Awaiting warrant"   count={d.casesAwaitingWarrant}   tone="rose" />
      </div>

      <div className="grid lg:grid-cols-3 gap-5">
        {/* Upcoming court dates */}
        <Card className="lg:col-span-2">
          <CardHeader
            title="Upcoming court dates"
            subtitle={d.nextCourtDateUtc ? `Next on ${fmtDate(d.nextCourtDateUtc)}` : 'No upcoming dates'}
          />
          <CardBody>
            {d.upcomingCourtDates.length === 0 ? (
              <EmptyState
                title="No scheduled court dates"
                description="You'll see upcoming dates here as soon as the firm schedules them."
                icon={<Calendar size={20} />}
              />
            ) : (
              <ul className="divide-y divide-slate-100">
                {d.upcomingCourtDates.map(u => (
                  <li key={u.caseId} className="py-2.5">
                    <Link to={`/portal/cases/${u.caseId}`} className="flex items-center justify-between gap-3 group">
                      <div className="min-w-0">
                        <div className="font-medium text-slate-800 truncate group-hover:text-emerald-700">{u.caseTitle}</div>
                        <div className="text-xs text-slate-500">
                          {u.caseNumber}{u.courtVenue ? ` · ${u.courtVenue}` : ''}{u.courtDocketNumber ? ` · Docket ${u.courtDocketNumber}` : ''}
                        </div>
                      </div>
                      <div className="text-right shrink-0">
                        <div className="text-sm font-semibold">{fmtDate(u.courtDateUtc)}</div>
                        <div className="text-xs text-slate-500">{fmtDateTime(u.courtDateUtc).split(', ')[1]}</div>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardBody>
        </Card>

        {/* Notifications */}
        <Card>
          <CardHeader
            title="Recent notifications"
            subtitle={d.unreadNotificationCount > 0 ? `${d.unreadNotificationCount} highlighted` : 'All caught up'}
            action={<Link to="/portal/notifications" className="text-xs text-emerald-700 hover:underline">View all</Link>}
          />
          <CardBody>
            {d.recentActivity.length === 0 ? (
              <EmptyState title="No activity yet" icon={<Bell size={20} />} />
            ) : (
              <ul className="space-y-3">
                {d.recentActivity.slice(0, 8).map(n => (
                  <li key={n.id} className="text-sm">
                    <Link to={`/portal/cases/${n.caseId}`} className="block hover:bg-slate-50 rounded -mx-1 px-1 py-1">
                      <div className="flex items-start gap-1.5">
                        {n.isHighlighted && <span className="size-1.5 mt-2 shrink-0 rounded-full bg-rose-500" />}
                        <div className="min-w-0">
                          <div className="text-slate-800">{n.summary}</div>
                          <div className="text-[11px] text-slate-500">
                            {n.caseNumber} · {fmtDateTime(n.occurredAtUtc)}
                          </div>
                        </div>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardBody>
        </Card>
      </div>

      {/* Quick links */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <QuickLink to="/portal/cases" title="My cases" subtitle="View status, timeline, documents" icon={<Briefcase size={18}/>} />
        <QuickLink to="/portal/notifications" title="Notifications" subtitle="Recent updates from your firm" icon={<Bell size={18}/>} />
        <QuickLink to="/portal/cases" title="Documents" subtitle={`${d.documentsAvailableCount} document(s) available`} icon={<FileText size={18}/>} />
      </div>
    </div>
  )
}

function PhaseStat({ label, count, tone }: { label: string; count: number; tone: 'sky' | 'amber' | 'rose' }) {
  const bg = { sky: 'bg-sky-50 text-sky-700', amber: 'bg-amber-50 text-amber-700', rose: 'bg-rose-50 text-rose-700' }[tone]
  return (
    <div className="card p-5 flex items-center gap-3">
      <div className={`rounded-lg p-2.5 ${bg}`}><Clock size={18} /></div>
      <div>
        <div className="text-xs text-slate-500 uppercase tracking-wide">{label}</div>
        <div className="text-2xl font-semibold">{count}</div>
      </div>
    </div>
  )
}

function QuickLink({ to, title, subtitle, icon }: { to: string; title: string; subtitle: string; icon: React.ReactNode }) {
  return (
    <Link to={to} className="card p-5 flex items-center justify-between hover:ring-emerald-200 transition group">
      <div className="flex items-center gap-3">
        <div className="rounded-lg bg-emerald-50 text-emerald-700 p-2.5">{icon}</div>
        <div>
          <div className="font-medium text-slate-800 group-hover:text-emerald-700">{title}</div>
          <div className="text-xs text-slate-500">{subtitle}</div>
        </div>
      </div>
      <ArrowRight size={14} className="text-slate-400 group-hover:text-emerald-600" />
    </Link>
  )
}
