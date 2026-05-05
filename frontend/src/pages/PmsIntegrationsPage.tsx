import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import type { ClientDto, PagedResult } from '@/types'
import type {
  CreatePmsIntegrationRequest, PmsConnectionTestResult,
  PmsIntegrationDto, PmsSyncRequest, SyncLogDto, SyncStatus,
  UpdatePmsIntegrationRequest,
} from '@/types/pms'
import { pmsApi } from '@/lib/pms'
import { api } from '@/lib/api'
import { Table } from '@/components/ui/Table'
import { Modal } from '@/components/ui/Modal'
import { Badge } from '@/components/ui/Badge'
import { Spinner } from '@/components/ui/Spinner'
import { Pencil, Plus, RefreshCcw, ZapIcon, ScrollText, CheckCircle2, AlertTriangle } from 'lucide-react'
import toast from 'react-hot-toast'
import { fmtDateTime } from '@/lib/format'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAuth } from '@/lib/auth'

// ─── form schema (shared by Create + Edit) ──────────────────────────────────
const formSchema = z.object({
  clientId: z.string(),
  provider: z.enum(['RentManager', 'Yardi', 'AppFolio', 'Buildium', 'PropertyFlow']),
  displayName: z.string().min(2, 'Display name required'),
  baseUrl: z.string().optional().nullable(),
  username: z.string().optional().nullable(),
  password: z.string().optional().nullable(),
  companyCode: z.string().optional().nullable(),
  locationId: z.string().optional().nullable(),
  syncIntervalMinutes: z.preprocess(v => Number(v), z.number().min(15).max(10080)),
  isActive: z.boolean().optional(),
})
type FormValues = z.infer<typeof formSchema>

export function PmsIntegrationsPage() {
  const qc = useQueryClient()
  const { hasAnyRole } = useAuth()
  const canManage = hasAnyRole(['FirmAdmin', 'Lawyer'])

  const [editingId, setEditingId] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [logsFor, setLogsFor] = useState<PmsIntegrationDto | null>(null)
  const [syncFor, setSyncFor] = useState<PmsIntegrationDto | null>(null)

  const integrationsQ = useQuery({
    queryKey: ['pms-integrations'],
    queryFn: async () => (await pmsApi.list({ pageSize: 100 })).data,
  })

  const clientsQ = useQuery({
    queryKey: ['clients-mini-pms'],
    queryFn: async () =>
      (await api.get<PagedResult<ClientDto>>('/clients', { params: { pageSize: 100 } })).data,
  })

  const editing = editingId
    ? integrationsQ.data?.items.find(i => i.id === editingId) ?? null
    : null

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">PMS Integrations</h1>
          <p className="text-sm text-slate-500">
            Connect to Rent Manager (live mock) and Yardi / AppFolio / Buildium / PropertyFlow (Phase&nbsp;5 stubs).
          </p>
        </div>
        {canManage && (
          <button className="btn-primary" onClick={() => setCreateOpen(true)}>
            <Plus size={14} /> New integration
          </button>
        )}
      </div>

      <Table<PmsIntegrationDto>
        loading={integrationsQ.isLoading}
        rows={integrationsQ.data?.items ?? []}
        rowKey={r => r.id}
        empty="No PMS integrations yet."
        columns={[
          { key: 'name', header: 'Name', render: r => <span className="font-medium">{r.displayName}</span> },
          { key: 'client', header: 'Client', render: r => r.clientName },
          { key: 'provider', header: 'Provider', render: r => <Badge tone="blue">{r.provider}</Badge> },
          { key: 'active', header: 'Active', render: r => (
              <Badge tone={r.isActive ? 'green' : 'gray'}>{r.isActive ? 'Active' : 'Disabled'}</Badge>) },
          { key: 'last', header: 'Last sync', render: r => syncBadge(r.lastSyncStatus, r.lastSyncAtUtc) },
          { key: 'every', header: 'Every', render: r => `${r.syncIntervalMinutes}m` },
          {
            key: 'act', header: '', className: 'text-right',
            render: r => (
              <div className="flex justify-end gap-1 flex-wrap">
                <TestButton id={r.id} disabled={!canManage} />
                <button className="btn-secondary" disabled={!canManage} onClick={() => setSyncFor(r)}>
                  <RefreshCcw size={14} /> Sync
                </button>
                {canManage && (
                  <button className="btn-ghost" onClick={() => setEditingId(r.id)}>
                    <Pencil size={14} /> Edit
                  </button>
                )}
                <button className="btn-ghost" onClick={() => setLogsFor(r)}>
                  <ScrollText size={14} /> Logs
                </button>
              </div>
            ),
          },
        ]}
      />

      {/* Create modal */}
      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="New PMS integration" size="lg">
        <IntegrationForm
          mode="create"
          clientOptions={clientsQ.data?.items ?? []}
          onClose={() => setCreateOpen(false)}
          onSubmitSuccess={msg => {
            qc.invalidateQueries({ queryKey: ['pms-integrations'] })
            toast.success(msg)
            setCreateOpen(false)
          }}
        />
      </Modal>

      {/* Edit modal */}
      <Modal open={!!editing} onClose={() => setEditingId(null)} title={`Edit — ${editing?.displayName ?? ''}`} size="lg">
        {editing && (
          <IntegrationForm
            mode="edit"
            existing={editing}
            clientOptions={clientsQ.data?.items ?? []}
            onClose={() => setEditingId(null)}
            onSubmitSuccess={msg => {
              qc.invalidateQueries({ queryKey: ['pms-integrations'] })
              toast.success(msg)
              setEditingId(null)
            }}
          />
        )}
      </Modal>

      {/* Sync modal */}
      <Modal open={!!syncFor} onClose={() => setSyncFor(null)} title={`Sync — ${syncFor?.displayName ?? ''}`}>
        {syncFor && (
          <SyncForm
            integration={syncFor}
            onClose={() => setSyncFor(null)}
            onAfter={() => {
              qc.invalidateQueries({ queryKey: ['pms-integrations'] })
              setSyncFor(null)
            }}
          />
        )}
      </Modal>

      {/* Logs modal */}
      <Modal open={!!logsFor} onClose={() => setLogsFor(null)} title={`Sync logs — ${logsFor?.displayName ?? ''}`} size="lg">
        {logsFor && <SyncLogsList integrationId={logsFor.id} />}
      </Modal>
    </div>
  )
}

