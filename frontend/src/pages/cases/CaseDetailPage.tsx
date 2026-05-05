import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useState } from 'react'
import toast from 'react-hot-toast'
import { api, unwrapError } from '@/lib/api'
import { casesApi } from '@/lib/cases'
import type {
  CaseActivityDto, CaseCommentDto, CaseDetail, CaseDocumentDto,
  CasePaymentDto, CaseSnapshotData, CaseSnapshotDto, GeneratedDocumentDto,
  LedgerItemDto,
} from '@/types'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Tabs } from '@/components/ui/Tabs'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { Badge, stageTone } from '@/components/ui/Badge'
import { Spinner } from '@/components/ui/Spinner'
import { EmptyState } from '@/components/ui/EmptyState'
import {
  ArrowLeft, Briefcase, Camera, FileText, Receipt, MessageSquare, Activity,
  Layers, Folder, FilePlus2, Download, Send, Upload, UserCog, Lock, Pencil,
  Home, MapPin, AlertCircle,
} from 'lucide-react'
import { fmtDate, fmtDateTime, fmtMoney } from '@/lib/format'
import { useAuth } from '@/lib/auth'
import { AssignModal, CloseModal, StatusModal } from '@/components/cases/CaseActionModals'

type TabId = 'overview' | 'snapshot' | 'ledger' | 'documents' | 'forms' | 'payments' | 'comments' | 'activity'

const TABS: { id: TabId; label: string; icon: typeof Briefcase }[] = [
  { id: 'overview',  label: 'Overview',         icon: Briefcase },
  { id: 'snapshot',  label: 'Tenant/Property',  icon: Home },
  { id: 'ledger',    label: 'Ledger',           icon: Receipt },
  { id: 'documents', label: 'Documents',        icon: Folder },
  { id: 'forms',     label: 'Forms',            icon: FileText },
  { id: 'payments',  label: 'Payments',         icon: Receipt },
  { id: 'comments',  label: 'Comments',         icon: MessageSquare },
  { id: 'activity',  label: 'Activity',         icon: Activity },
]

