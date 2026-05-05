import { Link, useParams } from 'react-router-dom'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { portalApi } from '@/lib/portal'
import { unwrapError } from '@/lib/api'
import type {
  CaseActivityDto, CaseCommentDto, CaseDetail, CaseDocumentDto,
} from '@/types'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Tabs } from '@/components/ui/Tabs'
import { Badge, stageTone } from '@/components/ui/Badge'
import { Spinner } from '@/components/ui/Spinner'
import { EmptyState } from '@/components/ui/EmptyState'
import {
  ArrowLeft, Activity, MessageSquare, Folder, Send, Upload, Calendar,
  Briefcase, FileText, Info,
} from 'lucide-react'
import { fmtDate, fmtDateTime, fmtMoney } from '@/lib/format'
import { useAuth } from '@/lib/auth'

type TabId = 'overview' | 'timeline' | 'documents' | 'comments'
const TABS: { id: TabId; label: string; icon: typeof Activity }[] = [
  { id: 'overview',  label: 'Overview',  icon: Briefcase },
  { id: 'timeline',  label: 'Timeline',  icon: Activity },
  { id: 'documents', label: 'Documents', icon: Folder },
  { id: 'comments',  label: 'Comments',  icon: MessageSquare },
]

export function PortalCaseDetail() {
  const { id } = useParams<{ id: string }>()
  const [tab, setTab] = useState<TabId>('overview')
  const { hasAnyRole } = useAuth()
  const canWrite = hasAnyRole(['ClientAdmin'])

  const q = useQuery({
    queryKey: ['portal-case', id],
    queryFn: () => portalApi.getCase(id!),
    enabled: !!id,
  })

  if (q.isLoading || !q.data) return <div className="py-16 flex justify-center"><Spinner /></div>
  const c: CaseDetail = q.data

  return (
    <div className="space-y-5">
      <div>
        <Link to="/portal/cases" className="text-xs text-slate-500 hover:text-emerald-700 inline-flex items-center gap-1">
          <ArrowLeft size={12} /> All cases
        </Link>
        <h1 className="text-xl font-semibold mt-1">
          <span className="text-slate-500 font-mono mr-2">{c.caseNumber}</span>
          {c.title}
        </h1>
        <div className="text-sm text-slate-500 flex flex-wrap items-center gap-2 mt-1">
          <Badge tone={stageTone(c.stageCode)}>{c.stageName}</Badge>
          <Badge tone={c.statusCode === 'Open' ? 'green' : 'gray'}>{c.statusName}</Badge>
          {c.assignedAttorney && <span>· Attorney {c.assignedAttorney}</span>}
          {c.amountInControversy != null && <span>· {fmtMoney(c.amountInControversy)}</span>}
        </div>
      </div>

      <Tabs<TabId>
        tabs={TABS.map(t => ({
          id: t.id,
          label: <span className="inline-flex items-center gap-1.5"><t.icon size={13}/> {t.label}</span>,
        }))}
        active={tab}
        onChange={setTab}
      />

      {tab === 'overview' && <OverviewTab c={c} />}
      {tab === 'timeline' && <TimelineTab caseId={c.id} />}
      {tab === 'documents' && <DocumentsTab caseId={c.id} canUpload={canWrite} />}
      {tab === 'comments' && <CommentsTab caseId={c.id} canPost={canWrite} />}
    </div>
  )
}

