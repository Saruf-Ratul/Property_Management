import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useState } from 'react'
import { api, unwrapError } from '@/lib/api'
import type {
  CaseDetail, LeaseDto, LedgerItemDto, PropertyDetailDto, PropertyLedgerSummaryDto,
  TenantDto, UnitDto,
} from '@/types'
import { Tabs } from '@/components/ui/Tabs'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Badge, stageTone } from '@/components/ui/Badge'
import { Stat } from '@/components/ui/Stat'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Spinner'
import {
  ArrowLeft, Building2, Users, Home, DollarSign, AlertTriangle,
  Briefcase, ArrowUpRight, Receipt, FileText,
} from 'lucide-react'
import { fmtDate, fmtMoney, fmtNumber } from '@/lib/format'
import toast from 'react-hot-toast'
import { useAuth } from '@/lib/auth'

type TabId = 'overview' | 'units' | 'tenants' | 'leases' | 'ledger'

const TABS: { id: TabId; label: string }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'units', label: 'Units' },
  { id: 'tenants', label: 'Tenants' },
  { id: 'leases', label: 'Leases' },
  { id: 'ledger', label: 'Ledger summary' },
]

export function PropertyDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const [tab, setTab] = useState<TabId>('overview')

  const detailQ = useQuery({
    queryKey: ['property-detail', id],
    queryFn: async () => (await api.get<PropertyDetailDto>(`/properties/${id}/detail`)).data,
    enabled: !!id,
  })

  const d = detailQ.data
  if (detailQ.isLoading) {
    return <div className="py-16 flex justify-center"><Spinner size={20} /></div>
  }
  if (!d) {
    return (
      <div className="py-16 text-center space-y-3">
        <div className="text-base font-semibold">Property not found</div>
        <Link to="/properties" className="btn-secondary inline-flex">Back to properties</Link>
      </div>
    )
  }

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <Link to="/properties" className="text-xs text-slate-500 hover:text-brand-600 inline-flex items-center gap-1">
            <ArrowLeft size={12} /> All properties
          </Link>
          <h1 className="text-xl font-semibold tracking-tight mt-1">{d.name}</h1>
          <div className="text-sm text-slate-500 flex flex-wrap items-center gap-2 mt-1">
            <Badge tone="blue">{d.provider}</Badge>
            <span>{d.clientName}</span>
            {d.county && <span>· {d.county} County</span>}
            <span className="text-slate-400">· External ID {d.externalId}</span>
            <Badge tone={d.isActive ? 'green' : 'gray'}>{d.isActive ? 'Active' : 'Inactive'}</Badge>
          </div>
          {d.addressLine1 && (
            <div className="text-sm text-slate-700 mt-1">
              {d.addressLine1}{d.addressLine2 ? `, ${d.addressLine2}` : ''} · {d.city}, {d.state} {d.postalCode}
            </div>
          )}
        </div>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-5 gap-4">
        <Stat label="Total Units"      value={fmtNumber(d.unitCount ?? 0)}            icon={<Home size={18} />}        tone="brand" />
        <Stat label="Occupied"         value={fmtNumber(d.occupiedUnitCount)}         icon={<Users size={18} />}       tone="green" />
        <Stat label="Active Leases"    value={fmtNumber(d.activeLeaseCount)}          icon={<Briefcase size={18} />}   tone="brand" />
        <Stat label="Delinquent"       value={fmtNumber(d.delinquentTenantCount)}     icon={<AlertTriangle size={18}/>} tone="rose" />
        <Stat label="Outstanding"      value={fmtMoney(d.outstandingBalance)}         icon={<DollarSign size={18} />}  tone="amber" />
      </div>

      <Tabs<TabId> tabs={TABS} active={tab} onChange={setTab} />

      {tab === 'overview' && <OverviewPanel detail={d} />}
      {tab === 'units' && <UnitsPanel propertyId={d.id} />}
      {tab === 'tenants' && <TenantsPanel propertyId={d.id} clientId={d.clientId} />}
      {tab === 'leases' && <LeasesPanel propertyId={d.id} />}
      {tab === 'ledger' && <LedgerPanel propertyId={d.id} />}
    </div>
  )
}

// ─── Overview ───────────────────────────────────────────────────────────────
function OverviewPanel({ detail }: { detail: PropertyDetailDto }) {
  return (
    <div className="grid lg:grid-cols-2 gap-5">
      <Card>
        <CardHeader title="Property" subtitle="Synced metadata" />
        <CardBody>
          <dl className="grid sm:grid-cols-2 gap-3 text-sm">
            <Field label="Client">{detail.clientName}</Field>
            <Field label="Provider"><Badge tone="blue">{detail.provider}</Badge></Field>
            <Field label="Integration">{detail.integrationDisplayName}</Field>
            <Field label="External ID"><code className="text-xs">{detail.externalId}</code></Field>
            <Field label="County">{detail.county ?? '—'}</Field>
            <Field label="State">{detail.state ?? '—'}</Field>
            <Field label="Created">{fmtDate(detail.createdAtUtc)}</Field>
            <Field label="Status">
              <Badge tone={detail.isActive ? 'green' : 'gray'}>{detail.isActive ? 'Active' : 'Inactive'}</Badge>
            </Field>
          </dl>
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="Financial summary" subtitle="Across active leases" />
        <CardBody>
          <dl className="grid grid-cols-2 gap-3 text-sm">
            <Field label="Active Leases">{fmtNumber(detail.activeLeaseCount)}</Field>
            <Field label="Delinquent">{fmtNumber(detail.delinquentTenantCount)}</Field>
            <Field label="Outstanding">{fmtMoney(detail.outstandingBalance)}</Field>
            <Field label="Avg Market Rent">{fmtMoney(detail.averageMarketRent)}</Field>
            <Field label="Occupied">{detail.occupiedUnitCount}</Field>
            <Field label="Vacant">{detail.vacantUnitCount}</Field>
          </dl>
        </CardBody>
      </Card>
    </div>
  )
}

