import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, unwrapError } from '@/lib/api'
import type {
  CaseDetail, ClientDto, LeaseDto, LedgerItemDto, PagedResult, PropertyDto,
  TenantDetailDto, TenantDto,
} from '@/types'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FilterBar, FilterNumber, FilterSelect, FilterToggle } from '@/components/ui/FilterBar'
import { Drawer } from '@/components/ui/Drawer'
import { Badge } from '@/components/ui/Badge'
import { Stat } from '@/components/ui/Stat'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Spinner'
import {
  Briefcase, Users, ArrowUpRight, DollarSign, Mail, Phone, Calendar, Home, FileText,
} from 'lucide-react'
import { fmtDate, fmtMoney, fmtNumber } from '@/lib/format'
import toast from 'react-hot-toast'
import { useAuth } from '@/lib/auth'

interface Filters {
  search: string
  clientId: string
  propertyId: string
  delinquentOnly: boolean
  minBalance: number | ''
  isActive: '' | 'true' | 'false'
}

export function TenantsPage() {
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<Filters>({
    search: '', clientId: '', propertyId: '',
    delinquentOnly: false, minBalance: '', isActive: '',
  })
  const [drawerId, setDrawerId] = useState<string | null>(null)

  const clientsQ = useQuery({
    queryKey: ['clients-mini'],
    queryFn: async () => (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  // Cascade properties dropdown by selected client
  const propsQ = useQuery({
    queryKey: ['properties-mini', filters.clientId],
    queryFn: async () =>
      (await api.get<PagedResult<PropertyDto>>('/properties', {
        params: { pageSize: 100, clientId: filters.clientId || undefined },
      })).data,
  })

  const tenantsQ = useQuery({
    queryKey: ['tenants', filters, page],
    queryFn: async () =>
      (await api.get<PagedResult<TenantDto>>('/tenants', {
        params: {
          search: filters.search || undefined,
          clientId: filters.clientId || undefined,
          propertyId: filters.propertyId || undefined,
          delinquentOnly: filters.delinquentOnly || undefined,
          minBalance: filters.minBalance === '' ? undefined : filters.minBalance,
          isActive: filters.isActive === '' ? undefined : filters.isActive === 'true',
          page,
          pageSize: 25,
        },
      })).data,
  })

  const hasActiveFilters = useMemo(
    () => !!(filters.clientId || filters.propertyId || filters.delinquentOnly ||
             filters.minBalance !== '' || filters.isActive),
    [filters],
  )

  const cols: Column<TenantDto>[] = [
    {
      key: 'name', header: 'Tenant', sortKey: 'fullName',
      render: r => (
        <div>
          <div className="font-medium text-slate-900">{r.fullName}</div>
          <div className="text-xs text-slate-500">{r.externalId}</div>
        </div>
      ),
    },
    {
      key: 'contact', header: 'Contact',
      render: r => (
        <div className="text-xs text-slate-600 space-y-0.5">
          {r.email && <div className="flex items-center gap-1"><Mail size={10} /> {r.email}</div>}
          {r.phone && <div className="flex items-center gap-1"><Phone size={10} /> {r.phone}</div>}
          {!r.email && !r.phone && <span className="text-slate-400">—</span>}
        </div>
      ),
    },
    {
      key: 'unit', header: 'Property / Unit',
      render: r => r.propertyName
        ? <span>{r.propertyName} · #{r.unitNumber}</span>
        : <span className="text-slate-400">No active lease</span>,
    },
    {
      key: 'bal', header: 'Balance', align: 'right', sortKey: 'currentBalance',
      render: r => <span className={r.currentBalance > 0 ? 'text-rose-700 font-medium' : ''}>{fmtMoney(r.currentBalance)}</span>,
    },
    {
      key: 'active', header: 'Status',
      render: r => <Badge tone={r.isActive ? 'green' : 'gray'}>{r.isActive ? 'Active' : 'Inactive'}</Badge>,
    },
  ]

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Tenants</h1>
        <p className="text-sm text-slate-500">All tenants from synced PMS data, filterable across clients and properties.</p>
      </div>

      <FilterBar
        search={filters.search}
        onSearchChange={v => { setFilters(f => ({ ...f, search: v })); setPage(1) }}
        searchPlaceholder="Search tenants by name, email, or phone…"
        hasActiveFilters={hasActiveFilters}
        onClear={() => {
          setFilters(f => ({ search: f.search, clientId: '', propertyId: '', delinquentOnly: false, minBalance: '', isActive: '' }))
          setPage(1)
        }}
      >
        <FilterSelect
          label="Client"
          value={filters.clientId}
          options={(clientsQ.data?.items ?? []).map(c => ({ value: c.id, label: c.name }))}
          onChange={v => { setFilters(f => ({ ...f, clientId: v, propertyId: '' })); setPage(1) }}
        />
        <FilterSelect
          label="Property"
          value={filters.propertyId}
          options={(propsQ.data?.items ?? []).map(p => ({ value: p.id, label: p.name }))}
          onChange={v => { setFilters(f => ({ ...f, propertyId: v })); setPage(1) }}
        />
        <FilterToggle
          label="Delinquent only"
          checked={filters.delinquentOnly}
          onChange={v => { setFilters(f => ({ ...f, delinquentOnly: v })); setPage(1) }}
        />
        <FilterNumber
          label="Min balance"
          value={filters.minBalance}
          onChange={v => { setFilters(f => ({ ...f, minBalance: v })); setPage(1) }}
        />
        <FilterSelect
          label="Status"
          value={filters.isActive}
          options={[{ value: 'true', label: 'Active' }, { value: 'false', label: 'Inactive' }]}
          onChange={v => { setFilters(f => ({ ...f, isActive: v as any })); setPage(1) }}
        />
      </FilterBar>

      <DataTable<TenantDto>
        rows={tenantsQ.data?.items ?? []}
        columns={cols}
        rowKey={r => r.id}
        loading={tenantsQ.isLoading}
        page={page}
        pageSize={tenantsQ.data?.pageSize ?? 25}
        totalCount={tenantsQ.data?.totalCount ?? 0}
        onPageChange={setPage}
        onRowClick={r => setDrawerId(r.id)}
        sort={{ by: 'name', dir: 'asc' }}
        onSortChange={() => {}}
        empty={{
          title: 'No tenants match your filters',
          description: 'Try removing a filter or running a PMS sync.',
          icon: <Users size={20} />,
        }}
      />

      <TenantDrawer id={drawerId} onClose={() => setDrawerId(null)} />
    </div>
  )
}

// ─── Drawer with tenant + active lease + ledger + Start LT Case CTA ─────────
function TenantDrawer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { isFirmStaff } = useAuth()
  const nav = useNavigate()
  const qc = useQueryClient()

  const detailQ = useQuery({
    queryKey: ['tenant-detail', id],
    queryFn: async () => (await api.get<TenantDetailDto>(`/tenants/${id}`)).data,
    enabled: !!id,
  })

  const leasesQ = useQuery({
    queryKey: ['tenant-leases', id],
    queryFn: async () => (await api.get<LeaseDto[]>(`/tenants/${id}/leases`)).data,
    enabled: !!id,
  })

  const activeLease = leasesQ.data?.find(l => l.isActive) ?? leasesQ.data?.[0]

  const ledgerQ = useQuery({
    queryKey: ['lease-ledger', activeLease?.id],
    queryFn: async () => (await api.get<LedgerItemDto[]>(`/leases/${activeLease!.id}/ledger`)).data,
    enabled: !!activeLease,
  })

  const startCase = useMutation({
    mutationFn: async () => {
      if (!detailQ.data || !activeLease) throw new Error('Tenant has no active lease.')
      const r = await api.post<CaseDetail>('/cases', {
        title: `${detailQ.data.fullName} — ${activeLease.propertyName} #${activeLease.unitNumber} — Non-payment`,
        caseType: 'LandlordTenantEviction',
        clientId: detailQ.data.clientId,
        pmsLeaseId: activeLease.id,
        amountInControversy: activeLease.currentBalance,
        description: `Created from tenants page. Outstanding balance: ${fmtMoney(activeLease.currentBalance)}.`,
      })
      try { await api.post(`/cases/${r.data.id}/snapshot`) } catch { /* non-fatal */ }
      return r.data
    },
    onSuccess: (c) => {
      toast.success(`Case ${c.caseNumber} created`)
      qc.invalidateQueries({ queryKey: ['cases'] })
      onClose()
      nav(`/cases/${c.id}`)
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const d = detailQ.data
  return (
    <Drawer
      open={!!id}
      onClose={onClose}
      title={d?.fullName ?? 'Tenant'}
      subtitle={d ? d.clientName : undefined}
      width="lg"
      footer={
        isFirmStaff && d && activeLease ? (
          <button
            className="btn-primary w-full"
            onClick={() => startCase.mutate()}
            disabled={startCase.isPending}
          >
            {startCase.isPending ? <Spinner size={14} className="text-white" /> : (
              <>
                <Briefcase size={14} /> Start LT case from this tenant <ArrowUpRight size={14} />
              </>
            )}
          </button>
        ) : null
      }
    >
      {detailQ.isLoading || !d ? (
        <div className="py-8 flex justify-center"><Spinner /></div>
      ) : (
        <div className="space-y-5">
          <div className="grid grid-cols-2 gap-3">
            <Stat label="Outstanding balance" value={fmtMoney(d.currentBalance)} icon={<DollarSign size={16} />}
                  tone={d.currentBalance > 0 ? 'rose' : 'green'} />
            <Stat label="Active rent" value={fmtMoney(d.monthlyRent ?? 0)} icon={<Home size={16} />} tone="brand" />
          </div>

          <Card>
            <CardHeader title="Tenant info" />
            <CardBody>
              <dl className="grid grid-cols-2 gap-3 text-sm">
                <Field label="Email">{d.email ?? '—'}</Field>
                <Field label="Phone">{d.phone ?? '—'}</Field>
                <Field label="DOB">{fmtDate(d.dateOfBirth)}</Field>
                <Field label="Status"><Badge tone={d.isActive ? 'green' : 'gray'}>{d.isActive ? 'Active' : 'Inactive'}</Badge></Field>
                <Field label="External ID"><code className="text-xs">{d.externalId}</code></Field>
                <Field label="Integration">{d.integrationDisplayName}</Field>
              </dl>
            </CardBody>
          </Card>

          {activeLease ? (
            <Card>
              <CardHeader
                title="Active lease"
                subtitle={`${activeLease.propertyName} · Unit ${activeLease.unitNumber}`}
              />
              <CardBody>
                <dl className="grid grid-cols-2 gap-3 text-sm">
                  <Field label="Start"><Calendar size={12} className="inline mr-1" />{fmtDate(activeLease.startDate)}</Field>
                  <Field label="End">{activeLease.endDate ? fmtDate(activeLease.endDate) : <Badge tone="amber">Month-to-month</Badge>}</Field>
                  <Field label="Rent">{fmtMoney(activeLease.monthlyRent)}</Field>
                  <Field label="Balance">
                    <span className={activeLease.currentBalance > 0 ? 'text-rose-700 font-medium' : ''}>
                      {fmtMoney(activeLease.currentBalance)}
                    </span>
                  </Field>
                </dl>
              </CardBody>
            </Card>
          ) : (
            <Card>
              <CardBody>
                <div className="text-sm text-slate-500">No active lease for this tenant.</div>
              </CardBody>
            </Card>
          )}

          {activeLease && ledgerQ.data && ledgerQ.data.length > 0 && (
            <Card>
              <CardHeader title="Recent ledger" subtitle="Most recent 10 items" />
              <CardBody className="p-0">
                <DataTable<LedgerItemDto>
                  rows={ledgerQ.data.slice(0, 10)}
                  rowKey={r => r.id}
                  columns={[
                    { key: 'date', header: 'Date', render: r => fmtDate(r.postedDate) },
                    { key: 'cat', header: 'Category', render: r => <Badge tone={r.isPayment ? 'green' : r.isCharge ? 'amber' : 'gray'}>{r.category}</Badge> },
                    { key: 'desc', header: 'Description', render: r => r.description ?? '—' },
                    { key: 'amt', header: 'Amount', align: 'right',
                      render: r => <span className={r.isPayment ? 'text-emerald-700' : r.isCharge ? 'text-rose-700' : ''}>{fmtMoney(r.amount)}</span> },
                    { key: 'bal', header: 'Balance', align: 'right', render: r => fmtMoney(r.balance) },
                  ]}
                  empty={{ title: 'No ledger items', icon: <FileText size={20} /> }}
                />
              </CardBody>
            </Card>
          )}
        </div>
      )}
    </Drawer>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-500 font-medium">{label}</dt>
      <dd className="mt-0.5 text-slate-800">{children}</dd>
    </div>
  )
}
