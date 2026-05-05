import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, useEffect } from 'react'
import toast from 'react-hot-toast'
import { Modal } from '@/components/ui/Modal'
import { Spinner } from '@/components/ui/Spinner'
import { Badge } from '@/components/ui/Badge'
import { CheckCircle2, AlertTriangle, UserCog, Lock } from 'lucide-react'
import type {
  AssigneeDto, CaseDetail, CaseStatusCode, CaseStatusDto,
} from '@/types'
import { casesApi } from '@/lib/cases'
import { unwrapError } from '@/lib/api'

// ─── Status update modal ────────────────────────────────────────────────────
interface StatusModalProps {
  open: boolean
  onClose: () => void
  caseDetail: CaseDetail
  onUpdated?: (c: CaseDetail) => void
}

export function StatusModal({ open, onClose, caseDetail, onUpdated }: StatusModalProps) {
  const qc = useQueryClient()
  const [statusCode, setStatusCode] = useState<CaseStatusCode>(caseDetail.statusCode)
  const [note, setNote] = useState('')

  useEffect(() => { if (open) { setStatusCode(caseDetail.statusCode); setNote('') } }, [open, caseDetail.statusCode])

  const statusesQ = useQuery<CaseStatusDto[]>({
    queryKey: ['case-statuses'], queryFn: casesApi.statuses, enabled: open,
  })

  const m = useMutation({
    mutationFn: () => casesApi.changeStatus(caseDetail.id, { statusCode, note: note || null }),
    onSuccess: (c) => {
      toast.success(`Status updated to ${c.statusName}`)
      qc.invalidateQueries({ queryKey: ['case', caseDetail.id] })
      qc.invalidateQueries({ queryKey: ['cases'] })
      onUpdated?.(c); onClose()
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  return (
    <Modal open={open} onClose={onClose} title="Update case status" size="md">
      <div className="space-y-4">
        <div className="flex items-center justify-between text-sm">
          <span className="text-slate-500">Current status</span>
          <Badge tone={caseDetail.statusCode === 'Open' ? 'green' : 'gray'}>{caseDetail.statusName}</Badge>
        </div>
        <div>
          <label className="label">New status</label>
          <select className="input" value={statusCode} onChange={e => setStatusCode(e.target.value as CaseStatusCode)}>
            {(statusesQ.data ?? []).map(s => <option key={s.code} value={s.code}>{s.name}</option>)}
          </select>
        </div>
        <div>
          <label className="label">Note (optional)</label>
          <textarea className="input min-h-[70px]" value={note} onChange={e => setNote(e.target.value)} />
        </div>
        <div className="flex justify-end gap-2 pt-1">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn-primary" disabled={m.isPending || statusCode === caseDetail.statusCode}
                  onClick={() => m.mutate()}>
            {m.isPending ? <Spinner size={14} className="text-white" /> : 'Update status'}
          </button>
        </div>
      </div>
    </Modal>
  )
}

// ─── Assign lawyer modal ────────────────────────────────────────────────────
interface AssignModalProps {
  open: boolean
  onClose: () => void
  caseDetail: CaseDetail
}

export function AssignModal({ open, onClose, caseDetail }: AssignModalProps) {
  const qc = useQueryClient()
  const [attorneyId, setAttorneyId] = useState<string>('')
  const [paralegalId, setParalegalId] = useState<string>('')
  const [note, setNote] = useState('')

  useEffect(() => {
    if (open) {
      setAttorneyId(caseDetail.assignedAttorneyId ?? '')
      setParalegalId(caseDetail.assignedParalegalId ?? '')
      setNote('')
    }
  }, [open, caseDetail.assignedAttorneyId, caseDetail.assignedParalegalId])

  const assigneesQ = useQuery<AssigneeDto[]>({
    queryKey: ['case-assignees'], queryFn: casesApi.assignees, enabled: open,
  })

  const lawyers = (assigneesQ.data ?? []).filter(a => a.role === 'Lawyer' || a.role === 'FirmAdmin')
  const paralegals = (assigneesQ.data ?? []).filter(a => a.role === 'Paralegal')

  const m = useMutation({
    mutationFn: () => casesApi.assign(caseDetail.id, {
      attorneyId: attorneyId || null,
      paralegalId: paralegalId || null,
      note: note || null,
    }),
    onSuccess: (c) => {
      toast.success('Assignment updated')
      qc.invalidateQueries({ queryKey: ['case', caseDetail.id] })
      qc.invalidateQueries({ queryKey: ['cases'] })
      onClose()
      // Activity timeline updates too
      qc.invalidateQueries({ queryKey: ['case-activities', c.id] })
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  return (
    <Modal open={open} onClose={onClose} title="Assign case" size="md">
      <div className="space-y-4">
        <div className="text-sm text-slate-600 inline-flex items-center gap-2">
          <UserCog size={14} /> Assign attorney and paralegal. Leave a field blank to unassign.
        </div>
        <div>
          <label className="label">Attorney</label>
          <select className="input" value={attorneyId} onChange={e => setAttorneyId(e.target.value)}>
            <option value="">— Unassigned —</option>
            {lawyers.map(l => <option key={l.id} value={l.id}>{l.fullName} ({l.role})</option>)}
          </select>
        </div>
        <div>
          <label className="label">Paralegal</label>
          <select className="input" value={paralegalId} onChange={e => setParalegalId(e.target.value)}>
            <option value="">— Unassigned —</option>
            {paralegals.map(l => <option key={l.id} value={l.id}>{l.fullName}</option>)}
          </select>
        </div>
        <div>
          <label className="label">Note (optional)</label>
          <textarea className="input min-h-[60px]" value={note} onChange={e => setNote(e.target.value)} />
        </div>
        <div className="flex justify-end gap-2 pt-1">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn-primary" onClick={() => m.mutate()} disabled={m.isPending}>
            {m.isPending ? <Spinner size={14} className="text-white" /> : 'Save assignment'}
          </button>
        </div>
      </div>
    </Modal>
  )
}

// ─── Close case modal ───────────────────────────────────────────────────────
interface CloseModalProps {
  open: boolean
  onClose: () => void
  caseDetail: CaseDetail
}

const OUTCOME_OPTIONS = [
  'Settled with payment plan',
  'Settled — paid in full',
  'Tenant vacated voluntarily',
  'Judgment for landlord',
  'Warrant of removal executed',
  'Dismissed',
  'Withdrawn',
  'Other',
]

export function CloseModal({ open, onClose, caseDetail }: CloseModalProps) {
  const qc = useQueryClient()
  const [outcome, setOutcome] = useState<string>('')
  const [outcomeOther, setOutcomeOther] = useState('')
  const [notes, setNotes] = useState('')
  const [confirm, setConfirm] = useState(false)

  useEffect(() => { if (open) { setOutcome(''); setOutcomeOther(''); setNotes(''); setConfirm(false) } }, [open])

  const m = useMutation({
    mutationFn: () => casesApi.close(caseDetail.id, {
      outcome: outcome === 'Other' ? (outcomeOther.trim() || null) : (outcome || null),
      notes: notes || null,
    }),
    onSuccess: () => {
      toast.success(`Case ${caseDetail.caseNumber} closed`)
      qc.invalidateQueries({ queryKey: ['case', caseDetail.id] })
      qc.invalidateQueries({ queryKey: ['cases'] })
      qc.invalidateQueries({ queryKey: ['case-activities', caseDetail.id] })
      onClose()
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const isClosed = caseDetail.statusCode === 'Closed'

  return (
    <Modal open={open} onClose={onClose} title={`Close case · ${caseDetail.caseNumber}`} size="md">
      <div className="space-y-4">
        {isClosed ? (
          <div className="card p-3 bg-emerald-50 ring-emerald-200 text-sm text-emerald-800 inline-flex items-center gap-2">
            <CheckCircle2 size={14} /> This case is already closed.
          </div>
        ) : (
          <>
            <div className="card p-3 bg-amber-50 ring-amber-200 text-sm text-amber-800 inline-flex items-center gap-2">
              <AlertTriangle size={14} /> Closing the case sets the stage and status to Closed and stops further activity.
            </div>
            <div>
              <label className="label">Outcome</label>
              <select className="input" value={outcome} onChange={e => setOutcome(e.target.value)}>
                <option value="">— Select outcome —</option>
                {OUTCOME_OPTIONS.map(o => <option key={o}>{o}</option>)}
              </select>
            </div>
            {outcome === 'Other' && (
              <div>
                <label className="label">Custom outcome</label>
                <input className="input" value={outcomeOther} onChange={e => setOutcomeOther(e.target.value)} />
              </div>
            )}
            <div>
              <label className="label">Closing notes</label>
              <textarea className="input min-h-[80px]" value={notes} onChange={e => setNotes(e.target.value)} />
            </div>
            <label className="text-sm inline-flex items-center gap-2">
              <input type="checkbox" checked={confirm} onChange={e => setConfirm(e.target.checked)} />
              I confirm this case should be closed.
            </label>
          </>
        )}
        <div className="flex justify-end gap-2 pt-1">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          {!isClosed && (
            <button className="btn-danger" disabled={!confirm || m.isPending} onClick={() => m.mutate()}>
              {m.isPending ? <Spinner size={14} className="text-white" /> : (<><Lock size={14} /> Close case</>)}
            </button>
          )}
        </div>
      </div>
    </Modal>
  )
}