// ─── Units ──────────────────────────────────────────────────────────────────
function UnitsPanel({ propertyId }: { propertyId: string }) {
  const q = useQuery({
    queryKey: ['property-units', propertyId],
    queryFn: async () => (await api.get<UnitDto[]>(`/properties/${propertyId}/units`)).data,
  })
  const cols: Column<UnitDto>[] = [
    { key: 'num', header: 'Unit', sortKey: 'unitNumber', render: r => <span className="font-medium">{r.unitNumber}</span> },
    { key: 'br', header: 'BR / Bath', render: r => `${r.bedrooms ?? '—'} / ${r.bathrooms ?? '—'}` },
    { key: 'rent', header: 'Market Rent', align: 'right', sortKey: 'marketRent', render: r => fmtMoney(r.marketRent) },
    {
      key: 'occ', header: 'Status',
      render: r => <Badge tone={r.isOccupied ? 'green' : 'gray'}>{r.isOccupied ? 'Occupied' : 'Vacant'}</Badge>,
    },
    { key: 'ext', header: 'External ID', render: r => <code className="text-xs text-slate-500">{r.externalId}</code> },
  ]
  return (
    <DataTable<UnitDto>
      rows={q.data ?? []}
      columns={cols}
      rowKey={r => r.id}
      loading={q.isLoading}
      sort={{ by: 'num', dir: 'asc' }}
      onSortChange={() => {}}
      empty={{ title: 'No units', description: 'This property has no units in the synced data.', icon: <Home size={20} /> }}
    />
  )
}