// ─── Overview ───────────────────────────────────────────────────────────────
function OverviewTab({ c }: { c: CaseDetail }) {
  return (
    <div className="grid lg:grid-cols-3 gap-5">
      <Card className="lg:col-span-2">
        <CardHeader title="Case info" />
        <CardBody>
          <dl className="grid sm:grid-cols-2 gap-3 text-sm">
            <Field label="Type">{c.caseType.replace(/([A-Z])/g, ' $1').trim()}</Field>
            <Field label="Court venue">{c.courtVenue ?? '—'}</Field>
            <Field label="Filed on">{fmtDate(c.filedOnUtc)}</Field>
            <Field label="Court date">
              {c.courtDateUtc
                ? <span className="inline-flex items-center gap-1 text-emerald-700"><Calendar size={12}/> {fmtDateTime(c.courtDateUtc)}</span>
                : '—'}
            </Field>
            <Field label="Docket #">{c.courtDocketNumber ?? '—'}</Field>
            <Field label="Outcome">{c.outcome ?? <span className="text-slate-400">—</span>}</Field>
            <Field label="Assigned attorney">{c.assignedAttorney ?? '—'}</Field>
            <Field label="Assigned paralegal">{c.assignedParalegal ?? '—'}</Field>
            <Field label="Amount in dispute">{fmtMoney(c.amountInControversy)}</Field>
            <Field label="Created">{fmtDateTime(c.createdAtUtc)}</Field>
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
        <CardHeader title="LT case summary" />
        <CardBody>
          {c.ltCase ? (
            <dl className="space-y-2 text-sm">
              <Field label="Premises">
                {[c.ltCase.premisesAddressLine1, c.ltCase.premisesCity, c.ltCase.premisesState].filter(Boolean).join(', ') || '—'}
              </Field>
              <Field label="County">{c.ltCase.premisesCounty ?? '—'}</Field>
              <Field label="Total due">{fmtMoney(c.ltCase.totalDue)}</Field>
              <Field label="Rent due as of">{fmtDate(c.ltCase.rentDueAsOf)}</Field>
              <Field label="Attorney reviewed">
                <Badge tone={c.ltCase.attorneyReviewed ? 'green' : 'amber'}>
                  {c.ltCase.attorneyReviewed ? 'Reviewed' : 'Pending'}
                </Badge>
              </Field>
            </dl>
          ) : <p className="text-sm text-slate-500">No LT data available yet.</p>}
        </CardBody>
      </Card>
    </div>
  )
}

