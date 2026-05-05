import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { portalApi } from '@/lib/portal'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Badge } from '@/components/ui/Badge'
import { EmptyState } from '@/components/ui/EmptyState'
import { Spinner } from '@/components/ui/Spinner'
import { Bell, ArrowRight } from 'lucide-react'
import { fmtDateTime } from '@/lib/format'

export function PortalNotificationsPage() {
  const q = useQuery({
    queryKey: ['portal-notifications'],
    queryFn: () => portalApi.notifications(50),
  })

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Notifications</h1>
        <p className="text-sm text-slate-500">Recent updates from your firm across all of your cases.</p>
      </div>

      <Card>
        <CardHeader
          title="Recent activity"
          subtitle={q.data ? `${q.data.length} item(s) · ${q.data.filter(i => i.isHighlighted).length} highlighted` : '—'}
        />
        <CardBody>
          {q.isLoading && <div className="py-6 flex justify-center"><Spinner /></div>}
          {!q.isLoading && (q.data ?? []).length === 0 && (
            <EmptyState title="No notifications" description="You're all caught up." icon={<Bell size={20}/>} />
          )}
          <ul className="divide-y divide-slate-100">
            {(q.data ?? []).map(n => (
              <li key={n.id} className="py-3">
                <Link to={`/portal/cases/${n.caseId}`} className="flex items-start justify-between gap-4 group">
                  <div className="flex items-start gap-2 min-w-0">
                    {n.isHighlighted && <span className="size-2 mt-2 shrink-0 rounded-full bg-rose-500" />}
                    <div className="min-w-0">
                      <div className="text-sm font-medium text-slate-800 group-hover:text-emerald-700">
                        {n.summary}
                      </div>
                      <div className="text-xs text-slate-500 mt-0.5 inline-flex items-center gap-1.5">
                        <Badge tone="sky">{n.activityType}</Badge>
                        <span>{n.caseNumber} — {n.caseTitle}</span>
                      </div>
                    </div>
                  </div>
                  <div className="text-xs text-slate-400 shrink-0 inline-flex items-center gap-1">
                    {fmtDateTime(n.occurredAtUtc)} <ArrowRight size={12} className="opacity-0 group-hover:opacity-100" />
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        </CardBody>
      </Card>
    </div>
  )
}
