import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { api, getStoredToken } from '@/lib/api'
import type { AuditLogDetailDto, AuditLogDto, PagedResult } from '@/types'
import { DataTable, type Column } from '@/components/ui/DataTable'
import { FilterBar, FilterSelect } from '@/components/ui/FilterBar'
import { Drawer } from '@/components/ui/Drawer'
import { Badge } from '@/components/ui/Badge'
import { Spinner } from '@/components/ui/Spinner'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import {
  ShieldCheck, Download, AlertCircle,
} from 'lucide-react'
import { fmtDateTime } from '@/lib/format'
import toast from 'react-hot-toast'

// Keep this list aligned with PropertyManagement.Domain.Enums.AuditAction.
const ACTIONS = [
  'Login', 'Logout', 'LoginFailed',
  'PmsSync', 'PmsSyncStarted', 'PmsSyncCompleted', 'PmsSyncFailed',
  'CreateCase', 'UpdateCase', 'ChangeStatus', 'CloseCase',
  'GeneratePdf', 'DownloadPdf', 'UploadDocument', 'DeleteDocument',
  'CreateUser', 'UpdateUser', 'UserRoleChanged',
  'PmsIntegrationCreated', 'PmsIntegrationUpdated', 'PmsIntegrationDeleted',
  'PaymentRecorded', 'CommentAdded',
  'ClientPortalAccess',
] as const

type Action = typeof ACTIONS[number]

const ACTION_TONE: Record<string, 'gray' | 'green' | 'rose' | 'amber' | 'blue' | 'sky' | 'violet'> = {
  Login: 'green', LoginFailed: 'rose', Logout: 'gray',
  PmsSyncStarted: 'sky', PmsSyncCompleted: 'green', PmsSyncFailed: 'rose', PmsSync: 'sky',
  CreateCase: 'blue', UpdateCase: 'amber', ChangeStatus: 'amber', CloseCase: 'violet',
  GeneratePdf: 'sky', DownloadPdf: 'gray', UploadDocument: 'blue', DeleteDocument: 'rose',
  CreateUser: 'blue', UpdateUser: 'amber', UserRoleChanged: 'amber',
  PmsIntegrationCreated: 'blue', PmsIntegrationUpdated: 'amber', PmsIntegrationDeleted: 'rose',
  PaymentRecorded: 'green', CommentAdded: 'blue',
  ClientPortalAccess: 'sky',
}

