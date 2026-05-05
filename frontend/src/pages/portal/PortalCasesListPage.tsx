import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { portalApi } from '@/lib/portal'
import type { CaseListItem, CaseStageCode, CaseStatusCode } from '@/types'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FilterBar, FilterSelect } from '@/components/ui/FilterBar'
import { Tabs } from '@/components/ui/Tabs'
import { Badge, stageTone } from '@/components/ui/Badge'
import { Briefcase } from 'lucide-react'
import { fmtDate, fmtMoney } from '@/lib/format'

type Tab = 'Active' | 'Closed' | 'All'

const TABS: { id: Tab; label: string }[] = [
  { id: 'Active', label: 'Active' },
  { id: 'Closed', label: 'Closed' },
  { id: 'All',    label: 'All' },
]

export function PortalCasesListPage() {
  const nav = useNavigate()
  const [tab, setTab] = useState<Tab>('Active')
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [stage, setStage] = useState<CaseStageCode | ''>('')

  const status: CaseStatusCode | undefined = tab === 'Active' ? 'Open'
    : tab === 'Closed' ? 'Closed' : undefined

  const q = useQuery({
    queryKey: ['portal-cases', tab, search, stage, page],
    queryFn: () => portalApi.listCases({
      page, pageSize: 25,
      search: search || undefined,
      stage: (stage || undefined) as CaseStageCode | undefined,
      status,
    }),
  })

  const cols: Column<CaseListItem>[] = [
    {
      key: 'num', header: 'Case #', sortKey: 'caseNumber',
      render: r => <span className="font-medium text-slate-900">{r.caseNumber}</span>,
    },
    { key: 'title', header: 'Title', render: r => r.title },
    {
      key: 'stage', header: 'Stage',
      render: r => <Badge tone={stageTone(r.stageCode)}>{r.stageName}</Badge>,
    },
    {
      key: 'status', header: 'Status',
      render: r => <Badge tone={r.statusCode === 'Open' ? 'green' : 'gray'}>{r.statusName}</Badge>,
    },
    {
      key: 'attorney', header: 'Attorney',
      render: r => r.assignedAttorney ?? <span className="text-slate-400">—</span>,
    },
    {
      key: 'amount', header: 'Amount', align: 'right',
      render: r => fmtMoney(r.amountInControversy),
    },
    {
      key: 'court', header: 'Court date',
      render: r => r.courtDateUtc ? fmtDate(r.courtDateUtc) : <span className="text-slate-400">—</span>,
    },
    { key: 'created', header: 'Filed/Created', render: r => fmtDate(r.filedOnUtc ?? r.createdAtUtc) },
  ]

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">My Cases</h1>
        <p className="text-sm text-slate-500">Cases your firm is handling on behalf of your company.</p>
      </div>

      <Tabs<Tab>
        tabs={TABS.map(t => ({ id: t.id, label: t.label }))}
        active={tab}
        onChange={(t) => { setTab(t); setPage(1) }}
      />

      <FilterBar
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1) }}
        searchPlaceholder="Search by case number or title…"
        hasActiveFilters={!!stage}
        onClear={() => { setStage(''); setPage(1) }}
      >
        <FilterSelect
          label="Stage" value={stage}
          options={[
            'Intake','Draft','FormReview','ReadyToFile','Filed',
            'CourtDateScheduled','Judgment','Settlement','Dismissed',
            'WarrantRequested','Closed',
          ].map(s => ({ value: s, label: s.replace(/([A-Z])/g, ' $1').trim() }))}
          onChange={(v) => { setStage(v as CaseStageCode | ''); setPage(1) }}
        />
      </FilterBar>

      <DataTable<CaseListItem>
        rows={q.data?.items ?? []}
        columns={cols}
        rowKey={r => r.id}
        loading={q.isLoading}
        page={page}
        pageSize={q.data?.pageSize ?? 25}
        totalCount={q.data?.totalCount ?? 0}
        onPageChange={setPage}
        onRowClick={r => nav(`/portal/cases/${r.id}`)}
        sort={{ by: 'created', dir: 'desc' }}
        onSortChange={() => {}}
        empty={{
          title: 'No cases yet',
          description: 'When your firm files cases, they will appear here.',
          icon: <Briefcase size={20} />,
        }}
      />
    </div>
  )
}
