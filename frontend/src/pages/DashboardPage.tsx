import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { DashboardStats } from '@/types'
import { Stat } from '@/components/ui/Stat'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Briefcase, AlertTriangle, CheckCircle2, DollarSign, Users } from 'lucide-react'
import { fmtMoney, fmtDateTime } from '@/lib/format'
import { Badge, stageTone } from '@/components/ui/Badge'

export function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: async () => (await api.get<DashboardStats>('/dashboard')).data,
  })

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Dashboard</h1>
        <p className="text-sm text-slate-500">Operational view across firm cases and property data.</p>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-5 gap-4">
        <Stat label="Total Cases" value={isLoading ? '…' : data?.totalCases ?? 0} icon={<Briefcase size={18} />} tone="brand" />
        <Stat label="Active" value={isLoading ? '…' : data?.activeCases ?? 0} icon={<Briefcase size={18} />} tone="green" />
        <Stat label="Closed" value={isLoading ? '…' : data?.closedCases ?? 0} icon={<CheckCircle2 size={18} />} tone="gray" />
        <Stat label="Delinquent Tenants" value={isLoading ? '…' : data?.delinquentTenants ?? 0} icon={<Users size={18} />} tone="amber" />
        <Stat label="Outstanding Balance" value={isLoading ? '…' : fmtMoney(data?.totalOutstandingBalance ?? 0)} icon={<DollarSign size={18} />} tone="rose" />
      </div>

      <div className="grid lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader title="Cases by stage" />
          <CardBody>
            {!data?.casesByStage?.length && <p className="text-sm text-slate-500">No cases yet.</p>}
            <div className="space-y-2">
              {data?.casesByStage?.map(s => (
                <div key={s.code} className="flex items-center justify-between text-sm">
                  <Badge tone={stageTone(s.code)}>{s.name}</Badge>
                  <span className="font-medium text-slate-800">{s.count}</span>
                </div>
              ))}
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardHeader title="PMS sync status" />
          <CardBody>
            {!data?.pmsSyncStatus?.length && <p className="text-sm text-slate-500">No integrations configured.</p>}
            <ul className="space-y-3">
              {data?.pmsSyncStatus?.map(s => (
                <li key={s.integrationId} className="text-sm">
                  <div className="font-medium text-slate-800">{s.displayName}</div>
                  <div className="text-xs text-slate-500">{s.clientName}</div>
                  <div className="text-xs text-slate-500 mt-0.5 flex items-center gap-1.5">
                    {s.lastSyncStatus === 'Succeeded'
                      ? <CheckCircle2 size={12} className="text-emerald-600" />
                      : s.lastSyncStatus === 'Failed'
                        ? <AlertTriangle size={12} className="text-rose-600" />
                        : null}
                    Last sync: {fmtDateTime(s.lastSyncAtUtc)}
                  </div>
                </li>
              ))}
            </ul>
          </CardBody>
        </Card>
      </div>

      <Card>
        <CardHeader title="Recent activity" />
        <CardBody>
          {!data?.recentActivity?.length && <p className="text-sm text-slate-500">Nothing yet.</p>}
          <ul className="divide-y divide-slate-100">
            {data?.recentActivity?.map((a, i) => (
              <li key={i} className="py-2 text-sm flex items-start justify-between">
                <div>
                  <div className="text-slate-800">{a.summary}</div>
                  {a.caseNumber && <div className="text-xs text-slate-500">Case {a.caseNumber}</div>}
                </div>
                <div className="text-xs text-slate-400 whitespace-nowrap">{fmtDateTime(a.occurredAtUtc)}</div>
              </li>
            ))}
          </ul>
        </CardBody>
      </Card>
    </div>
  )
}
