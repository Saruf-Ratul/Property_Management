import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { api, unwrapError } from '@/lib/api'
import { casesApi } from '@/lib/cases'
import type {
  AssigneeDto, ClientDto, LeaseDto, LedgerItemDto, PagedResult, PropertyDto, TenantDto,
} from '@/types'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Spinner'
import { EmptyState } from '@/components/ui/EmptyState'
import { Stat } from '@/components/ui/Stat'
import { DataTable } from '@/components/ui/DataTable'
import { Badge } from '@/components/ui/Badge'
import {
  ArrowLeft, ArrowRight, Briefcase, CheckCircle2, ClipboardCheck, Building2, Users,
  DollarSign, ScrollText, AlertTriangle, Sparkles,
} from 'lucide-react'
import { fmtDate, fmtMoney } from '@/lib/format'

type StepId = 1 | 2 | 3 | 4 | 5

const STEPS: { id: StepId; label: string; icon: typeof Briefcase }[] = [
  { id: 1, label: 'Client',           icon: Users },
  { id: 2, label: 'Property',         icon: Building2 },
  { id: 3, label: 'Tenant & Lease',   icon: Users },
  { id: 4, label: 'Ledger review',    icon: DollarSign },
  { id: 5, label: 'Compliance',       icon: ClipboardCheck },
]

interface Compliance {
  noticeServed: boolean
  registeredMultipleDwelling: boolean
  ledgerVerified: boolean
  attorneyAssigned: boolean
  redactionAcknowledged: boolean
}