// ─── Tenants (with Create Case CTA) ─────────────────────────────────────────
function TenantsPanel({ propertyId, clientId }: { propertyId: string; clientId: string }) {
  const { isFirmStaff } = useAuth()
  const nav = useNavigate()
  const qc = useQueryClient()

  const q = useQuery({
    queryKey: ['property-tenants', propertyId],
    queryFn: async () => (await api.get<TenantDto[]>(`/properties/${propertyId}/tenants`)).data,
  })

  const m = useMutation({
    mutationFn: async (t: TenantDto) => {
      const leasesResp = await api.get<LeaseDto[]>(`/tenants/${t.id}/leases`)
      const activeLease = leasesResp.data.find(l => l.isActive) ?? leasesResp.data[0]
      if (!activeLease) throw new Error('Tenant has no leases.')
      const r = await api.post<CaseDetail>('/cases', {
        title: `${t.fullName} — ${activeLease.propertyName} #${activeLease.unitNumber} — Non-payment`,
        caseType: 'LandlordTenantEviction',
        clientId,
        pmsLeaseId: activeLease.id,
        amountInControversy: activeLease.currentBalance,
        description: `Created from property tenants tab. Outstanding balance: $${activeLease.currentBalance.toFixed(2)}.`,
      })
      try { await api.post(`/cases/${r.data.id}/snapshot`) } catch { /* non-fatal */ }
      return r.data
    },
    onSuccess: (c) => {
      toast.success(`Case ${c.caseNumber} created`)
      qc.invalidateQueries({ queryKey: ['cases'] })
      nav(`/cases/${c.id}`)
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const cols: Column<TenantDto>[] = [
    { key: 'name', header: 'Tenant', sortKey: 'fullName', render: r => <span className="font-medium">{r.fullName}</span> },
    { key: 'unit', header: 'Unit', render: r => r.unitNumber ?? '—' },
    { key: 'contact', header: 'Contact', render: r => (
        <div className="text-xs text-slate-600">
          <div>{r.email ?? '—'}</div>
          <div>{r.phone ?? '—'}</div>
        </div>) },
    {
      key: 'bal', header: 'Balance', align: 'right', sortKey: 'currentBalance',
      render: r => <span className={r.currentBalance > 0 ? 'text-rose-700 font-medium' : ''}>{fmtMoney(r.currentBalance)}</span>,
    },
    {
      key: 'act', header: '', align: 'right',
      render: r => isFirmStaff && (
        <button
          className="btn-secondary text-xs"
          onClick={() => m.mutate(r)}
          disabled={m.isPending}
        >
          <Briefcase size={12} /> Create case <ArrowUpRight size={12} />
        </button>
      ),
    },
  ]
  return (
    <DataTable<TenantDto>
      rows={q.data ?? []}
      columns={cols}
      rowKey={r => r.id}
      loading={q.isLoading}
      sort={{ by: 'name', dir: 'asc' }}
      onSortChange={() => {}}
      empty={{ title: 'No active tenants', description: 'No active leases are currently associated with this property.', icon: <Users size={20} /> }}
    />
  )
}

// ─── Leases ─────────────────────────────────────────────────────────────────
function LeasesPanel({ propertyId }: { propertyId: string }) {
  const q = useQuery({
    queryKey: ['property-leases', propertyId],
    queryFn: async () => (await api.get<LeaseDto[]>(`/properties/${propertyId}/leases`)).data,
  })
  const cols: Column<LeaseDto>[] = [
    { key: 'tenant', header: 'Tenant', sortKey: 'tenantName', render: r => <span className="font-medium">{r.tenantName}</span> },
    { key: 'unit', header: 'Unit', render: r => `#${r.unitNumber}` },
    { key: 'start', header: 'Start', sortKey: 'startDate', render: r => fmtDate(r.startDate) },
    { key: 'end', header: 'End', render: r => r.endDate ? fmtDate(r.endDate) : <Badge tone="amber">M-T-M</Badge> },
    { key: 'rent', header: 'Rent', align: 'right', sortKey: 'monthlyRent', render: r => fmtMoney(r.monthlyRent) },
    {
      key: 'bal', header: 'Balance', align: 'right', sortKey: 'currentBalance',
      render: r => <span className={r.currentBalance > 0 ? 'text-rose-700 font-medium' : ''}>{fmtMoney(r.currentBalance)}</span>,
    },
    { key: 'active', header: 'Status', render: r => <Badge tone={r.isActive ? 'green' : 'gray'}>{r.isActive ? 'Active' : 'Inactive'}</Badge> },
  ]
  return (
    <DataTable<LeaseDto>
      rows={q.data ?? []}
      columns={cols}
      rowKey={r => r.id}
      loading={q.isLoading}
      sort={{ by: 'start', dir: 'desc' }}
      onSortChange={() => {}}
      empty={{ title: 'No leases', description: 'No leases found for this property.', icon: <FileText size={20} /> }}
    />
  )
}

// ─── Ledger summary ─────────────────────────────────────────────────────────
function LedgerPanel({ propertyId }: { propertyId: string }) {
  const q = useQuery({
    queryKey: ['property-ledger', propertyId],
    queryFn: async () => (await api.get<PropertyLedgerSummaryDto>(`/properties/${propertyId}/ledger-summary`)).data,
  })

  const cols: Column<LedgerItemDto>[] = [
    { key: 'date', header: 'Date', sortKey: 'postedDate', render: r => fmtDate(r.postedDate) },
    { key: 'cat', header: 'Category', render: r => <Badge tone={r.isPayment ? 'green' : r.isCharge ? 'amber' : 'gray'}>{r.category}</Badge> },
    { key: 'desc', header: 'Description', render: r => r.description ?? '—' },
    {
      key: 'amt', header: 'Amount', align: 'right', sortKey: 'amount',
      render: r => <span className={r.isPayment ? 'text-emerald-700' : r.isCharge ? 'text-rose-700' : ''}>{fmtMoney(r.amount)}</span>,
    },
    { key: 'bal', header: 'Balance', align: 'right', render: r => fmtMoney(r.balance) },
  ]

  if (q.isLoading || !q.data) {
    return <div className="py-12 flex justify-center"><Spinner /></div>
  }

  const s = q.data
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <Stat label="Total Charges"      value={fmtMoney(s.totalCharges)}      icon={<Receipt size={18}/>}     tone="rose" />
        <Stat label="Total Payments"     value={fmtMoney(s.totalPayments)}     icon={<DollarSign size={18}/>}  tone="green" />
        <Stat label="Outstanding"        value={fmtMoney(s.outstandingBalance)} icon={<AlertTriangle size={18}/>} tone="amber" />
        <Stat label="Delinquent Leases"  value={fmtNumber(s.delinquentLeases)} icon={<Briefcase size={18}/>}    tone="brand" />
      </div>

      <Card>
        <CardHeader
          title="Recent ledger items (most recent 20)"
          subtitle={s.oldestUnpaidPostedAt
            ? <>Oldest unpaid charge posted on {fmtDate(s.oldestUnpaidPostedAt)}</>
            : 'No outstanding charges'}
        />
        <CardBody className="p-0">
          <DataTable<LedgerItemDto>
            rows={s.recentItems}
            columns={cols}
            rowKey={r => r.id}
            empty={{ title: 'No ledger entries', description: 'Run a PMS sync to populate ledger items.', icon: <Receipt size={20} /> }}
          />
        </CardBody>
      </Card>
    </div>
  )
}

// ─── helper ─────────────────────────────────────────────────────────────────
function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-500 font-medium">{label}</dt>
      <dd className="mt-0.5 text-slate-800">{children}</dd>
    </div>
  )
}
