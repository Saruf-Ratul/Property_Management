import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '@/lib/api'
import type {
  ClientDto, PagedResult, PmsProvider, PropertyDto, PropertyDetailDto,
} from '@/types'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FilterBar, FilterSelect } from '@/components/ui/FilterBar'
import { Drawer } from '@/components/ui/Drawer'
import { Badge } from '@/components/ui/Badge'
import { Building2, ArrowRight, MapPin, Users, DollarSign, Home } from 'lucide-react'
import { fmtMoney, fmtNumber } from '@/lib/format'
import { Stat } from '@/components/ui/Stat'

const PROVIDERS: PmsProvider[] = ['RentManager', 'Yardi', 'AppFolio', 'Buildium', 'PropertyFlow']

interface Filters {
  search: string
  clientId: string
  provider: PmsProvider | ''
  county: string
  isActive: '' | 'true' | 'false'
}

export function PropertiesPage() {
  const nav = useNavigate()
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<Filters>({ search: '', clientId: '', provider: '', county: '', isActive: '' })
  const [drawerId, setDrawerId] = useState<string | null>(null)

  const clientsQ = useQuery({
    queryKey: ['clients-mini'],
    queryFn: async () => (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  const countiesQ = useQuery({
    queryKey: ['property-counties'],
    queryFn: async () => (await api.get<string[]>('/properties/counties')).data,
  })

  const propsQ = useQuery({
    queryKey: ['properties', filters, page],
    queryFn: async () =>
      (await api.get<PagedResult<PropertyDto>>('/properties', {
        params: {
          search: filters.search || undefined,
          clientId: filters.clientId || undefined,
          provider: filters.provider || undefined,
          county: filters.county || undefined,
          isActive: filters.isActive === '' ? undefined : filters.isActive === 'true',
          page,
          pageSize: 25,
        },
      })).data,
  })

  const hasActiveFilters = useMemo(
    () => !!(filters.clientId || filters.provider || filters.county || filters.isActive),
    [filters],
  )

  const columns: Column<PropertyDto>[] = [
    {
      key: 'name',
      header: 'Property',
      sortKey: 'name',
      render: r => (
        <div>
          <div className="font-medium text-slate-900">{r.name}</div>
          <div className="text-xs text-slate-500">{r.externalId}</div>
        </div>
      ),
    },
    {
      key: 'address',
      header: 'Address',
      render: r => {
        const parts = [r.addressLine1, r.city, r.state, r.postalCode].filter(Boolean)
        return parts.length > 0
          ? <span className="text-slate-700">{parts.join(', ')}</span>
          : <span className="text-slate-400">—</span>
      },
    },
    { key: 'county', header: 'County', sortKey: 'county', render: r => r.county ?? <span className="text-slate-400">—</span> },
    { key: 'units', header: 'Units', align: 'right', sortKey: 'unitCount', render: r => r.unitCount ?? '—' },
    {
      key: 'active', header: 'Status',
      render: r => <Badge tone={r.isActive ? 'green' : 'gray'}>{r.isActive ? 'Active' : 'Inactive'}</Badge>,
    },
  ]

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Properties</h1>
          <p className="text-sm text-slate-500">Synced from your PMS integrations across all clients.</p>
        </div>
      </div>

      <FilterBar
        search={filters.search}
        onSearchChange={v => { setFilters(f => ({ ...f, search: v })); setPage(1) }}
        searchPlaceholder="Search by name, address, city, or zip…"
        hasActiveFilters={hasActiveFilters}
        onClear={() => {
          setFilters({ search: filters.search, clientId: '', provider: '', county: '', isActive: '' })
          setPage(1)
        }}
      >
        <FilterSelect
          label="Client"
          value={filters.clientId}
          options={(clientsQ.data?.items ?? []).map(c => ({ value: c.id, label: c.name }))}
          onChange={v => { setFilters(f => ({ ...f, clientId: v })); setPage(1) }}
        />
        <FilterSelect
          label="Provider"
          value={filters.provider}
          options={PROVIDERS.map(p => ({ value: p, label: p }))}
          onChange={v => { setFilters(f => ({ ...f, provider: v as any })); setPage(1) }}
        />
        <FilterSelect
          label="County"
          value={filters.county}
          options={(countiesQ.data ?? []).map(c => ({ value: c, label: c }))}
          onChange={v => { setFilters(f => ({ ...f, county: v })); setPage(1) }}
        />
        <FilterSelect
          label="Status"
          value={filters.isActive}
          options={[{ value: 'true', label: 'Active' }, { value: 'false', label: 'Inactive' }]}
          onChange={v => { setFilters(f => ({ ...f, isActive: v as any })); setPage(1) }}
        />
      </FilterBar>

      <DataTable<PropertyDto>
        rows={propsQ.data?.items ?? []}
        columns={columns}
        rowKey={r => r.id}
        loading={propsQ.isLoading}
        page={page}
        pageSize={propsQ.data?.pageSize ?? 25}
        totalCount={propsQ.data?.totalCount ?? 0}
        onPageChange={setPage}
        sort={{ by: 'name', dir: 'asc' }}
        onSortChange={() => { /* client sort only */ }}
        onRowClick={r => setDrawerId(r.id)}
        empty={{
          title: 'No properties found',
          description: 'Try a different filter combination, or run a PMS sync.',
          icon: <Building2 size={20} />,
        }}
      />

      <PropertyDrawer
        id={drawerId}
        onClose={() => setDrawerId(null)}
        onOpenFull={() => { if (drawerId) nav(`/properties/${drawerId}`) }}
      />
    </div>
  )
}

// ─── Drawer with quick property summary ────────────────────────────────────
function PropertyDrawer({
  id, onClose, onOpenFull,
}: {
  id: string | null
  onClose: () => void
  onOpenFull: () => void
}) {
  const detailQ = useQuery({
    queryKey: ['property-detail', id],
    queryFn: async () => (await api.get<PropertyDetailDto>(`/properties/${id}/detail`)).data,
    enabled: !!id,
  })

  const d = detailQ.data
  return (
    <Drawer
      open={!!id}
      onClose={onClose}
      title={d?.name ?? 'Property'}
      subtitle={d ? <span className="flex items-center gap-1.5"><MapPin size={12} />{[d.addressLine1, d.city, d.state].filter(Boolean).join(', ')}</span> : null}
      width="md"
      footer={
        <button className="btn-primary w-full" onClick={onOpenFull} disabled={!id}>
          Open full property page <ArrowRight size={14} />
        </button>
      }
    >
      {detailQ.isLoading && <div className="text-sm text-slate-500">Loading…</div>}
      {d && (
        <div className="space-y-5">
          <div className="grid grid-cols-2 gap-3">
            <Stat label="Active Leases" value={fmtNumber(d.activeLeaseCount)} icon={<Users size={16} />} tone="brand" />
            <Stat label="Delinquent" value={fmtNumber(d.delinquentTenantCount)} icon={<Users size={16} />} tone="rose" />
            <Stat label="Outstanding" value={fmtMoney(d.outstandingBalance)} icon={<DollarSign size={16} />} tone="amber" />
            <Stat label="Avg Market Rent" value={fmtMoney(d.averageMarketRent)} icon={<Home size={16} />} tone="green" />
          </div>

          <dl className="grid grid-cols-2 gap-3 text-sm">
            <Field label="Client">{d.clientName}</Field>
            <Field label="Provider"><Badge tone="blue">{d.provider}</Badge></Field>
            <Field label="Integration">{d.integrationDisplayName}</Field>
            <Field label="External ID"><span className="text-xs">{d.externalId}</span></Field>
            <Field label="County">{d.county ?? '—'}</Field>
            <Field label="Total Units">{d.unitCount ?? '—'}</Field>
            <Field label="Occupied">{d.occupiedUnitCount}</Field>
            <Field label="Vacant">{d.vacantUnitCount}</Field>
          </dl>
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