export function CaseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const qc = useQueryClient()
  const { isFirmStaff, hasAnyRole } = useAuth()
  const canManage = hasAnyRole(['FirmAdmin', 'Lawyer'])

  const [tab, setTab] = useState<TabId>('overview')
  const [statusOpen, setStatusOpen] = useState(false)
  const [assignOpen, setAssignOpen] = useState(false)
  const [closeOpen, setCloseOpen] = useState(false)

  const caseQ = useQuery({
    queryKey: ['case', id],
    queryFn: async () => casesApi.get(id!),
    enabled: !!id,
  })

  const snapshotMutation = useMutation({
    mutationFn: () => casesApi.snapshotPms(id!),
    onSuccess: () => {
      toast.success('Snapshot updated from PMS')
      qc.invalidateQueries({ queryKey: ['case', id] })
      qc.invalidateQueries({ queryKey: ['case-snapshot', id] })
      qc.invalidateQueries({ queryKey: ['case-activities', id] })
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const c = caseQ.data
  if (caseQ.isLoading || !c) {
    return <div className="py-16 flex justify-center"><Spinner /></div>
  }

  const isClosed = c.statusCode === 'Closed' || c.statusCode === 'Cancelled'

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <Link to="/cases" className="text-xs text-slate-500 hover:text-brand-600 inline-flex items-center gap-1">
            <ArrowLeft size={12} /> All cases
          </Link>
          <h1 className="text-xl font-semibold tracking-tight mt-1">
            <span className="text-slate-500 font-mono mr-2">{c.caseNumber}</span>
            {c.title}
          </h1>
          <div className="text-sm text-slate-500 flex flex-wrap items-center gap-2 mt-1.5">
            <Badge tone={stageTone(c.stageCode)}>{c.stageName}</Badge>
            <Badge tone={c.statusCode === 'Open' ? 'green' : 'gray'}>{c.statusName}</Badge>
            <span>· {c.clientName}</span>
            {c.assignedAttorney && <span>· Atty {c.assignedAttorney}</span>}
            {c.assignedParalegal && <span>· {c.assignedParalegal}</span>}
            {c.amountInControversy != null && <span>· {fmtMoney(c.amountInControversy)}</span>}
          </div>
        </div>

        {isFirmStaff && (
          <div className="flex flex-wrap gap-2">
            {c.pmsLeaseId && (
              <button className="btn-secondary" onClick={() => snapshotMutation.mutate()}
                disabled={snapshotMutation.isPending || isClosed}>
                <Camera size={14} />
                {snapshotMutation.isPending ? 'Snapshotting…' : 'Snapshot PMS'}
              </button>
            )}
            <button className="btn-secondary" disabled={isClosed} onClick={() => setStatusOpen(true)}>
              <Pencil size={14} /> Update status
            </button>
            {canManage && (
              <button className="btn-secondary" disabled={isClosed} onClick={() => setAssignOpen(true)}>
                <UserCog size={14} /> Assign
              </button>
            )}
            <Link to={`/cases/${c.id}/forms`} className="btn-primary">
              <FilePlus2 size={14} /> Form wizard
            </Link>
            {canManage && !isClosed && (
              <button className="btn-danger" onClick={() => setCloseOpen(true)}>
                <Lock size={14} /> Close case
              </button>
            )}
          </div>
        )}
      </div>

      <Tabs<TabId>
        tabs={TABS.map(t => ({
          id: t.id,
          label: <span className="inline-flex items-center gap-1.5"><t.icon size={13} /> {t.label}</span>,
        }))}
        active={tab}
        onChange={setTab}
      />

      {tab === 'overview' && <OverviewTab caseDetail={c} />}
      {tab === 'snapshot' && <SnapshotTab caseId={c.id} />}
      {tab === 'ledger' && <LedgerTab leaseId={c.pmsLeaseId} />}
      {tab === 'documents' && <DocumentsTab caseId={c.id} canUpload={isFirmStaff} />}
      {tab === 'forms' && <FormsTab caseId={c.id} />}
      {tab === 'payments' && <PaymentsTab caseId={c.id} canAdd={isFirmStaff} />}
      {tab === 'comments' && <CommentsTab caseId={c.id} />}
      {tab === 'activity' && <ActivityTab caseId={c.id} />}

      <StatusModal open={statusOpen} onClose={() => setStatusOpen(false)} caseDetail={c} />
      <AssignModal open={assignOpen} onClose={() => setAssignOpen(false)} caseDetail={c} />
      <CloseModal  open={closeOpen}  onClose={() => setCloseOpen(false)}  caseDetail={c} />
    </div>
  )
}

// ─── Tab: Overview ──────────────────────────────────────────────────────────
function OverviewTab({ caseDetail: c }: { caseDetail: CaseDetail }) {
  return (
    <div className="grid lg:grid-cols-3 gap-5">
      <Card className="lg:col-span-2">
        <CardHeader title="Case info" />
        <CardBody>
          <dl className="grid sm:grid-cols-2 gap-3 text-sm">
            <Field label="Client">{c.clientName}</Field>
            <Field label="Type">{c.caseType.replace(/([A-Z])/g, ' $1').trim()}</Field>
            <Field label="Attorney">{c.assignedAttorney ?? '—'}</Field>
            <Field label="Paralegal">{c.assignedParalegal ?? '—'}</Field>
            <Field label="Amount in controversy">{fmtMoney(c.amountInControversy)}</Field>
            <Field label="Court venue">{c.courtVenue ?? '—'}</Field>
            <Field label="Filed on">{fmtDate(c.filedOnUtc)}</Field>
            <Field label="Court date">{fmtDateTime(c.courtDateUtc)}</Field>
            <Field label="Docket #">{c.courtDocketNumber ?? '—'}</Field>
            <Field label="Outcome">{c.outcome ?? '—'}</Field>
            <Field label="Created">{fmtDateTime(c.createdAtUtc)}</Field>
            <Field label="PMS snapshot">
              {c.pmsSnapshotTakenAtUtc
                ? <Badge tone="blue">Captured {fmtDateTime(c.pmsSnapshotTakenAtUtc)}</Badge>
                : <Badge tone="gray">Not captured</Badge>}
            </Field>
          </dl>
          {c.description && (
            <div className="mt-4">
              <div className="text-xs uppercase tracking-wide text-slate-500 font-medium mb-1">Description</div>
              <p className="text-sm text-slate-700 whitespace-pre-wrap">{c.description}</p>
            </div>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardHeader title="LT case data" />
        <CardBody>
          {c.ltCase ? (
            <dl className="space-y-2 text-sm">
              <Field label="Premises">
                {[c.ltCase.premisesAddressLine1, c.ltCase.premisesCity, c.ltCase.premisesState].filter(Boolean).join(', ') || '—'}
              </Field>
              <Field label="County">{c.ltCase.premisesCounty ?? '—'}</Field>
              <Field label="Landlord">{c.ltCase.landlordName ?? '—'}</Field>
              <Field label="Total due">{fmtMoney(c.ltCase.totalDue)}</Field>
              <Field label="Rent due as of">{fmtDate(c.ltCase.rentDueAsOf)}</Field>
              <Field label="Multiple dwelling registered">{c.ltCase.isRegisteredMultipleDwelling ? 'Yes' : 'No'}</Field>
              <Field label="Attorney reviewed">
                <Badge tone={c.ltCase.attorneyReviewed ? 'green' : 'amber'}>
                  {c.ltCase.attorneyReviewed ? 'Yes' : 'Pending'}
                </Badge>
              </Field>
            </dl>
          ) : <p className="text-sm text-slate-500">No LT data yet.</p>}
        </CardBody>
      </Card>
    </div>
  )
}

// ─── Tab: Tenant/Property snapshot ──────────────────────────────────────────
function SnapshotTab({ caseId }: { caseId: string }) {
  const q = useQuery({
    queryKey: ['case-snapshot', caseId],
    queryFn: async () => casesApi.snapshot(caseId),
  })

  if (q.isLoading || !q.data) return <div className="py-12 flex justify-center"><Spinner /></div>
  const s: CaseSnapshotDto = q.data
  if (!s.data) {
    return (
      <Card>
        <CardBody>
          <EmptyState
            title="No snapshot captured yet"
            description="Click 'Snapshot PMS' on the case header to copy current PMS data into this case."
            icon={<Camera size={20} />}
          />
        </CardBody>
      </Card>
    )
  }

  const d: CaseSnapshotData = s.data
  return (
    <div className="space-y-5">
      <div className="text-xs text-slate-500">
        Snapshot taken {fmtDateTime(s.takenAtUtc)} · the case will keep this version even if the PMS changes.
      </div>

      <div className="grid lg:grid-cols-2 gap-5">
        <Card>
          <CardHeader title="Property" subtitle={<span className="inline-flex items-center gap-1"><MapPin size={11}/> Premises</span>} />
          <CardBody>
            {d.property ? (
              <dl className="grid grid-cols-2 gap-3 text-sm">
                <Field label="Name">{d.property.name}</Field>
                <Field label="County">{d.property.county ?? '—'}</Field>
                <Field label="Address">{d.property.addressLine1 ?? '—'}</Field>
                <Field label="City">{d.property.city ?? '—'}</Field>
                <Field label="State">{d.property.state ?? '—'}</Field>
                <Field label="Zip">{d.property.postalCode ?? '—'}</Field>
              </dl>
            ) : <p className="text-sm text-slate-500">No property data.</p>}
          </CardBody>
        </Card>

        <Card>
          <CardHeader title="Unit" />
          <CardBody>
            {d.unit ? (
              <dl className="grid grid-cols-2 gap-3 text-sm">
                <Field label="Unit #">{d.unit.unitNumber}</Field>
                <Field label="Beds / Baths">{d.unit.bedrooms ?? '—'} / {d.unit.bathrooms ?? '—'}</Field>
                <Field label="Market rent">{fmtMoney(d.unit.marketRent)}</Field>
              </dl>
            ) : <p className="text-sm text-slate-500">No unit data.</p>}
          </CardBody>
        </Card>

        <Card>
          <CardHeader title="Tenant" />
          <CardBody>
            {d.tenant ? (
              <dl className="grid grid-cols-2 gap-3 text-sm">
                <Field label="First name">{d.tenant.firstName}</Field>
                <Field label="Last name">{d.tenant.lastName}</Field>
                <Field label="Email">{d.tenant.email ?? '—'}</Field>
                <Field label="Phone">{d.tenant.phone ?? '—'}</Field>
              </dl>
            ) : <p className="text-sm text-slate-500">No tenant data.</p>}
          </CardBody>
        </Card>

        <Card>
          <CardHeader title="Lease" />
          <CardBody>
            {d.lease ? (
              <dl className="grid grid-cols-2 gap-3 text-sm">
                <Field label="Start">{fmtDate(d.lease.startDate)}</Field>
                <Field label="End">{d.lease.endDate ? fmtDate(d.lease.endDate) : <Badge tone="amber">M-T-M</Badge>}</Field>
                <Field label="Monthly rent">{fmtMoney(d.lease.monthlyRent)}</Field>
                <Field label="Security deposit">{fmtMoney(d.lease.securityDeposit)}</Field>
                <Field label="Current balance">
                  <span className={(d.lease.currentBalance ?? 0) > 0 ? 'text-rose-700 font-medium' : ''}>
                    {fmtMoney(d.lease.currentBalance)}
                  </span>
                </Field>
              </dl>
            ) : <p className="text-sm text-slate-500">No lease data.</p>}
          </CardBody>
        </Card>
      </div>

      <Card>
        <CardHeader title="Snapshot ledger (50 most recent)" />
        <CardBody className="p-0">
          <DataTable
            rows={d.ledger ?? []}
            rowKey={r => `${r.postedDate}-${r.category}-${r.amount}`}
            columns={[
              { key: 'date', header: 'Date', render: r => fmtDate(r.postedDate) },
              { key: 'cat',  header: 'Category',
                render: r => <Badge tone={r.isPayment ? 'green' : r.isCharge ? 'amber' : 'gray'}>{r.category}</Badge> },
              { key: 'desc', header: 'Description', render: r => r.description ?? '—' },
              { key: 'amt',  header: 'Amount', align: 'right',
                render: r => <span className={r.isPayment ? 'text-emerald-700' : r.isCharge ? 'text-rose-700' : ''}>{fmtMoney(r.amount)}</span> },
              { key: 'bal',  header: 'Balance', align: 'right', render: r => fmtMoney(r.balance) },
            ]}
            empty={{ title: 'No ledger items in snapshot', icon: <Receipt size={20} /> }}
          />
        </CardBody>
      </Card>
    </div>
  )
}

// ─── Tab: Ledger (live PMS) ─────────────────────────────────────────────────
function LedgerTab({ leaseId }: { leaseId: string | null }) {
  const q = useQuery({
    queryKey: ['lease-ledger', leaseId],
    queryFn: async () => casesApi.ledger(leaseId!),
    enabled: !!leaseId,
  })

  if (!leaseId) {
    return (
      <Card><CardBody>
        <EmptyState
          title="Case is not linked to a PMS lease"
          description="Live ledger is only available for cases created from PMS data."
          icon={<AlertCircle size={20} />}
        />
      </CardBody></Card>
    )
  }

  return (
    <Card>
      <CardHeader
        title="Live ledger"
        subtitle="Pulled from the linked PMS lease — reflects current state, not the case snapshot."
      />
      <CardBody className="p-0">
        <DataTable<LedgerItemDto>
          rows={q.data ?? []}
          rowKey={r => r.id}
          loading={q.isLoading}
          columns={[
            { key: 'date', header: 'Posted',  render: r => fmtDate(r.postedDate) },
            { key: 'due',  header: 'Due',     render: r => r.dueDate ? fmtDate(r.dueDate) : '—' },
            { key: 'cat',  header: 'Category',
              render: r => <Badge tone={r.isPayment ? 'green' : r.isCharge ? 'amber' : 'gray'}>{r.category}</Badge> },
            { key: 'desc', header: 'Description', render: r => r.description ?? '—' },
            { key: 'amt',  header: 'Amount', align: 'right',
              render: r => <span className={r.isPayment ? 'text-emerald-700' : r.isCharge ? 'text-rose-700' : ''}>{fmtMoney(r.amount)}</span> },
            { key: 'bal',  header: 'Balance', align: 'right', render: r => fmtMoney(r.balance) },
          ]}
          empty={{ title: 'No ledger items', icon: <Receipt size={20} /> }}
        />
      </CardBody>
    </Card>
  )
}

// ─── Tab: Documents (uploaded) ──────────────────────────────────────────────
function DocumentsTab({ caseId, canUpload }: { caseId: string; canUpload: boolean }) {
  const qc = useQueryClient()
  const [uploading, setUploading] = useState(false)
  const q = useQuery({ queryKey: ['case-documents', caseId], queryFn: () => casesApi.documents(caseId) })

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0]; if (!f) return
    setUploading(true)
    try {
      const fd = new FormData()
      fd.append('file', f)
      fd.append('documentType', 'Other')
      fd.append('isClientVisible', 'true')
      await api.post(`/cases/${caseId}/documents`, fd, { headers: { 'Content-Type': 'multipart/form-data' } })
      qc.invalidateQueries({ queryKey: ['case-documents', caseId] })
      qc.invalidateQueries({ queryKey: ['case-activities', caseId] })
      toast.success(`Uploaded ${f.name}`)
    } catch (err) { toast.error(unwrapError(err)) }
    finally { setUploading(false); e.target.value = '' }
  }

  return (
    <Card>
      <CardHeader
        title="Case documents"
        subtitle={`${q.data?.length ?? 0} attached`}
        action={canUpload ? (
          <label className="btn-secondary cursor-pointer">
            <Upload size={14} /> {uploading ? 'Uploading…' : 'Upload'}
            <input type="file" className="hidden" onChange={onFile} disabled={uploading} />
          </label>
        ) : null}
      />
      <CardBody className="p-0">
        <DataTable<CaseDocumentDto>
          rows={q.data ?? []}
          rowKey={r => r.id}
          loading={q.isLoading}
          columns={[
            { key: 'name', header: 'File', sortKey: 'fileName', render: r => <span className="font-medium">{r.fileName}</span> },
            { key: 'type', header: 'Type', render: r => <Badge tone="blue">{r.documentType}</Badge> },
            { key: 'desc', header: 'Description', render: r => r.description ?? '—' },
            { key: 'visible', header: 'Client visible', render: r =>
              <Badge tone={r.isClientVisible ? 'green' : 'gray'}>{r.isClientVisible ? 'Yes' : 'No'}</Badge> },
            { key: 'size', header: 'Size', align: 'right', render: r => `${(r.sizeBytes / 1024).toFixed(1)} KB` },
            { key: 'when', header: 'Uploaded', render: r => fmtDateTime(r.createdAtUtc) },
          ]}
          empty={{ title: 'No documents uploaded', icon: <Folder size={20} /> }}
        />
      </CardBody>
    </Card>
  )
}

// ─── Tab: Forms (LT generated) ──────────────────────────────────────────────
function FormsTab({ caseId }: { caseId: string }) {
  const q = useQuery({ queryKey: ['case-generated', caseId], queryFn: () => casesApi.generated(caseId) })
  return (
    <Card>
      <CardHeader title="Generated forms & packets" subtitle="From the NJ LT form wizard" action={
        <Link to={`/cases/${caseId}/forms`} className="btn-secondary"><FilePlus2 size={14}/> Open wizard</Link>
      } />
      <CardBody className="p-0">
        <DataTable<GeneratedDocumentDto>
          rows={q.data ?? []}
          rowKey={r => r.id}
          loading={q.isLoading}
          columns={[
            { key: 'name', header: 'File', render: r => <span className="font-medium">{r.fileName}</span> },
            { key: 'type', header: 'Type',
              render: r => r.isMergedPacket ? <Badge tone="violet"><Layers size={11} className="inline mr-1" />Packet</Badge> : <Badge tone="blue">{r.formType}</Badge> },
            { key: 'ver',  header: 'Version', render: r => `v${r.version}${r.isCurrent ? ' (current)' : ''}` },
            { key: 'size', header: 'Size', align: 'right', render: r => `${(r.sizeBytes / 1024).toFixed(1)} KB` },
            { key: 'when', header: 'Generated', render: r => fmtDateTime(r.generatedAtUtc) },
            { key: 'act',  header: '', align: 'right', render: r => (
              <a className="btn-secondary text-xs" target="_blank" rel="noreferrer"
                 href={`/api/generated-documents/${r.id}/download`}>
                <Download size={12} /> Download
              </a>) },
          ]}
          empty={{ title: 'Nothing generated yet', icon: <FileText size={20} /> }}
        />
      </CardBody>
    </Card>
  )
}

// ─── Tab: Payments ──────────────────────────────────────────────────────────
function PaymentsTab({ caseId, canAdd }: { caseId: string; canAdd: boolean }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['case-payments', caseId], queryFn: () => casesApi.payments(caseId) })
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState('Cash')
  const [reference, setReference] = useState('')

  const m = useMutation({
    mutationFn: () => casesApi.addPayment(caseId, {
      receivedOnUtc: new Date().toISOString(),
      amount: Number(amount),
      method, reference: reference || null, notes: null,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['case-payments', caseId] })
      qc.invalidateQueries({ queryKey: ['case-activities', caseId] })
      setAmount(''); setReference('')
      toast.success('Payment recorded')
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const total = (q.data ?? []).reduce((s, p) => s + p.amount, 0)

  return (
    <div className="space-y-5">
      <Card>
        <CardHeader title="Payments" subtitle={`Total received: ${fmtMoney(total)}`} />
        <CardBody className="p-0">
          <DataTable<CasePaymentDto>
            rows={q.data ?? []}
            rowKey={r => r.id}
            loading={q.isLoading}
            columns={[
              { key: 'date', header: 'Received', render: r => fmtDateTime(r.receivedOnUtc) },
              { key: 'amt',  header: 'Amount', align: 'right',
                render: r => <span className="font-medium text-emerald-700">{fmtMoney(r.amount)}</span> },
              { key: 'method', header: 'Method', render: r => r.method ?? '—' },
              { key: 'ref',  header: 'Reference', render: r => r.reference ?? '—' },
              { key: 'notes',header: 'Notes', render: r => r.notes ?? '—' },
            ]}
            empty={{ title: 'No payments yet', icon: <Receipt size={20} /> }}
          />
        </CardBody>
      </Card>

      {canAdd && (
        <Card>
          <CardHeader title="Record a payment" />
          <CardBody>
            <form className="grid sm:grid-cols-4 gap-3" onSubmit={e => { e.preventDefault(); if (amount) m.mutate() }}>
              <div><label className="label">Amount</label>
                <input className="input" type="number" step="0.01" placeholder="0.00"
                       value={amount} onChange={e => setAmount(e.target.value)} /></div>
              <div><label className="label">Method</label>
                <select className="input" value={method} onChange={e => setMethod(e.target.value)}>
                  <option>Cash</option><option>Check</option><option>ACH</option><option>Card</option>
                </select></div>
              <div><label className="label">Reference</label>
                <input className="input" placeholder="Check #, txn id…"
                       value={reference} onChange={e => setReference(e.target.value)} /></div>
              <div className="flex items-end">
                <button className="btn-primary w-full" type="submit" disabled={!amount || m.isPending}>
                  <Receipt size={14} /> Record
                </button>
              </div>
            </form>
          </CardBody>
        </Card>
      )}
    </div>
  )
}

// ─── Tab: Comments ──────────────────────────────────────────────────────────
function CommentsTab({ caseId }: { caseId: string }) {
  const qc = useQueryClient()
  const { isFirmStaff } = useAuth()
  const q = useQuery({ queryKey: ['case-comments', caseId], queryFn: () => casesApi.comments(caseId) })
  const [body, setBody] = useState('')
  const [internal, setInternal] = useState(false)

  const m = useMutation({
    mutationFn: () => casesApi.addComment(caseId, { body, isInternal: internal }),
    onSuccess: () => {
      setBody('')
      qc.invalidateQueries({ queryKey: ['case-comments', caseId] })
      qc.invalidateQueries({ queryKey: ['case-activities', caseId] })
      toast.success('Comment posted')
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  return (
    <Card>
      <CardHeader title="Comments" subtitle={`${q.data?.length ?? 0} comments`} />
      <CardBody>
        <ul className="space-y-4">
          {(q.data ?? []).length === 0 && (
            <li><EmptyState title="No comments yet" icon={<MessageSquare size={20} />} /></li>
          )}
          {(q.data ?? []).map((c: CaseCommentDto) => (
            <li key={c.id} className="text-sm">
              <div className="flex items-center gap-2">
                <div className="font-medium text-slate-800">{c.authorName}</div>
                {c.isInternal && <Badge tone="amber">Internal</Badge>}
                <span className="text-xs text-slate-400">· {fmtDateTime(c.createdAtUtc)}</span>
              </div>
              <p className="text-slate-700 mt-0.5 whitespace-pre-wrap">{c.body}</p>
            </li>
          ))}
        </ul>
        <form className="mt-5 space-y-2 border-t border-slate-100 pt-4"
              onSubmit={e => { e.preventDefault(); if (body.trim()) m.mutate() }}>
          <textarea className="input min-h-[80px]" placeholder="Add a comment…"
                    value={body} onChange={e => setBody(e.target.value)} />
          <div className="flex items-center justify-between">
            {isFirmStaff && (
              <label className="text-xs text-slate-600 inline-flex items-center gap-1.5">
                <input type="checkbox" checked={internal} onChange={e => setInternal(e.target.checked)} /> Internal only
              </label>
            )}
            <button type="submit" className="btn-primary" disabled={!body.trim() || m.isPending}>
              <Send size={14} /> Post comment
            </button>
          </div>
        </form>
      </CardBody>
    </Card>
  )
}

// ─── Tab: Activity ──────────────────────────────────────────────────────────
function ActivityTab({ caseId }: { caseId: string }) {
  const q = useQuery({ queryKey: ['case-activities', caseId], queryFn: () => casesApi.activities(caseId) })
  return (
    <Card>
      <CardHeader title="Activity timeline" subtitle="Every status change, comment, payment, document, and close event" />
      <CardBody>
        {q.isLoading && <div className="py-6 flex justify-center"><Spinner /></div>}
        {(q.data ?? []).length === 0 && !q.isLoading && (
          <EmptyState title="No activity yet" icon={<Activity size={20} />} />
        )}
        <ol className="relative border-l-2 border-slate-100 pl-5 ml-1.5 space-y-4">
          {(q.data ?? []).map((a: CaseActivityDto, i: number) => (
            <li key={a.id} className="relative">
              <span className="absolute -left-[27px] top-1.5 size-3 rounded-full bg-brand-500 ring-4 ring-white" />
              <div className="flex items-center justify-between gap-3 flex-wrap">
                <div className="text-sm font-medium text-slate-800">{a.summary}</div>
                <span className="text-xs text-slate-400">{fmtDateTime(a.occurredAtUtc)}</span>
              </div>
              <div className="text-xs text-slate-500 mt-0.5">
                {a.activityType}{a.actorName ? ` · ${a.actorName}` : ''}
              </div>
              {a.details && <div className="text-xs text-slate-600 mt-1 whitespace-pre-wrap">{a.details}</div>}
            </li>
          ))}
        </ol>
      </CardBody>
    </Card>
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