// ─── Integration form (shared by Create + Edit) ─────────────────────────────
function IntegrationForm({
  mode, existing, clientOptions, onClose, onSubmitSuccess,
}: {
  mode: 'create' | 'edit'
  existing?: PmsIntegrationDto
  clientOptions: ClientDto[]
  onClose: () => void
  onSubmitSuccess: (msg: string) => void
}) {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      clientId: existing?.clientId ?? '',
      provider: existing?.provider ?? 'RentManager',
      displayName: existing?.displayName ?? '',
      baseUrl: existing?.baseUrl ?? '',
      username: '',
      password: '',
      companyCode: existing?.companyCode ?? '',
      locationId: existing?.locationId ?? '',
      syncIntervalMinutes: existing?.syncIntervalMinutes ?? 1440,
      isActive: existing?.isActive ?? true,
    },
  })
  useEffect(() => {
    if (existing) {
      reset({
        clientId: existing.clientId,
        provider: existing.provider,
        displayName: existing.displayName,
        baseUrl: existing.baseUrl ?? '',
        username: '',
        password: '',
        companyCode: existing.companyCode ?? '',
        locationId: existing.locationId ?? '',
        syncIntervalMinutes: existing.syncIntervalMinutes,
        isActive: existing.isActive,
      })
    }
  }, [existing, reset])

  const submit = handleSubmit(async (data) => {
    try {
      if (mode === 'create') {
        const req: CreatePmsIntegrationRequest = {
          clientId: data.clientId,
          provider: data.provider,
          displayName: data.displayName,
          baseUrl: data.baseUrl || null,
          username: data.username || null,
          password: data.password || null,
          companyCode: data.companyCode || null,
          locationId: data.locationId || null,
          syncIntervalMinutes: data.syncIntervalMinutes,
        }
        const r = await pmsApi.create(req)
        onSubmitSuccess(r.message ?? 'Integration created.')
      } else if (existing) {
        const req: UpdatePmsIntegrationRequest = {
          displayName: data.displayName,
          baseUrl: data.baseUrl || null,
          username: data.username || null,
          password: data.password || null,
          companyCode: data.companyCode || null,
          locationId: data.locationId || null,
          syncIntervalMinutes: data.syncIntervalMinutes,
          isActive: data.isActive ?? true,
        }
        const r = await pmsApi.update(existing.id, req)
        onSubmitSuccess(r.message ?? 'Integration updated.')
      }
    } catch (e: any) {
      toast.error(e.message ?? 'Failed.')
    }
  })

  return (
    <form className="space-y-3" onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="label">Client</label>
          <select className="input" disabled={mode === 'edit'} {...register('clientId')}>
            <option value="">— Select —</option>
            {clientOptions.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
        <div>
          <label className="label">Provider</label>
          <select className="input" disabled={mode === 'edit'} {...register('provider')}>
            <option value="RentManager">Rent Manager</option>
            <option value="Yardi" disabled>Yardi (Phase 5)</option>
            <option value="AppFolio" disabled>AppFolio (Phase 5)</option>
            <option value="Buildium" disabled>Buildium (Phase 5)</option>
            <option value="PropertyFlow" disabled>PropertyFlow (Phase 5)</option>
          </select>
        </div>

        <div className="col-span-2">
          <label className="label">Display name</label>
          <input className="input" {...register('displayName')} />
          {errors.displayName && <p className="text-xs text-rose-600 mt-1">{errors.displayName.message}</p>}
        </div>

        <div className="col-span-2">
          <label className="label">Base URL</label>
          <input className="input" placeholder="https://yourcompany.api.rentmanager.com" {...register('baseUrl')} />
          <p className="text-xs text-slate-500 mt-1">
            For Rent Manager 12, use <code>https://&lt;customer&gt;.api.rentmanager.com</code> — not the
            <code> .rmx.</code> web-client URL. We'll auto-rewrite <code>.rmx.</code> → <code>.api.</code> if you paste it.
          </p>
        </div>
        <div><label className="label">Username</label><input className="input" {...register('username')} /></div>
        <div>
          <label className="label">Password {mode === 'edit' && <span className="text-slate-400">(leave blank to keep)</span>}</label>
          <input type="password" className="input" {...register('password')} />
        </div>
        <div><label className="label">Company code</label><input className="input" {...register('companyCode')} /></div>
        <div>
          <label className="label">Location ID</label>
          <input className="input" placeholder="1" {...register('locationId')} />
          <p className="text-xs text-slate-500 mt-1">Numeric Rent Manager LocationID (defaults to 1).</p>
        </div>
        <div>
          <label className="label">Sync interval (minutes)</label>
          <input type="number" className="input" {...register('syncIntervalMinutes' as any)} />
        </div>
        {mode === 'edit' && (
          <div className="flex items-center gap-2">
            <label className="text-sm text-slate-600 inline-flex items-center gap-2">
              <input type="checkbox" {...register('isActive')} /> Active
            </label>
          </div>
        )}
      </div>

      <div className="flex justify-end gap-2 pt-2">
        <button type="button" className="btn-secondary" onClick={onClose}>Cancel</button>
        <button type="submit" className="btn-primary" disabled={isSubmitting}>
          {isSubmitting ? <Spinner size={14} className="text-white" /> : mode === 'create' ? 'Create' : 'Save'}
        </button>
      </div>
    </form>
  )
}

