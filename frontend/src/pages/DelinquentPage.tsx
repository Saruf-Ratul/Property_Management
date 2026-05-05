import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, unwrapError } from '@/lib/api'
import type {
  CaseDetail, ClientDto, DelinquencyStatsDto, DelinquentTenantDto, PagedResult,
} from '@/types'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FilterBar, FilterNumber, FilterSelect } from '@/components/ui/FilterBar'
import { Stat } from '@/components/ui/Stat'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Badge } from '@/components/ui/Badge'
import { Spinner } from '@/components/ui/Spinner'
import { EmptyState } from '@/components/ui/EmptyState'
import { Modal } from '@/components/ui/Modal'
import {
  AlertTriangle, ArrowRight, Briefcase, CalendarClock, DollarSign,
  TrendingUp, Users, Layers, CheckCircle2,
} from 'lucide-react'
import { fmtDate, fmtMoney, fmtNumber } from '@/lib/format'
import { useAuth } from '@/lib/auth'
import toast from 'react-hot-toast'

export function DelinquentPage() {
  const [clientId, setClientId] = useState<string>('')
  const [minBalance, setMinBalance] = useState<number | ''>(1)
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string[]>([])
  const [bulkOpen, setBulkOpen] = useState(false)

  const { isFirmStaff } = useAuth()
  const nav = useNavigate()
  const qc = useQueryClient()

  const clientsQ = useQuery({
    queryKey: ['clients-mini'],
    queryFn: async () => (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  const statsQ = useQuery({
    queryKey: ['delinquency-stats', clientId],
    queryFn: async () => (await api.get<DelinquencyStatsDto>('/tenants/delinquency-stats', {
      params: { clientId: clientId || undefined },
    })).data,
  })

  const listQ = useQuery({
    queryKey: ['delinquent-list', clientId, minBalance, page],
    queryFn: async () => (await api.get<PagedResult<DelinquentTenantDto>>('/tenants/delinquent', {
      params: {
        clientId: clientId || undefined,
        minBalance: minBalance === '' ? 1 : minBalance,
        page,
        pageSize: 25,
      },
    })).data,
  })

  const stats = statsQ.data
  const rows = listQ.data?.items ?? []
  const selectedRows = useMemo(() => rows.filter(r => selected.includes(r.leaseId)), [rows, selected])

  const startCase = useMutation({
    mutationFn: async (row: DelinquentTenantDto) => {
      const r = await api.post<CaseDetail>('/cases', {
        title: `${row.tenantName} — ${row.propertyName} #${row.unitNumber} — Non-payment`,
        caseType: 'LandlordTenantEviction',
        clientId: row.clientId,
        pmsLeaseId: row.leaseId,
        amountInControversy: row.currentBalance,
        description: `Outstanding balance: ${fmtMoney(row.currentBalance)} · Days delinquent: ${row.daysDelinquent}.`,
      })
      try { await api.post(`/cases/${r.data.id}/snapshot`) } catch { /* non-fatal */ }
      return r.data
    },
    onSuccess: (c) => {
      toast.success(`Case ${c.caseNumber} created`)
      qc.invalidateQueries({ queryKey: ['cases'] })
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const bulkCreate = useMutation({
    mutationFn: async () => {
      const results: { ok: number; fail: number; cases: string[] } = { ok: 0, fail: 0, cases: [] }
      for (const row of selectedRows) {
        try {
          const r = await api.post<CaseDetail>('/cases', {
            title: `${row.tenantName} — ${row.propertyName} #${row.unitNumber} — Non-payment`,
            caseType: 'LandlordTenantEviction',
            clientId: row.clientId,
            pmsLeaseId: row.leaseId,
            amountInControversy: row.currentBalance,
            description: `Outstanding balance: ${fmtMoney(row.currentBalance)} · Days delinquent: ${row.daysDelinquent}.`,
          })
          try { await api.post(`/cases/${r.data.id}/snapshot`) } catch { /* non-fatal */ }
          results.ok++
          results.cases.push(r.data.caseNumber)
        } catch {
          results.fail++
        }
      }
      return results
    },
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['cases'] })
      qc.invalidateQueries({ queryKey: ['delinquent-list'] })
      qc.invalidateQueries({ queryKey: ['delinquency-stats'] })
      toast.success(`Created ${r.ok} case(s)${r.fail ? ` (${r.fail} failed)` : ''}`)
      setSelected([])
      setBulkOpen(false)
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const cols: Column<DelinquentTenantDto>[] = [
    {
      key: 'tenant', header: 'Tenant', sortKey: 'tenantName',
      render: r => <span className="font-medium text-slate-900">{r.tenantName}</span>,
    },
    {
      key: 'unit', header: 'Property / Unit',
      render: r => (
        <Link to={`/properties/${r.propertyId}`} className="text-slate-700 hover:text-brand-700"
          onClick={e => e.stopPropagation()}>
          {r.propertyName} · #{r.unitNumber}
        </Link>),
    },
    { key: 'client', header: 'Client', render: r => r.clientName },
    { key: 'rent', header: 'Rent', align: 'right', sortKey: 'monthlyRent', render: r => fmtMoney(r.monthlyRent) },
    {
      key: 'bal', header: 'Balance', align: 'right', sortKey: 'currentBalance',
      render: r => <span className="text-rose-700 font-medium">{fmtMoney(r.currentBalance)}</span>,
    },
    {
      key: 'days', header: 'Days late', align: 'right', sortKey: 'daysDelinquent',
      render: r => (
        <Badge tone={r.daysDelinquent > 60 ? 'rose' : r.daysDelinquent > 30 ? 'amber' : 'gray'}>
          {r.daysDelinquent}d
        </Badge>),
    },
    {
      key: 'act', header: '', align: 'right',
      render: r => isFirmStaff && (
        <button
          className="btn-secondary text-xs"
          onClick={(e) => { e.stopPropagation(); startCase.mutate(r) }}
          disabled={startCase.isPending}
        >
          File case <ArrowRight size={12} />
        </button>
      ),
    },
  ]

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Delinquency Dashboard</h1>
        <p className="text-sm text-slate-500">Tenants with outstanding balances on active leases. Bulk-review candidates for filing.</p>
      </div>

      {/* KPI cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <Stat
          label="Delinquent Tenants"
          value={statsQ.isLoading ? '…' : fmtNumber(stats?.totalDelinquentTenants ?? 0)}
          icon={<Users size={18} />} tone="rose"
        />
        <Stat
          label="Outstanding Balance"
          value={statsQ.isLoading ? '…' : fmtMoney(stats?.totalOutstandingBalance ?? 0)}
          icon={<DollarSign size={18} />} tone="amber"
        />
        <Stat
          label="Average Balance"
          value={statsQ.isLoading ? '…' : fmtMoney(stats?.averageBalance ?? 0)}
          icon={<TrendingUp size={18} />} tone="brand"
        />
        <Stat
          label="Oldest Unpaid"
          value={statsQ.isLoading ? '…' : `${stats?.oldestUnpaidDays ?? 0}d`}
          icon={<CalendarClock size={18} />} tone="gray"
        />
      </div>

      {/* Top properties + Oldest unpaid */}
      <div className="grid lg:grid-cols-2 gap-5">
        <Card>
          <CardHeader title="Top properties by outstanding balance"
            subtitle="Aggregate balance across delinquent leases" />
          <CardBody>
            {statsQ.isLoading
              ? <div className="py-6 flex justify-center"><Spinner /></div>
              : (stats?.topPropertiesByBalance.length ?? 0) === 0
                ? <EmptyState title="No delinquent properties" icon={<CheckCircle2 size={20} />}
                    description="Nothing to file. Property managers will be relieved." />
                : (
                  <ul className="divide-y divide-slate-100">
                    {stats!.topPropertiesByBalance.map(p => (
                      <li key={p.propertyId} className="py-2.5 flex items-center justify-between gap-3">
                        <div className="min-w-0">
                          <Link to={`/properties/${p.propertyId}`} className="text-sm font-medium text-slate-800 hover:text-brand-700 truncate block">
                            {p.propertyName}
                          </Link>
                          <div className="text-xs text-slate-500">
                            {p.clientName} · {p.delinquentTenantCount} delinquent · biggest {fmtMoney(p.largestSingleBalance)}
                          </div>
                        </div>
                        <div className="text-right shrink-0">
                          <div className="text-sm font-semibold text-rose-700">{fmtMoney(p.outstandingBalance)}</div>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
          </CardBody>
        </Card>

        <Card>
          <CardHeader title="Largest unpaid balances"
            subtitle="Top tenants ordered by outstanding amount" />
          <CardBody>
            {statsQ.isLoading
              ? <div className="py-6 flex justify-center"><Spinner /></div>
              : (stats?.oldestUnpaidTenants.length ?? 0) === 0
                ? <EmptyState title="No outstanding tenants" icon={<CheckCircle2 size={20} />} />
                : (
                  <ul className="divide-y divide-slate-100">
                    {stats!.oldestUnpaidTenants.map(t => (
                      <li key={t.leaseId} className="py-2.5 flex items-center justify-between gap-3">
                        <div className="min-w-0">
                          <div className="text-sm font-medium text-slate-800 truncate">{t.tenantName}</div>
                          <div className="text-xs text-slate-500 truncate">
                            {t.propertyName} · #{t.unitNumber} · {t.clientName}
                          </div>
                        </div>
                        <div className="text-right shrink-0 flex items-center gap-3">
                          <div>
                            <div className="text-sm font-semibold text-rose-700">{fmtMoney(t.currentBalance)}</div>
                            <div className="text-[11px] text-slate-500">{t.daysDelinquent}d late</div>
                          </div>
                          {isFirmStaff && (
                            <button
                              className="btn-ghost text-xs"
                              onClick={() => startCase.mutate(t)}
                              disabled={startCase.isPending}
                              title="Create LT case"
                            >
                              <Briefcase size={12} />
                            </button>
                          )}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
          </CardBody>
        </Card>
      </div>

      {/* Filters + bulk review */}
      <FilterBar
        search=""
        onSearchChange={() => {}}
        searchPlaceholder=""
        hasActiveFilters={!!clientId || (typeof minBalance === 'number' && minBalance > 1)}
        onClear={() => { setClientId(''); setMinBalance(1); setPage(1) }}
        actions={
          isFirmStaff ? (
            <button
              className="btn-primary"
              disabled={selected.length === 0}
              onClick={() => setBulkOpen(true)}
            >
              <Layers size={14} /> Bulk review ({selected.length})
            </button>
          ) : null
        }
      >
        <FilterSelect
          label="Client"
          value={clientId}
          options={(clientsQ.data?.items ?? []).map(c => ({ value: c.id, label: c.name }))}
          onChange={v => { setClientId(v as string); setPage(1) }}
        />
        <FilterNumber
          label="Min balance"
          value={minBalance}
          onChange={v => { setMinBalance(v); setPage(1) }}
        />
      </FilterBar>

      <DataTable<DelinquentTenantDto>
        rows={rows}
        columns={cols}
        rowKey={r => r.leaseId}
        loading={listQ.isLoading}
        page={page}
        pageSize={listQ.data?.pageSize ?? 25}
        totalCount={listQ.data?.totalCount ?? 0}
        onPageChange={setPage}
        sort={{ by: 'bal', dir: 'desc' }}
        onSortChange={() => {}}
        selectedIds={isFirmStaff ? selected : undefined}
        onSelectedChange={isFirmStaff ? setSelected : undefined}
        empty={{
          title: 'No delinquent tenants',
          description: 'Either no balances meet the filter, or PMS has not been synced yet.',
          icon: <AlertTriangle size={20} />,
        }}
      />

      {/* Bulk-review confirmation modal */}
      <Modal open={bulkOpen} onClose={() => setBulkOpen(false)} title="Bulk review & file LT cases" size="lg">
        <div className="space-y-4">
          <div className="text-sm text-slate-700">
            About to create <strong>{selectedRows.length}</strong> landlord-tenant cases. Each will be
            linked to its PMS lease and a snapshot will be taken automatically.
          </div>
          <div className="card p-3 max-h-72 overflow-y-auto">
            <ul className="divide-y divide-slate-100 text-sm">
              {selectedRows.map(r => (
                <li key={r.leaseId} className="py-2 flex items-center justify-between gap-3">
                  <div className="min-w-0 truncate">
                    <span className="font-medium">{r.tenantName}</span>
                    <span className="text-slate-500"> · {r.propertyName} #{r.unitNumber}</span>
                  </div>
                  <span className="text-rose-700 font-medium shrink-0">{fmtMoney(r.currentBalance)}</span>
                </li>
              ))}
            </ul>
          </div>
          <div className="flex items-center justify-between pt-1">
            <div className="text-xs text-slate-500">
              Total amount in controversy:{' '}
              <span className="font-semibold text-slate-800">
                {fmtMoney(selectedRows.reduce((s, r) => s + r.currentBalance, 0))}
              </span>
            </div>
            <div className="flex gap-2">
              <button className="btn-secondary" onClick={() => setBulkOpen(false)}>Cancel</button>
              <button
                className="btn-primary"
                disabled={bulkCreate.isPending || selectedRows.length === 0}
                onClick={() => bulkCreate.mutate()}
              >
                {bulkCreate.isPending
                  ? <Spinner size={14} className="text-white" />
                  : <>Create {selectedRows.length} case(s)</>}
              </button>
            </div>
          </div>
        </div>
      </Modal>
    </div>
  )
}
