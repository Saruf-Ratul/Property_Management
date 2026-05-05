import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { useMemo, useState } from 'react'
import { api } from '@/lib/api'
import { casesApi } from '@/lib/cases'
import type {
  AssigneeDto, CaseListItem, CaseListTab, CaseStageCode, CaseStageDto,
  CaseStatusCode, ClientDto, PagedResult,
} from '@/types'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FilterBar, FilterSelect } from '@/components/ui/FilterBar'
import { Tabs } from '@/components/ui/Tabs'
import { Badge, stageTone } from '@/components/ui/Badge'
import { Plus, Briefcase, ArrowRight, Sparkles } from 'lucide-react'
import { fmtDate, fmtMoney } from '@/lib/format'
import { useAuth } from '@/lib/auth'

const TABS: { id: CaseListTab; label: string }[] = [
  { id: 'Active', label: 'Active' },
  { id: 'Filed', label: 'Filed' },
  { id: 'Closed', label: 'Closed' },
  { id: 'All', label: 'All' },
]

interface Filters {
  search: string
  clientId: string
  stage: CaseStageCode | ''
  status: CaseStatusCode | ''
  attorneyId: string
  createdFrom: string
  createdTo: string
}

export function CasesListPage() {
  const nav = useNavigate()
  const { isFirmStaff } = useAuth()

  const [tab, setTab] = useState<CaseListTab>('Active')
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<Filters>({
    search: '', clientId: '', stage: '', status: '',
    attorneyId: '', createdFrom: '', createdTo: '',
  })

  const clientsQ = useQuery({
    queryKey: ['clients-mini'],
    queryFn: async () => (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })
  const stagesQ = useQuery({ queryKey: ['case-stages'], queryFn: casesApi.stages })
  const assigneesQ = useQuery({ queryKey: ['case-assignees'], queryFn: casesApi.assignees, enabled: isFirmStaff })

  const lawyers = (assigneesQ.data ?? []).filter((a: AssigneeDto) => a.role === 'Lawyer' || a.role === 'FirmAdmin')

  const listQ = useQuery({
    queryKey: ['cases', tab, filters, page],
    queryFn: async () => casesApi.list({
      tab,
      page,
      pageSize: 25,
      search: filters.search || undefined,
      clientId: filters.clientId || undefined,
      stage: (filters.stage || undefined) as CaseStageCode | undefined,
      status: (filters.status || undefined) as CaseStatusCode | undefined,
      assignedAttorneyId: filters.attorneyId || undefined,
      createdFrom: filters.createdFrom || undefined,
      createdTo: filters.createdTo || undefined,
    }),
  })

  const hasActiveFilters = useMemo(
    () => !!(filters.clientId || filters.stage || filters.status || filters.attorneyId
             || filters.createdFrom || filters.createdTo),
    [filters],
  )

  const cols: Column<CaseListItem>[] = [
    {
      key: 'num', header: 'Case #', sortKey: 'caseNumber',
      render: r => <span className="font-medium text-slate-900">{r.caseNumber}</span>,
    },
    { key: 'title', header: 'Title', sortKey: 'title', render: r => r.title },
    { key: 'client', header: 'Client', sortKey: 'clientName', render: r => r.clientName },
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
      key: 'amount', header: 'Amount',
      sortKey: 'amountInControversy', align: 'right',
      render: r => fmtMoney(r.amountInControversy),
    },
    { key: 'created', header: 'Created', sortKey: 'createdAtUtc', render: r => fmtDate(r.createdAtUtc) },
  ]

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Cases</h1>
          <p className="text-sm text-slate-500">Landlord-tenant matters across all clients.</p>
        </div>
        {isFirmStaff && (
          <div className="flex gap-2">
            <Link to="/cases/new" className="btn-secondary">
              <Plus size={14} /> New case
            </Link>
            <Link to="/cases/intake" className="btn-primary">
              <Sparkles size={14} /> Start case from PMS
            </Link>
          </div>
        )}
      </div>

      <Tabs<CaseListTab>
        tabs={TABS.map(t => ({ id: t.id, label: t.label }))}
        active={tab}
        onChange={(t) => { setTab(t); setPage(1) }}
      />

      <FilterBar
        search={filters.search}
        onSearchChange={v => { setFilters(f => ({ ...f, search: v })); setPage(1) }}
        searchPlaceholder="Search by case number, title, or docket…"
        hasActiveFilters={hasActiveFilters}
        onClear={() => {
          setFilters(f => ({ search: f.search, clientId: '', stage: '', status: '', attorneyId: '', createdFrom: '', createdTo: '' }))
          setPage(1)
        }}
      >
        <FilterSelect
          label="Client" value={filters.clientId}
          options={(clientsQ.data?.items ?? []).map(c => ({ value: c.id, label: c.name }))}
          onChange={v => { setFilters(f => ({ ...f, clientId: v as string })); setPage(1) }}
        />
        <FilterSelect
          label="Stage" value={filters.stage}
          options={(stagesQ.data ?? []).map((s: CaseStageDto) => ({ value: s.code, label: s.name }))}
          onChange={v => { setFilters(f => ({ ...f, stage: v as CaseStageCode | '' })); setPage(1) }}
        />
        <FilterSelect
          label="Status" value={filters.status}
          options={[
            { value: 'Open', label: 'Open' },
            { value: 'OnHold', label: 'On Hold' },
            { value: 'Closed', label: 'Closed' },
            { value: 'Cancelled', label: 'Cancelled' },
          ]}
          onChange={v => { setFilters(f => ({ ...f, status: v as CaseStatusCode | '' })); setPage(1) }}
        />
        {isFirmStaff && (
          <FilterSelect
            label="Attorney" value={filters.attorneyId}
            options={lawyers.map(l => ({ value: l.id, label: l.fullName }))}
            onChange={v => { setFilters(f => ({ ...f, attorneyId: v as string })); setPage(1) }}
          />
        )}
        <div className="flex items-center gap-1.5">
          <input className="input text-xs py-1.5 w-36" type="date"
            value={filters.createdFrom}
            onChange={e => { setFilters(f => ({ ...f, createdFrom: e.target.value })); setPage(1) }} />
          <span className="text-xs text-slate-400">to</span>
          <input className="input text-xs py-1.5 w-36" type="date"
            value={filters.createdTo}
            onChange={e => { setFilters(f => ({ ...f, createdTo: e.target.value })); setPage(1) }} />
        </div>
      </FilterBar>

      <DataTable<CaseListItem>
        rows={listQ.data?.items ?? []}
        columns={cols}
        rowKey={r => r.id}
        loading={listQ.isLoading}
        page={page}
        pageSize={listQ.data?.pageSize ?? 25}
        totalCount={listQ.data?.totalCount ?? 0}
        onPageChange={setPage}
        onRowClick={r => nav(`/cases/${r.id}`)}
        sort={{ by: 'created', dir: 'desc' }}
        onSortChange={() => {}}
        empty={{
          title: 'No cases',
          description: 'Try a different filter set, or start a new case.',
          icon: <Briefcase size={20} />,
          action: isFirmStaff ? (
            <Link to="/cases/intake" className="btn-primary">
              <Sparkles size={14} /> Start case from PMS <ArrowRight size={14} />
            </Link>
          ) : undefined,
        }}
      />
    </div>
  )
}