export function AuditLogsPage() {
  const [search, setSearch] = useState('')
  const [action, setAction] = useState<Action | ''>('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [detailId, setDetailId] = useState<string | null>(null)

  const q = useQuery({
    queryKey: ['audit-logs', { search, action, from, to, page }],
    queryFn: async () =>
      (await api.get<PagedResult<AuditLogDto>>('/audit-logs', {
        params: {
          search: search || undefined,
          action: action || undefined,
          from: from || undefined,
          to: to || undefined,
          page, pageSize: 50,
        },
      })).data,
  })

  const detailQ = useQuery({
    queryKey: ['audit-log', detailId],
    queryFn: async () => (await api.get<AuditLogDetailDto>(`/audit-logs/${detailId}`)).data,
    enabled: !!detailId,
  })

  const hasActiveFilters = useMemo(() => !!(action || from || to), [action, from, to])

  async function exportCsv() {
    try {
      const params = new URLSearchParams()
      if (search) params.set('search', search)
      if (action) params.set('action', action)
      if (from) params.set('from', from)
      if (to) params.set('to', to)
      const token = getStoredToken()
      const r = await fetch(`/api/audit-logs/export?${params}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      if (!r.ok) { toast.error(`Export failed: HTTP ${r.status}`); return }
      const blob = await r.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `audit-log-${new Date().toISOString().slice(0, 19).replace(/:/g, '')}.csv`
      document.body.appendChild(a); a.click(); a.remove()
      URL.revokeObjectURL(url)
      toast.success('CSV downloaded')
    } catch (e: any) { toast.error(e.message ?? 'Export failed') }
  }

  const cols: Column<AuditLogDto>[] = [
    { key: 'when', header: 'When', sortKey: 'occurredAtUtc', render: r => fmtDateTime(r.occurredAtUtc) },
    {
      key: 'action', header: 'Action',
      render: r => <Badge tone={ACTION_TONE[r.action] ?? 'blue'}>{r.action}</Badge>,
    },
    {
      key: 'entity', header: 'Entity',
      render: r => `${r.entityType}${r.entityId ? ` · ${r.entityId.slice(0, 8)}…` : ''}`,
    },
    { key: 'summary', header: 'Summary', render: r => <span className="line-clamp-1">{r.summary ?? '—'}</span> },
    { key: 'user', header: 'User', render: r => r.userEmail ?? <span className="text-slate-400">—</span> },
    { key: 'ip', header: 'IP', render: r => r.ipAddress ?? '—' },
  ]

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-xl font-semibold tracking-tight inline-flex items-center gap-2">
            <ShieldCheck size={18} className="text-brand-600" /> Audit logs
          </h1>
          <p className="text-sm text-slate-500">
            Immutable record of every sensitive action — login attempts, case lifecycle, PMS sync,
            document uploads/downloads, payments, and comments.
          </p>
        </div>
        <button className="btn-secondary" onClick={exportCsv}>
          <Download size={14} /> Export CSV
        </button>
      </div>

      <FilterBar
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1) }}
        searchPlaceholder="Search summary, user, or entity…"
        hasActiveFilters={hasActiveFilters}
        onClear={() => { setAction(''); setFrom(''); setTo(''); setPage(1) }}
      >
        <FilterSelect
          label="Action" value={action}
          options={ACTIONS.map(a => ({ value: a, label: a }))}
          onChange={(v) => { setAction(v as Action | ''); setPage(1) }}
        />
        <div className="flex items-center gap-1.5">
          <input className="input text-xs py-1.5 w-36" type="date"
            value={from} onChange={e => { setFrom(e.target.value); setPage(1) }} />
          <span className="text-xs text-slate-400">to</span>
          <input className="input text-xs py-1.5 w-36" type="date"
            value={to} onChange={e => { setTo(e.target.value); setPage(1) }} />
        </div>
      </FilterBar>

      <DataTable<AuditLogDto>
        rows={q.data?.items ?? []}
        columns={cols}
        rowKey={r => r.id}
        loading={q.isLoading}
        page={page}
        pageSize={q.data?.pageSize ?? 50}
        totalCount={q.data?.totalCount ?? 0}
        onPageChange={setPage}
        onRowClick={r => setDetailId(r.id)}
        empty={{
          title: 'No audit log entries',
          description: 'No events match the current filter set.',
          icon: <AlertCircle size={20} />,
        }}
      />

      <Drawer
        open={!!detailId}
        onClose={() => setDetailId(null)}
        title={detailQ.data ? `${detailQ.data.action}` : 'Audit entry'}
        subtitle={detailQ.data?.summary ?? undefined}
        width="lg"
      >
        {detailQ.isLoading || !detailQ.data
          ? <div className="py-10 flex justify-center"><Spinner /></div>
          : <AuditDetailBody d={detailQ.data} />}
      </Drawer>
    </div>
  )
}

// ─── Detail panel ───────────────────────────────────────────────────────────
function AuditDetailBody({ d }: { d: AuditLogDetailDto }) {
  return (
    <div className="space-y-5">
      <Card>
        <CardHeader title="Event" />
        <CardBody>
          <dl className="grid sm:grid-cols-2 gap-3 text-sm">
            <Field label="Occurred">{fmtDateTime(d.occurredAtUtc)}</Field>
            <Field label="Action"><Badge tone={ACTION_TONE[d.action] ?? 'blue'}>{d.action}</Badge></Field>
            <Field label="Entity type">{d.entityType}</Field>
            <Field label="Entity id"><code className="text-xs">{d.entityId ?? '—'}</code></Field>
            <Field label="User email">{d.userEmail ?? '—'}</Field>
            <Field label="User id"><code className="text-xs">{d.userId ?? '—'}</code></Field>
            <Field label="Law firm id"><code className="text-xs">{d.lawFirmId ?? '—'}</code></Field>
            <Field label="IP address">{d.ipAddress ?? '—'}</Field>
            <Field label="User agent" col2>
              <span className="text-xs break-all">{d.userAgent ?? '—'}</span>
            </Field>
          </dl>
          {d.summary && (
            <div className="mt-4">
              <div className="text-xs uppercase tracking-wide text-slate-500 font-medium mb-1">Summary</div>
              <p className="text-sm text-slate-700 whitespace-pre-wrap">{d.summary}</p>
            </div>
          )}
        </CardBody>
      </Card>

      {(d.oldValueJson || d.newValueJson) && (
        <div className="grid lg:grid-cols-2 gap-4">
          <JsonCard title="Old value" json={d.oldValueJson} tone="rose" />
          <JsonCard title="New value" json={d.newValueJson} tone="green" />
        </div>
      )}

      {d.payloadJson && (
        <JsonCard title="Payload" json={d.payloadJson} tone="gray" />
      )}
    </div>
  )
}

function JsonCard({ title, json, tone }: { title: string; json: string | null; tone: 'gray' | 'rose' | 'green' }) {
  if (!json) return <Card><CardHeader title={title} /><CardBody><span className="text-xs text-slate-400">(none)</span></CardBody></Card>
  let pretty = json
  try { pretty = JSON.stringify(JSON.parse(json), null, 2) } catch { /* keep raw */ }
  const ringClass = { gray: 'ring-slate-200', rose: 'ring-rose-200', green: 'ring-emerald-200' }[tone]
  return (
    <Card>
      <CardHeader title={title} />
      <CardBody className="p-0">
        <pre className={`text-[11px] leading-relaxed font-mono whitespace-pre-wrap break-words p-3 rounded-b-xl bg-slate-50 ring-1 ring-inset ${ringClass} max-h-72 overflow-y-auto`}>
{pretty}
        </pre>
      </CardBody>
    </Card>
  )
}

function Field({ label, children, col2 }: { label: string; children: React.ReactNode; col2?: boolean }) {
  return (
    <div className={col2 ? 'sm:col-span-2' : ''}>
      <dt className="text-xs uppercase tracking-wide text-slate-500 font-medium">{label}</dt>
      <dd className="mt-0.5 text-slate-800">{children}</dd>
    </div>
  )
}