export function CaseIntakeWizard() {
  const nav = useNavigate()
  const [params] = useSearchParams()

  const [step, setStep] = useState<StepId>(1)

  // Pre-fill from `?leaseId=…` if user came from the delinquency dashboard.
  const seededLeaseId = params.get('leaseId')

  const [clientId, setClientId] = useState<string>('')
  const [propertyId, setPropertyId] = useState<string>('')
  const [leaseId, setLeaseId] = useState<string>(seededLeaseId ?? '')

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [attorneyId, setAttorneyId] = useState<string>('')
  const [paralegalId, setParalegalId] = useState<string>('')

  const [compliance, setCompliance] = useState<Compliance>({
    noticeServed: false,
    registeredMultipleDwelling: false,
    ledgerVerified: false,
    attorneyAssigned: false,
    redactionAcknowledged: false,
  })

  const allComplianceChecked = Object.values(compliance).every(Boolean)

  // ─── Lookups ────────────────────────────────────────────────────────────
  const clientsQ = useQuery({
    queryKey: ['clients-mini-wiz'],
    queryFn: async () => (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  const propsQ = useQuery({
    queryKey: ['properties-mini-wiz', clientId],
    enabled: !!clientId,
    queryFn: async () =>
      (await api.get<PagedResult<PropertyDto>>('/properties', {
        params: { pageSize: 100, clientId },
      })).data,
  })

  const propTenantsQ = useQuery({
    queryKey: ['property-tenants-wiz', propertyId],
    enabled: !!propertyId,
    queryFn: async () => (await api.get<TenantDto[]>(`/properties/${propertyId}/tenants`)).data,
  })

  const propLeasesQ = useQuery({
    queryKey: ['property-leases-wiz', propertyId],
    enabled: !!propertyId,
    queryFn: async () => (await api.get<LeaseDto[]>(`/properties/${propertyId}/leases`)).data,
  })

  const ledgerQ = useQuery({
    queryKey: ['lease-ledger-wiz', leaseId],
    enabled: !!leaseId,
    queryFn: async () => (await api.get<LedgerItemDto[]>(`/leases/${leaseId}/ledger`)).data,
  })

  const assigneesQ = useQuery({
    queryKey: ['case-assignees-wiz'],
    queryFn: casesApi.assignees,
  })

  const lawyers = (assigneesQ.data ?? []).filter((a: AssigneeDto) => a.role === 'Lawyer' || a.role === 'FirmAdmin')
  const paralegals = (assigneesQ.data ?? []).filter((a: AssigneeDto) => a.role === 'Paralegal')

  // Hydrate from seeded leaseId — find the property/client that owns the lease and skip ahead.
  useEffect(() => {
    if (!seededLeaseId) return
    let cancelled = false
    ;(async () => {
      try {
        // Pull the delinquent list which already exposes client+property ids per lease.
        const dq = (await api.get<PagedResult<{
          tenantId: string; leaseId: string; clientId: string; propertyId: string
        }>>('/tenants/delinquent', { params: { pageSize: 200 } })).data
        const hit = dq.items.find(x => x.leaseId === seededLeaseId)
        if (cancelled || !hit) return
        setClientId(hit.clientId)
        setPropertyId(hit.propertyId)
        setLeaseId(hit.leaseId)
        setStep(4)
      } catch { /* non-fatal — user picks manually */ }
    })()
    return () => { cancelled = true }
  }, [seededLeaseId])

  const lease = (propLeasesQ.data ?? []).find(l => l.id === leaseId) ?? null
  const tenant = lease ? (propTenantsQ.data ?? []).find(t => t.id === lease.tenantId) ?? null : null
  const property = (propsQ.data?.items ?? []).find(p => p.id === propertyId) ?? null

  // Recompute auto-title when lease selected
  useEffect(() => {
    if (lease && tenant && !title) {
      setTitle(`${tenant.fullName} — ${lease.propertyName} #${lease.unitNumber} — Non-payment`)
    }
    if (lease && !description) {
      setDescription(`Outstanding balance: ${fmtMoney(lease.currentBalance)}.`)
    }
  }, [lease, tenant])  // eslint-disable-line

  // Auto-tick compliance items where the system can confirm.
  useEffect(() => {
    setCompliance(c => ({
      ...c,
      ledgerVerified: (ledgerQ.data?.length ?? 0) > 0 ? c.ledgerVerified : c.ledgerVerified,
      attorneyAssigned: !!attorneyId,
    }))
  }, [ledgerQ.data, attorneyId])

  // Step gating
  const canAdvance = useMemo(() => {
    switch (step) {
      case 1: return !!clientId
      case 2: return !!propertyId
      case 3: return !!leaseId
      case 4: return !!leaseId
      case 5: return allComplianceChecked
    }
  }, [step, clientId, propertyId, leaseId, allComplianceChecked])

  const create = useMutation({
    mutationFn: () => casesApi.createFromPms({
      pmsLeaseId: leaseId,
      clientId,
      title: title || undefined,
      caseType: 'LandlordTenantEviction',
      assignedAttorneyId: attorneyId || null,
      assignedParalegalId: paralegalId || null,
      description: description || null,
      complianceConfirmed: allComplianceChecked,
    }),
    onSuccess: (c) => {
      toast.success(`Case ${c.caseNumber} created`)
      nav(`/cases/${c.id}`)
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  return (
    <div className="space-y-5 max-w-5xl mx-auto">
      <div>
        <Link to="/cases" className="text-xs text-slate-500 hover:text-brand-600 inline-flex items-center gap-1">
          <ArrowLeft size={12} /> Cases
        </Link>
        <h1 className="text-xl font-semibold tracking-tight mt-1 inline-flex items-center gap-2">
          <Sparkles size={18} className="text-brand-600" /> Case Intake — Start from PMS
        </h1>
        <p className="text-sm text-slate-500">
          Five guided steps. We'll snapshot the PMS data onto the case so future PMS changes don't alter generated forms.
        </p>
      </div>

      <Stepper step={step} />

      <Card>
        <CardBody>
          {step === 1 && (
            <Step1Client
              clients={clientsQ.data?.items ?? []}
              loading={clientsQ.isLoading}
              value={clientId}
              onChange={(v) => { setClientId(v); setPropertyId(''); setLeaseId('') }}
            />
          )}
          {step === 2 && (
            <Step2Property
              loading={propsQ.isLoading}
              clientName={clientsQ.data?.items.find(c => c.id === clientId)?.name}
              properties={propsQ.data?.items ?? []}
              value={propertyId}
              onChange={(v) => { setPropertyId(v); setLeaseId('') }}
            />
          )}
          {step === 3 && (
            <Step3TenantLease
              loading={propLeasesQ.isLoading}
              property={property}
              leases={propLeasesQ.data ?? []}
              tenants={propTenantsQ.data ?? []}
              value={leaseId}
              onChange={setLeaseId}
            />
          )}
          {step === 4 && (
            <Step4Ledger
              loading={ledgerQ.isLoading}
              tenant={tenant}
              property={property}
              lease={lease}
              ledger={ledgerQ.data ?? []}
              title={title}
              onTitleChange={setTitle}
              description={description}
              onDescriptionChange={setDescription}
            />
          )}
          {step === 5 && (
            <Step5Compliance
              attorneyId={attorneyId}
              onAttorneyChange={setAttorneyId}
              paralegalId={paralegalId}
              onParalegalChange={setParalegalId}
              lawyers={lawyers}
              paralegals={paralegals}
              compliance={compliance}
              onComplianceChange={setCompliance}
            />
          )}
        </CardBody>
      </Card>

      <div className="flex justify-between items-center">
        <button className="btn-secondary" disabled={step === 1}
                onClick={() => setStep(s => (s - 1) as StepId)}>
          <ArrowLeft size={14} /> Back
        </button>
        <div className="text-xs text-slate-500">Step {step} of {STEPS.length}</div>
        {step < 5 ? (
          <button className="btn-primary" disabled={!canAdvance}
                  onClick={() => setStep(s => (s + 1) as StepId)}>
            Next <ArrowRight size={14} />
          </button>
        ) : (
          <button className="btn-primary"
                  disabled={!canAdvance || create.isPending}
                  onClick={() => create.mutate()}>
            {create.isPending ? <Spinner size={14} className="text-white" /> : (
              <><CheckCircle2 size={14} /> Create case</>
            )}
          </button>
        )}
      </div>
    </div>
  )
}

// ─── stepper indicator ──────────────────────────────────────────────────────
function Stepper({ step }: { step: StepId }) {
  return (
    <nav className="card p-3">
      <ol className="flex items-center gap-2 overflow-x-auto">
        {STEPS.map((s, i) => {
          const done = step > s.id
          const active = step === s.id
          return (
            <li key={s.id} className="flex-1 min-w-[120px]">
              <div className={`flex items-center gap-2 px-3 py-2 rounded-lg ${
                active ? 'bg-brand-50 text-brand-800 ring-1 ring-brand-200' : done ? 'text-emerald-700' : 'text-slate-500'}`}>
                <div className={`size-6 rounded-full flex items-center justify-center text-[11px] font-semibold ${
                  active ? 'bg-brand-600 text-white' : done ? 'bg-emerald-600 text-white' : 'bg-slate-200 text-slate-700'}`}>
                  {done ? <CheckCircle2 size={12} /> : s.id}
                </div>
                <div className="flex items-center gap-1.5 text-sm font-medium">
                  <s.icon size={13} /> {s.label}
                </div>
              </div>
              {i < STEPS.length - 1 && <div className="h-px bg-slate-100" />}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}

// ─── Step 1 — pick client ───────────────────────────────────────────────────
function Step1Client({
  clients, loading, value, onChange,
}: { clients: ClientDto[]; loading: boolean; value: string; onChange: (v: string) => void }) {
  return (
    <div className="space-y-3">
      <h3 className="text-sm font-semibold text-slate-800">Select the property-management client</h3>
      <p className="text-xs text-slate-500">The case must belong to a single client (the property manager / landlord).</p>
      {loading && <Spinner />}
      {!loading && clients.length === 0 && (
        <EmptyState title="No clients yet" description="Add a client first." icon={<Users size={20} />} />
      )}
      <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {clients.map(c => (
          <button
            key={c.id}
            type="button"
            onClick={() => onChange(c.id)}
            className={`text-left card p-4 hover:ring-brand-300 transition ${
              value === c.id ? 'ring-2 ring-brand-500 bg-brand-50/40' : ''}`}
          >
            <div className="font-medium text-slate-900">{c.name}</div>
            <div className="text-xs text-slate-500 mt-0.5">{c.contactName ?? '—'}</div>
            <div className="flex items-center gap-2 mt-2 text-xs text-slate-500">
              <Badge tone="blue">{c.integrationsCount} integration(s)</Badge>
              <Badge tone="gray">{c.casesCount} case(s)</Badge>
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}

// ─── Step 2 — pick property ─────────────────────────────────────────────────
function Step2Property({
  clientName, properties, loading, value, onChange,
}: { clientName?: string; properties: PropertyDto[]; loading: boolean; value: string; onChange: (v: string) => void }) {
  return (
    <div className="space-y-3">
      <h3 className="text-sm font-semibold text-slate-800">Select the property</h3>
      <p className="text-xs text-slate-500">{clientName ? <>Showing properties for <b>{clientName}</b>.</> : 'Pick a client first.'}</p>
      {loading && <Spinner />}
      {!loading && properties.length === 0 && (
        <EmptyState title="No properties for this client" description="Trigger a PMS sync first." icon={<Building2 size={20} />} />
      )}
      <div className="grid sm:grid-cols-2 gap-3">
        {properties.map(p => (
          <button
            key={p.id}
            type="button"
            onClick={() => onChange(p.id)}
            className={`text-left card p-4 hover:ring-brand-300 transition ${
              value === p.id ? 'ring-2 ring-brand-500 bg-brand-50/40' : ''}`}
          >
            <div className="font-medium text-slate-900">{p.name}</div>
            <div className="text-xs text-slate-500">
              {[p.addressLine1, p.city, p.state].filter(Boolean).join(', ')}
            </div>
            <div className="text-xs text-slate-500 mt-1">{p.county ?? 'No county'} · {p.unitCount ?? '—'} units</div>
          </button>
        ))}
      </div>
    </div>
  )
}

// ─── Step 3 — pick tenant / lease ───────────────────────────────────────────
function Step3TenantLease({
  property, tenants, leases, loading, value, onChange,
}: {
  property: PropertyDto | null
  tenants: TenantDto[]
  leases: LeaseDto[]
  loading: boolean
  value: string
  onChange: (v: string) => void
}) {
  // Show one row per active lease; tenants prop is just for cross-reference
  const activeLeases = leases.filter(l => l.isActive)
  return (
    <div className="space-y-3">
      <h3 className="text-sm font-semibold text-slate-800">Pick the tenant / lease for this case</h3>
      <p className="text-xs text-slate-500">{property ? <>Showing active leases at <b>{property.name}</b>.</> : 'Pick a property first.'}</p>
      <DataTable<LeaseDto>
        rows={activeLeases}
        loading={loading}
        rowKey={r => r.id}
        onRowClick={r => onChange(r.id)}
        columns={[
          { key: 'select', header: '', className: 'w-8',
            render: r => <input type="radio" checked={value === r.id} readOnly /> },
          { key: 'tenant', header: 'Tenant', sortKey: 'tenantName',
            render: r => <span className="font-medium">{r.tenantName}</span> },
          { key: 'unit', header: 'Unit', render: r => `#${r.unitNumber}` },
          { key: 'rent', header: 'Rent', align: 'right', render: r => fmtMoney(r.monthlyRent) },
          { key: 'bal', header: 'Balance', align: 'right',
            render: r => <span className={r.currentBalance > 0 ? 'text-rose-700 font-medium' : ''}>{fmtMoney(r.currentBalance)}</span> },
          { key: 'start', header: 'Start', render: r => fmtDate(r.startDate) },
          { key: 'end', header: 'End', render: r => r.endDate ? fmtDate(r.endDate) : <Badge tone="amber">M-T-M</Badge> },
        ]}
        empty={{ title: 'No active leases', icon: <Users size={20} /> }}
      />
    </div>
  )
}

// ─── Step 4 — review ledger + title ─────────────────────────────────────────
function Step4Ledger({
  loading, tenant, property, lease, ledger,
  title, onTitleChange, description, onDescriptionChange,
}: {
  loading: boolean
  tenant: TenantDto | null
  property: PropertyDto | null
  lease: LeaseDto | null
  ledger: LedgerItemDto[]
  title: string
  onTitleChange: (v: string) => void
  description: string
  onDescriptionChange: (v: string) => void
}) {
  if (!lease) {
    return <EmptyState title="No lease selected" icon={<AlertTriangle size={20} />} />
  }
  const totalCharges = ledger.filter(l => l.isCharge).reduce((s, l) => s + l.amount, 0)
  const totalPayments = ledger.filter(l => l.isPayment).reduce((s, l) => s + Math.abs(l.amount), 0)
  return (
    <div className="space-y-4">
      <h3 className="text-sm font-semibold text-slate-800">Review the ledger and title the case</h3>

      <div className="grid sm:grid-cols-3 gap-4">
        <Stat label="Outstanding balance" value={fmtMoney(lease.currentBalance)} icon={<DollarSign size={16}/>} tone="rose" />
        <Stat label="Total charges"       value={fmtMoney(totalCharges)} icon={<DollarSign size={16}/>} tone="amber" />
        <Stat label="Total payments"      value={fmtMoney(totalPayments)} icon={<DollarSign size={16}/>} tone="green" />
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-1 space-y-3 text-sm">
          <div><span className="text-slate-500">Tenant</span><div className="font-medium">{tenant?.fullName ?? '—'}</div></div>
          <div><span className="text-slate-500">Property</span><div>{property?.name ?? '—'}</div></div>
          <div><span className="text-slate-500">Unit</span><div>#{lease.unitNumber}</div></div>
          <div><span className="text-slate-500">Rent</span><div>{fmtMoney(lease.monthlyRent)}</div></div>
          <div><span className="text-slate-500">Lease term</span>
            <div>{fmtDate(lease.startDate)} → {lease.endDate ? fmtDate(lease.endDate) : 'M-T-M'}</div>
          </div>
        </div>
        <div className="lg:col-span-2 space-y-3">
          <div>
            <label className="label">Case title</label>
            <input className="input" value={title} onChange={e => onTitleChange(e.target.value)} />
          </div>
          <div>
            <label className="label">Description</label>
            <textarea className="input min-h-[80px]" value={description} onChange={e => onDescriptionChange(e.target.value)} />
          </div>
        </div>
      </div>

      <DataTable<LedgerItemDto>
        rows={ledger.slice(0, 25)}
        loading={loading}
        rowKey={r => r.id}
        columns={[
          { key: 'date', header: 'Date', render: r => fmtDate(r.postedDate) },
          { key: 'cat',  header: 'Category',
            render: r => <Badge tone={r.isPayment ? 'green' : r.isCharge ? 'amber' : 'gray'}>{r.category}</Badge> },
          { key: 'desc', header: 'Description', render: r => r.description ?? '—' },
          { key: 'amt',  header: 'Amount', align: 'right',
            render: r => <span className={r.isPayment ? 'text-emerald-700' : r.isCharge ? 'text-rose-700' : ''}>{fmtMoney(r.amount)}</span> },
          { key: 'bal',  header: 'Balance', align: 'right', render: r => fmtMoney(r.balance) },
        ]}
        empty={{ title: 'No ledger items found for this lease', icon: <ScrollText size={20}/> }}
      />
    </div>
  )
}

// ─── Step 5 — compliance + assignment ───────────────────────────────────────
function Step5Compliance({
  attorneyId, onAttorneyChange, paralegalId, onParalegalChange,
  lawyers, paralegals, compliance, onComplianceChange,
}: {
  attorneyId: string; onAttorneyChange: (v: string) => void
  paralegalId: string; onParalegalChange: (v: string) => void
  lawyers: AssigneeDto[]; paralegals: AssigneeDto[]
  compliance: Compliance; onComplianceChange: (c: Compliance) => void
}) {
  const items: { key: keyof Compliance; label: string; help: string }[] = [
    { key: 'noticeServed', label: 'Notice to quit / cease has been served', help: 'Confirm certified mail or personal service.' },
    { key: 'registeredMultipleDwelling', label: 'Property is registered as a multiple dwelling (if applicable)', help: 'NJ requires this for properties with 3+ units.' },
    { key: 'ledgerVerified', label: 'Ledger balance is current and verified', help: 'Verify the outstanding amount with the property manager.' },
    { key: 'attorneyAssigned', label: 'Attorney is assigned for review', help: 'Auto-checked when you pick an attorney below.' },
    { key: 'redactionAcknowledged', label: 'I acknowledge SSN/DL/bank/card numbers must not appear on public forms', help: 'Generated PDFs are checked, but operator confirmation is required.' },
  ]
  return (
    <div className="space-y-5">
      <h3 className="text-sm font-semibold text-slate-800">Compliance checklist</h3>
      <p className="text-xs text-slate-500">Confirm each item before creating the case.</p>

      <Card>
        <CardHeader title="Pre-filing checklist" />
        <CardBody>
          <ul className="space-y-3">
            {items.map(item => (
              <li key={item.key} className="flex items-start gap-3">
                <input type="checkbox" className="mt-1"
                       checked={compliance[item.key]}
                       onChange={e => onComplianceChange({ ...compliance, [item.key]: e.target.checked })} />
                <div>
                  <div className="text-sm font-medium text-slate-800">{item.label}</div>
                  <div className="text-xs text-slate-500">{item.help}</div>
                </div>
              </li>
            ))}
          </ul>
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="Assignment" subtitle="Optional but recommended" />
        <CardBody>
          <div className="grid sm:grid-cols-2 gap-3">
            <div>
              <label className="label">Attorney</label>
              <select className="input" value={attorneyId} onChange={e => onAttorneyChange(e.target.value)}>
                <option value="">— Unassigned —</option>
                {lawyers.map(l => <option key={l.id} value={l.id}>{l.fullName} ({l.role})</option>)}
              </select>
            </div>
            <div>
              <label className="label">Paralegal</label>
              <select className="input" value={paralegalId} onChange={e => onParalegalChange(e.target.value)}>
                <option value="">— Unassigned —</option>
                {paralegals.map(l => <option key={l.id} value={l.id}>{l.fullName}</option>)}
              </select>
            </div>
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