// ─── Sync modal: scope picker + foreground/background toggle ────────────────
function SyncForm({
  integration, onClose, onAfter,
}: {
  integration: PmsIntegrationDto
  onClose: () => void
  onAfter: () => void
}) {
  const [fullSync, setFullSync] = useState(true)
  const [scope, setScope] = useState({ properties: true, units: true, tenants: true, leases: true, ledger: true })
  const [runInBackground, setRunInBackground] = useState(true)
  const [submitting, setSubmitting] = useState(false)

  const submit = async () => {
    setSubmitting(true)
    try {
      const req: PmsSyncRequest = fullSync
        ? { fullSync: true, runInBackground }
        : {
            fullSync: false,
            syncProperties: scope.properties,
            syncUnits: scope.units,
            syncTenants: scope.tenants,
            syncLeases: scope.leases,
            syncLedgerItems: scope.ledger,
            runInBackground,
          }
      const r = await pmsApi.sync(integration.id, req)
      toast.success(r.message ?? 'Sync started.')

      if (!runInBackground && r.data) {
        const { propertiesSynced: p, unitsSynced: u, tenantsSynced: t, leasesSynced: l, ledgerItemsSynced: g } = r.data
        toast.success(`Synced ${p}P · ${u}U · ${t}T · ${l}L · ${g} ledger`)
      }
      onAfter()
    } catch (e: any) {
      toast.error(e.message ?? 'Sync failed.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="space-y-4">
      <div className="card p-3 bg-slate-50/50">
        <label className="text-sm font-medium text-slate-700 inline-flex items-center gap-2">
          <input type="checkbox" checked={fullSync} onChange={e => setFullSync(e.target.checked)} />
          Full sync (all entities)
        </label>
        <div className="mt-3 grid grid-cols-2 gap-2 text-sm" style={{ opacity: fullSync ? 0.4 : 1 }}>
          {(['properties', 'units', 'tenants', 'leases', 'ledger'] as const).map(k => (
            <label key={k} className="inline-flex items-center gap-2 capitalize">
              <input type="checkbox" disabled={fullSync}
                checked={scope[k]}
                onChange={e => setScope(s => ({ ...s, [k]: e.target.checked }))} />
              Sync {k}
            </label>
          ))}
        </div>
      </div>
      <label className="text-sm text-slate-600 inline-flex items-center gap-2">
        <input type="checkbox" checked={runInBackground} onChange={e => setRunInBackground(e.target.checked)} />
        Run in background (Hangfire) — uncheck to wait for the result
      </label>
      <div className="flex justify-end gap-2 pt-2">
        <button type="button" className="btn-secondary" onClick={onClose}>Cancel</button>
        <button type="button" className="btn-primary" onClick={submit} disabled={submitting}>
          {submitting ? <Spinner size={14} className="text-white" /> : 'Run sync'}
        </button>
      </div>
    </div>
  )
}

// ─── Test button — fires the stored-credentials test and toasts the result ─
function TestButton({ id, disabled }: { id: string; disabled: boolean }) {
  const m = useMutation({
    mutationFn: async () => (await pmsApi.testStored(id)).data,
    onSuccess: (r: PmsConnectionTestResult) => {
      if (r.isConnected) {
        toast.success(`OK · ${r.message} · ${r.latencyMs}ms`, { icon: <CheckCircle2 size={16} className="text-emerald-600" /> })
      } else {
        toast.error(`Failed: ${r.message}`, { icon: <AlertTriangle size={16} className="text-rose-600" /> })
      }
    },
    onError: (e: any) => toast.error(e.message ?? 'Test failed.'),
  })
  return (
    <button className="btn-ghost" disabled={disabled || m.isPending} onClick={() => m.mutate()}>
      <ZapIcon size={14} /> Test
    </button>
  )
}

// ─── Logs list ──────────────────────────────────────────────────────────────
function SyncLogsList({ integrationId }: { integrationId: string }) {
  const q = useQuery({
    queryKey: ['sync-logs', integrationId],
    queryFn: async () => (await pmsApi.syncLogs(integrationId, 50)).data,
  })
  return (
    <Table<SyncLogDto>
      loading={q.isLoading}
      rows={q.data ?? []}
      rowKey={r => r.id}
      empty="No sync runs yet."
      columns={[
        { key: 'started', header: 'Started', render: r => fmtDateTime(r.startedAtUtc) },
        { key: 'status', header: 'Status', render: r =>
          <Badge tone={r.status === 'Succeeded' ? 'green' : r.status === 'Failed' ? 'rose' : 'amber'}>{r.status}</Badge> },
        { key: 'p', header: 'P', render: r => r.propertiesSynced, className: 'text-right' },
        { key: 'u', header: 'U', render: r => r.unitsSynced, className: 'text-right' },
        { key: 't', header: 'T', render: r => r.tenantsSynced, className: 'text-right' },
        { key: 'l', header: 'L', render: r => r.leasesSynced, className: 'text-right' },
        { key: 'led', header: 'Ledger', render: r => r.ledgerItemsSynced, className: 'text-right' },
        { key: 'msg', header: 'Message', render: r => r.message ?? r.errorDetail ?? '—' },
      ]}
    />
  )
}

// ─── helpers ────────────────────────────────────────────────────────────────
function syncBadge(status: SyncStatus | null, at: string | null) {
  if (!status) return <span className="text-xs text-slate-400">never</span>
  const tones: Record<SyncStatus, any> = {
    Started: 'amber', Succeeded: 'green', Failed: 'rose', PartiallySucceeded: 'amber',
  }
  return (
    <div>
      <Badge tone={tones[status]}>{status}</Badge>
      <div className="text-xs text-slate-500 mt-0.5">{fmtDateTime(at)}</div>
    </div>
  )
}
