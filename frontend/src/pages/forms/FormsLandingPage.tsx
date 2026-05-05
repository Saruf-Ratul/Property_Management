import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { ltApi } from '@/lib/ltforms'
import { api } from '@/lib/api'
import type { LtCaseSummaryDto, LtFormPhase } from '@/types/ltforms'
import type { ClientDto, PagedResult } from '@/types'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Tabs } from '@/components/ui/Tabs'
import { Badge, stageTone } from '@/components/ui/Badge'
import { FilterBar, FilterSelect } from '@/components/ui/FilterBar'
import { ArrowRight, FileText, CheckCircle2, AlertCircle, Layers } from 'lucide-react'
import { fmtDate, fmtMoney } from '@/lib/format'

const TABS: { id: LtFormPhase | 'All'; label: string }[] = [
  { id: 'All',                label: 'All cases' },
  { id: 'Filing',             label: 'Phase 1 — Filing' },
  { id: 'TrialCertification', label: 'Phase 2 — Trial / Certification' },
  { id: 'Warrant',            label: 'Phase 3 — Warrant' },
]

export function FormsLandingPage() {
  const nav = useNavigate()
  const [tab, setTab] = useState<LtFormPhase | 'All'>('All')
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [clientId, setClientId] = useState('')

  const clientsQ = useQuery({
    queryKey: ['clients-mini'],
    queryFn: async () => (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  const listQ = useQuery({
    queryKey: ['lt-cases', tab, search, clientId, page],
    queryFn: async () => ltApi.list({
      page, pageSize: 25,
      phase: tab === 'All' ? undefined : tab,
      clientId: clientId || undefined,
      search: search || undefined,
    }),
  })

  const cols: Column<LtCaseSummaryDto>[] = [
    {
      key: 'case', header: 'Case', sortKey: 'caseNumber',
      render: r => (
        <div>
          <div className="font-medium text-slate-900">{r.caseNumber}</div>
          <div className="text-xs text-slate-500 truncate max-w-[26rem]">{r.caseTitle}</div>
        </div>
      ),
    },
    { key: 'client', header: 'Client', render: r => r.clientName },
    {
      key: 'phase', header: 'Phase',
      render: r => <Badge tone={r.phase === 'Filing' ? 'sky' : r.phase === 'TrialCertification' ? 'amber' : 'rose'}>{r.phaseName}</Badge>,
    },
    {
      key: 'stage', header: 'Case stage',
      render: r => <Badge tone={stageTone(r.stageCode)}>{r.stageName}</Badge>,
    },
    {
      key: 'review', header: 'Attorney review',
      render: r => r.attorneyReviewed
        ? <Badge tone="green"><CheckCircle2 size={11} className="inline mr-0.5" /> Reviewed</Badge>
        : <Badge tone="amber">Pending</Badge>,
    },
    {
      key: 'forms', header: 'Forms approved', align: 'right',
      render: r => `${r.formsApproved} / ${r.formsTotal}`,
    },
    {
      key: 'gen', header: 'Generated',
      render: r => (
        <div className="text-xs">
          <div>{r.generatedFormCount} form(s) · {r.generatedPacketCount} packet(s)</div>
          {r.latestGeneratedAtUtc && <div className="text-slate-500">latest {fmtDate(r.latestGeneratedAtUtc)}</div>}
        </div>
      ),
    },
    { key: 'due', header: 'Total due', align: 'right', render: r => fmtMoney(r.totalDue) },
  ]

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-xl font-semibold tracking-tight inline-flex items-center gap-2">
            <FileText size={18} className="text-brand-600" /> NJ Landlord-Tenant Forms
          </h1>
          <p className="text-sm text-slate-500">
            Pick an LT case to enter the form wizard. Cases are grouped by procedural phase.
          </p>
        </div>
      </div>

      <Tabs<LtFormPhase | 'All'>
        tabs={TABS.map(t => ({ id: t.id, label: t.label }))}
        active={tab}
        onChange={(t) => { setTab(t); setPage(1) }}
      />

      <FilterBar
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1) }}
        searchPlaceholder="Search by case number or title…"
        hasActiveFilters={!!clientId}
        onClear={() => { setClientId(''); setPage(1) }}
      >
        <FilterSelect
          label="Client" value={clientId}
          options={(clientsQ.data?.items ?? []).map(c => ({ value: c.id, label: c.name }))}
          onChange={(v) => { setClientId(v as string); setPage(1) }}
        />
      </FilterBar>

      <DataTable<LtCaseSummaryDto>
        rows={listQ.data?.items ?? []}
        columns={cols}
        rowKey={r => r.id}
        loading={listQ.isLoading}
        page={page}
        pageSize={listQ.data?.pageSize ?? 25}
        totalCount={listQ.data?.totalCount ?? 0}
        onPageChange={setPage}
        onRowClick={r => nav(`/cases/${r.caseId}/forms`)}
        sort={{ by: 'case', dir: 'desc' }}
        onSortChange={() => {}}
        empty={{
          title: 'No LT cases',
          description: 'Cases that are linked to PMS data automatically receive an LT overlay when created from PMS.',
          icon: <AlertCircle size={20} />,
          action: (
            <Link to="/cases/intake" className="btn-primary">
              <Layers size={14} /> Start a new case from PMS <ArrowRight size={14} />
            </Link>
          ),
        }}
      />
    </div>
  )
}