// ─── Timeline ───────────────────────────────────────────────────────────────
function TimelineTab({ caseId }: { caseId: string }) {
  const q = useQuery({
    queryKey: ['portal-case-timeline', caseId],
    queryFn: () => portalApi.timeline(caseId),
  })
  return (
    <Card>
      <CardHeader title="Status timeline" subtitle="Important events in this case" />
      <CardBody>
        {q.isLoading && <div className="py-6 flex justify-center"><Spinner /></div>}
        {!q.isLoading && (q.data ?? []).length === 0 && (
          <EmptyState title="No activity yet" icon={<Activity size={20} />} />
        )}
        <ol className="relative border-l-2 border-emerald-100 pl-5 ml-1.5 space-y-4">
          {(q.data ?? []).map((a: CaseActivityDto) => (
            <li key={a.id} className="relative">
              <span className="absolute -left-[27px] top-1.5 size-3 rounded-full bg-emerald-500 ring-4 ring-white" />
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

// ─── Documents ──────────────────────────────────────────────────────────────
function DocumentsTab({ caseId, canUpload }: { caseId: string; canUpload: boolean }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['portal-case-docs', caseId], queryFn: () => portalApi.documents(caseId) })
  const [description, setDescription] = useState('')
  const [uploading, setUploading] = useState(false)

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0]; if (!f) return
    setUploading(true)
    try {
      await portalApi.uploadDocument(caseId, f, description || undefined)
      toast.success(`Uploaded ${f.name}`)
      setDescription('')
      qc.invalidateQueries({ queryKey: ['portal-case-docs', caseId] })
      qc.invalidateQueries({ queryKey: ['portal-case-timeline', caseId] })
    } catch (err) { toast.error(unwrapError(err)) }
    finally { setUploading(false); e.target.value = '' }
  }

  return (
    <div className="space-y-5">
      <Card>
        <CardHeader title="Shared documents" subtitle="Documents your firm has shared with you" />
        <CardBody>
          {q.isLoading && <div className="py-6 flex justify-center"><Spinner /></div>}
          {!q.isLoading && (q.data ?? []).length === 0 && (
            <EmptyState title="No documents yet" description="Files your firm shares will appear here." icon={<Folder size={20} />} />
          )}
          <ul className="divide-y divide-slate-100">
            {(q.data ?? []).map((d: CaseDocumentDto) => (
              <li key={d.id} className="py-2.5 flex items-center justify-between gap-3 text-sm">
                <div className="min-w-0">
                  <div className="font-medium truncate">{d.fileName}</div>
                  <div className="text-xs text-slate-500">
                    <Badge tone="blue">{d.documentType}</Badge>
                    <span className="ml-2">{(d.sizeBytes / 1024).toFixed(1)} KB · {fmtDateTime(d.createdAtUtc)}</span>
                  </div>
                  {d.description && <div className="text-xs text-slate-600 mt-1">{d.description}</div>}
                </div>
              </li>
            ))}
          </ul>
        </CardBody>
      </Card>

      {canUpload && (
        <Card>
          <CardHeader title="Upload a supporting document" subtitle="Files you upload are tagged as client-uploaded and visible to the firm." />
          <CardBody>
            <div className="grid sm:grid-cols-[1fr_auto] gap-3 items-end">
              <div>
                <label className="label">Description (optional)</label>
                <input className="input" placeholder="e.g. Updated rent ledger from accounting"
                       value={description} onChange={e => setDescription(e.target.value)} />
              </div>
              <label className="btn-primary cursor-pointer h-fit">
                <Upload size={14} /> {uploading ? 'Uploading…' : 'Choose file'}
                <input type="file" className="hidden" onChange={onFile} disabled={uploading} />
              </label>
            </div>
          </CardBody>
        </Card>
      )}

      {!canUpload && (
        <div className="card p-3 text-sm text-slate-600 inline-flex items-center gap-2 bg-slate-50">
          <Info size={14} /> Document uploads are available to <strong>Client Admin</strong> users only.
        </div>
      )}
    </div>
  )
}

// ─── Comments ───────────────────────────────────────────────────────────────
function CommentsTab({ caseId, canPost }: { caseId: string; canPost: boolean }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['portal-case-comments', caseId], queryFn: () => portalApi.comments(caseId) })
  const [body, setBody] = useState('')

  const m = useMutation({
    mutationFn: () => portalApi.addComment(caseId, body),
    onSuccess: () => {
      setBody('')
      qc.invalidateQueries({ queryKey: ['portal-case-comments', caseId] })
      qc.invalidateQueries({ queryKey: ['portal-case-timeline', caseId] })
      toast.success('Comment posted')
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  return (
    <Card>
      <CardHeader title="Comments" subtitle={`${q.data?.length ?? 0} message(s) · only client-visible`} />
      <CardBody>
        <ul className="space-y-4">
          {(q.data ?? []).length === 0 && (
            <li><EmptyState title="No comments yet" icon={<MessageSquare size={20} />} /></li>
          )}
          {(q.data ?? []).map((c: CaseCommentDto) => (
            <li key={c.id} className="text-sm">
              <div className="flex items-center gap-2">
                <div className="font-medium text-slate-800">{c.authorName}</div>
                <span className="text-xs text-slate-400">· {fmtDateTime(c.createdAtUtc)}</span>
              </div>
              <p className="text-slate-700 mt-0.5 whitespace-pre-wrap">{c.body}</p>
            </li>
          ))}
        </ul>

        {canPost ? (
          <form className="mt-5 space-y-2 border-t border-slate-100 pt-4"
                onSubmit={e => { e.preventDefault(); if (body.trim()) m.mutate() }}>
            <textarea className="input min-h-[80px]" placeholder="Ask a question or share an update with your firm…"
                      value={body} onChange={e => setBody(e.target.value)} />
            <div className="flex justify-end">
              <button className="btn-primary" type="submit" disabled={!body.trim() || m.isPending}>
                <Send size={14} /> Post comment
              </button>
            </div>
          </form>
        ) : (
          <div className="mt-5 card p-3 text-sm text-slate-600 inline-flex items-center gap-2 bg-slate-50">
            <Info size={14} /> Posting comments is available to <strong>Client Admin</strong> users only.
          </div>
        )}
      </CardBody>
    </Card>
  )
}

// ─── helpers ────────────────────────────────────────────────────────────────
function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-500 font-medium">{label}</dt>
      <dd className="mt-0.5 text-slate-800">{children}</dd>
    </div>
  )
}
