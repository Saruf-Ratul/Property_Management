import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { ltApi } from '@/lib/ltforms'
import { unwrapError } from '@/lib/api'
import type {
  LtFormBundleDto, LtFormDataSections, LtFormSchemaDto, LtValidationSummary,
} from '@/types/ltforms'
import type { CaseDetail, GeneratedDocumentDto, LtFormType } from '@/types'
import { casesApi } from '@/lib/cases'
import { Card, CardBody, CardHeader } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Spinner'
import { Badge } from '@/components/ui/Badge'
import { EmptyState } from '@/components/ui/EmptyState'
import { Tabs } from '@/components/ui/Tabs'
import { Modal } from '@/components/ui/Modal'
import {
  ArrowLeft, CheckCircle2, Eye, FileCheck2, Layers, Save, ShieldAlert, Sparkles,
  Download, History, Lock, AlertTriangle, Info,
} from 'lucide-react'
import { fmtDateTime } from '@/lib/format'
import { useAuth } from '@/lib/auth'

export function LtFormWizardPage() {
  const { id: caseId } = useParams<{ id: string }>()
  const qc = useQueryClient()
  const { hasAnyRole } = useAuth()
  const canApprove = hasAnyRole(['FirmAdmin', 'Lawyer'])

  // Pull the parent Case + LT case
  const caseQ = useQuery<CaseDetail>({
    queryKey: ['case', caseId],
    queryFn: () => casesApi.get(caseId!),
    enabled: !!caseId,
  })

  // Bootstrap LT case (idempotent)
  const ltCaseQ = useQuery({
    queryKey: ['lt-case-from-case', caseId],
    queryFn: () => ltApi.createFromCase(caseId!),
    enabled: !!caseId,
  })

  const ltCaseId = ltCaseQ.data?.id

  const schemasQ = useQuery({ queryKey: ['lt-schemas'], queryFn: ltApi.schemas })
  const bundleQ = useQuery({
    queryKey: ['lt-bundle', ltCaseId],
    queryFn: () => ltApi.getBundle(ltCaseId!),
    enabled: !!ltCaseId,
  })
  const generatedQ = useQuery({
    queryKey: ['lt-generated', ltCaseId],
    queryFn: () => ltApi.generated(ltCaseId!),
    enabled: !!ltCaseId,
  })

  const [activeForm, setActiveForm] = useState<LtFormType>('VerifiedComplaint')
  const [draft, setDraft] = useState<LtFormDataSections | null>(null)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)

  // Initialize / reset draft when bundle loads
  useEffect(() => {
    if (bundleQ.data && !draft) setDraft(structuredClone(bundleQ.data.sections))
  }, [bundleQ.data])  // eslint-disable-line

  const validateQ = useQuery({
    queryKey: ['lt-validate', ltCaseId, activeForm, bundleQ.dataUpdatedAt],
    queryFn: () => ltApi.validate(ltCaseId!, activeForm),
    enabled: !!ltCaseId,
  })

  const saveM = useMutation({
    mutationFn: () => ltApi.saveBundle(ltCaseId!, draft!),
    onSuccess: () => {
      toast.success('Form data saved')
      qc.invalidateQueries({ queryKey: ['lt-bundle', ltCaseId] })
      qc.invalidateQueries({ queryKey: ['lt-validate'] })
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const approveM = useMutation({
    mutationFn: ({ form, approve }: { form: LtFormType; approve: boolean }) =>
      ltApi.setApproval(ltCaseId!, form, approve),
    onSuccess: (_, vars) => {
      toast.success(`${vars.form} ${vars.approve ? 'approved' : 'unapproved'}`)
      qc.invalidateQueries({ queryKey: ['lt-bundle', ltCaseId] })
      qc.invalidateQueries({ queryKey: ['lt-cases'] })
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const reviewM = useMutation({
    mutationFn: (reviewed: boolean) => ltApi.setAttorneyReview(ltCaseId!, reviewed),
    onSuccess: (_, reviewed) => {
      toast.success(reviewed ? 'Marked attorney-reviewed' : 'Cleared attorney review')
      qc.invalidateQueries({ queryKey: ['lt-case-from-case', caseId] })
      qc.invalidateQueries({ queryKey: ['lt-cases'] })
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const generateM = useMutation({
    mutationFn: () => ltApi.generate(ltCaseId!, activeForm),
    onSuccess: (g) => {
      toast.success(`${g.formType} v${g.version} generated`)
      qc.invalidateQueries({ queryKey: ['lt-generated', ltCaseId] })
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const previewM = useMutation({
    mutationFn: () => ltApi.preview(ltCaseId!, activeForm),
    onSuccess: (url) => { setPreviewUrl(url); window.open(url, '_blank') },
    onError: (e) => toast.error(unwrapError(e)),
  })

  const packetM = useMutation({
    mutationFn: () => ltApi.generatePacket(
      ltCaseId!,
      // Default packet: 6 filing-phase forms (warrant excluded)
      ['VerifiedComplaint', 'Summons', 'CertificationByLandlord', 'CertificationByAttorney', 'CertificationOfLeaseAndRegistration', 'LandlordCaseInformationStatement'] as LtFormType[],
      true,
    ),
    onSuccess: (g) => {
      toast.success(`Filing packet v${g.version} generated`)
      qc.invalidateQueries({ queryKey: ['lt-generated', ltCaseId] })
      // open
      window.open(`/api/generated-documents/${g.id}/download`, '_blank')
    },
    onError: (e) => toast.error(unwrapError(e)),
  })

  if (!caseId) return null
  if (caseQ.isLoading || ltCaseQ.isLoading || schemasQ.isLoading || bundleQ.isLoading || !draft) {
    return <div className="py-16 flex justify-center"><Spinner /></div>
  }

  const c = caseQ.data!
  const lt = ltCaseQ.data!
  const schemas = schemasQ.data ?? []
  const bundle = bundleQ.data!
  const activeSchema = schemas.find(s => s.formType === activeForm)!

  return (
    <div className="space-y-5">
      <Header caseDetail={c} ltCaseDetail={lt} />

      <Tabs<LtFormType>
        tabs={schemas.map(s => ({
          id: s.formType,
          label: (
            <span className="inline-flex items-center gap-1.5">
              {s.displayName.replace(/^[^—-]+ - /, '').replace(/^[^—-]+ — /, '')}
              {bundle.approvals[s.formType]?.isApproved && <CheckCircle2 size={12} className="text-emerald-600" />}
              {!s.isPublicCourtForm && <Badge tone="gray">Internal</Badge>}
            </span>
          ),
        }))}
        active={activeForm}
        onChange={setActiveForm}
      />

      <div className="grid lg:grid-cols-[1fr_320px] gap-5">
        <div className="space-y-5">
          <Card>
            <CardHeader
              title={activeSchema.displayName}
              subtitle={
                <span className="inline-flex items-center gap-2 text-xs">
                  <Badge tone={activeSchema.phase === 'Filing' ? 'sky' : activeSchema.phase === 'TrialCertification' ? 'amber' : 'rose'}>
                    {activeSchema.phase}
                  </Badge>
                  {activeSchema.isPublicCourtForm
                    ? <Badge tone="rose">Public — redaction enforced</Badge>
                    : <Badge tone="gray">Internal certification</Badge>}
                </span>
              }
              action={
                <div className="flex flex-wrap gap-2">
                  <button className="btn-secondary" onClick={() => setHistoryOpen(true)}>
                    <History size={14} /> Version history
                  </button>
                  <button className="btn-secondary" onClick={() => previewM.mutate()} disabled={previewM.isPending}>
                    <Eye size={14} /> Preview
                  </button>
                  <button className="btn-secondary" onClick={() => saveM.mutate()} disabled={saveM.isPending}>
                    <Save size={14} /> Save
                  </button>
                  {canApprove && (
                    <button className="btn-secondary"
                            onClick={() => approveM.mutate({ form: activeForm, approve: !bundle.approvals[activeForm]?.isApproved })}
                            disabled={approveM.isPending}>
                      <FileCheck2 size={14} />
                      {bundle.approvals[activeForm]?.isApproved ? 'Unapprove' : 'Approve'}
                    </button>
                  )}
                  <button className="btn-primary" onClick={() => generateM.mutate()} disabled={generateM.isPending}>
                    <Sparkles size={14} /> Generate PDF
                  </button>
                </div>
              }
            />
            <CardBody className="space-y-6">
              <SectionedEditor
                schema={activeSchema}
                draft={draft}
                onChange={setDraft}
              />
            </CardBody>
          </Card>
        </div>

        <div className="space-y-5">
          <ValidationSummaryCard validation={validateQ.data} loading={validateQ.isLoading} />
          <ApprovalCard
            bundle={bundle}
            schemas={schemas}
            ltAttorneyReviewed={lt.attorneyReviewed}
            canApprove={canApprove}
            onMarkReviewed={(v) => reviewM.mutate(v)}
            onGeneratePacket={() => packetM.mutate()}
            packetPending={packetM.isPending}
          />
        </div>
      </div>

      <GeneratedDocsCard documents={generatedQ.data ?? []} />

      <Modal open={historyOpen} onClose={() => setHistoryOpen(false)} title="Document version history" size="lg">
        <ul className="divide-y divide-slate-100 text-sm max-h-[60vh] overflow-y-auto">
          {(generatedQ.data ?? []).filter(g => activeForm == null || g.formType === activeForm || g.isMergedPacket).length === 0 && (
            <li className="py-3 text-slate-500">No versions yet.</li>
          )}
          {(generatedQ.data ?? []).map(g => (
            <li key={g.id} className="py-2.5 flex items-center justify-between">
              <div>
                <div className="font-medium">
                  {g.isMergedPacket
                    ? <Badge tone="violet"><Layers size={11} className="inline mr-0.5" /> Packet</Badge>
                    : <Badge tone="blue">{g.formType}</Badge>}
                  <span className="ml-2">v{g.version}</span>
                  {g.isCurrent && <Badge tone="green" className="ml-2">current</Badge>}
                </div>
                <div className="text-xs text-slate-500">{g.fileName} · {fmtDateTime(g.generatedAtUtc)} · by {g.generatedBy ?? '—'}</div>
              </div>
              <a className="btn-secondary text-xs" href={`/api/generated-documents/${g.id}/download`} target="_blank" rel="noreferrer">
                <Download size={12} /> Download
              </a>
            </li>
          ))}
        </ul>
      </Modal>
    </div>
  )
}

// ─── Header ─────────────────────────────────────────────────────────────────
function Header({ caseDetail: c, ltCaseDetail: lt }: { caseDetail: CaseDetail; ltCaseDetail: any }) {
  return (
    <div>
      <Link to={`/cases/${c.id}`} className="text-xs text-slate-500 hover:text-brand-600 inline-flex items-center gap-1">
        <ArrowLeft size={12} /> Back to case
      </Link>
      <h1 className="text-xl font-semibold mt-1">NJ LT Forms — {c.caseNumber}</h1>
      <div className="text-sm text-slate-500 flex flex-wrap items-center gap-2 mt-1">
        <span>{c.title}</span>
        <Badge tone={lt.attorneyReviewed ? 'green' : 'amber'}>
          {lt.attorneyReviewed ? 'Attorney reviewed' : 'Attorney review pending'}
        </Badge>
        {lt.totalDue != null && <Badge tone="rose">Total due: ${globalThis.Number(lt.totalDue).toFixed(2)}</Badge>}
        {lt.premisesCounty && <Badge tone="sky">{lt.premisesCounty} County</Badge>}
      </div>
    </div>
  )
}

// ─── Sectioned editor ───────────────────────────────────────────────────────
function SectionedEditor({
  schema, draft, onChange,
}: { schema: LtFormSchemaDto; draft: LtFormDataSections; onChange: (s: LtFormDataSections) => void }) {
  const set = (path: string, value: any) => {
    const next = structuredClone(draft)
    const segs = path.split('.')
    let cur: any = next
    for (let i = 0; i < segs.length - 1; i++) cur = cur[segs[i]]
    cur[segs[segs.length - 1]] = value
    onChange(next)
  }
  const sections = schema.relevantSections.map(s => s.toLowerCase())
  const has = (s: string) => sections.includes(s.toLowerCase())

  return (
    <div className="space-y-5">
      {has('Caption') && (
        <Section title="Caption" subtitle="Court / docket / case number">
          <Input label="Court name" v={draft.caption.courtName} onV={v => set('caption.courtName', v)} />
          <Input label="Court venue" v={draft.caption.courtVenue} onV={v => set('caption.courtVenue', v)} />
          <Input label="County" v={draft.caption.countyName} onV={v => set('caption.countyName', v)} />
          <Input label="Docket #" v={draft.caption.docketNumber} onV={v => set('caption.docketNumber', v)} />
          <Input label="Case #" v={draft.caption.caseNumber} onV={v => set('caption.caseNumber', v)} />
          <Date label="Filing date" v={draft.caption.filingDate} onV={v => set('caption.filingDate', v)} />
        </Section>
      )}
      {has('Attorney') && (
        <Section title="Attorney" subtitle="Auto-filled from your firm's attorney settings">
          <Input label="Firm name" v={draft.attorney.firmName} onV={v => set('attorney.firmName', v)} />
          <Input label="Attorney name" v={draft.attorney.attorneyName} onV={v => set('attorney.attorneyName', v)} />
          <Input label="Bar #" v={draft.attorney.barNumber} onV={v => set('attorney.barNumber', v)} />
          <Input label="Email" v={draft.attorney.email} onV={v => set('attorney.email', v)} />
          <Input label="Phone" v={draft.attorney.phone} onV={v => set('attorney.phone', v)} />
          <Input label="Office address" v={draft.attorney.officeAddressLine1} onV={v => set('attorney.officeAddressLine1', v)} />
          <Input label="City" v={draft.attorney.officeCity} onV={v => set('attorney.officeCity', v)} />
          <Input label="State" v={draft.attorney.officeState} onV={v => set('attorney.officeState', v)} />
          <Input label="Zip" v={draft.attorney.officePostalCode} onV={v => set('attorney.officePostalCode', v)} />
        </Section>
      )}
      {has('Plaintiff') && (
        <Section title="Plaintiff / Landlord">
          <Input label="Name" v={draft.plaintiff.name} onV={v => set('plaintiff.name', v)} />
          <Input label="Address" v={draft.plaintiff.addressLine1} onV={v => set('plaintiff.addressLine1', v)} />
          <Input label="City" v={draft.plaintiff.city} onV={v => set('plaintiff.city', v)} />
          <Input label="State" v={draft.plaintiff.state} onV={v => set('plaintiff.state', v)} />
          <Input label="Zip" v={draft.plaintiff.postalCode} onV={v => set('plaintiff.postalCode', v)} />
          <Input label="Phone" v={draft.plaintiff.phone} onV={v => set('plaintiff.phone', v)} />
          <Input label="Email" v={draft.plaintiff.email} onV={v => set('plaintiff.email', v)} />
          <Check label="Corporate plaintiff" v={draft.plaintiff.isCorporate} onV={v => set('plaintiff.isCorporate', v)} />
        </Section>
      )}
      {has('Defendant') && (
        <Section title="Defendant / Tenant" subtitle="From PMS snapshot — overrides allowed">
          <Input label="First name" v={draft.defendant.firstName} onV={v => set('defendant.firstName', v)} />
          <Input label="Last name" v={draft.defendant.lastName} onV={v => set('defendant.lastName', v)} />
          <Input label="Phone" v={draft.defendant.phone} onV={v => set('defendant.phone', v)} />
          <Input label="Email" v={draft.defendant.email} onV={v => set('defendant.email', v)} />
          <Input label="Additional occupants" v={draft.defendant.additionalOccupants} onV={v => set('defendant.additionalOccupants', v)} cols={2} />
        </Section>
      )}
      {has('Premises') && (
        <Section title="Rental premises">
          <Input label="Address" v={draft.premises.addressLine1} onV={v => set('premises.addressLine1', v)} cols={2} />
          <Input label="Address line 2" v={draft.premises.addressLine2} onV={v => set('premises.addressLine2', v)} cols={2} />
          <Input label="City" v={draft.premises.city} onV={v => set('premises.city', v)} />
          <Input label="County" v={draft.premises.county} onV={v => set('premises.county', v)} />
          <Input label="State" v={draft.premises.state} onV={v => set('premises.state', v)} />
          <Input label="Zip" v={draft.premises.postalCode} onV={v => set('premises.postalCode', v)} />
          <Input label="Unit #" v={draft.premises.unitNumber} onV={v => set('premises.unitNumber', v)} />
        </Section>
      )}
      {has('Lease') && (
        <Section title="Lease">
          <Date label="Start date" v={draft.lease.startDate} onV={v => set('lease.startDate', v)} />
          <Date label="End date" v={draft.lease.endDate} onV={v => set('lease.endDate', v)} />
          <Check label="Month-to-month" v={draft.lease.isMonthToMonth} onV={v => set('lease.isMonthToMonth', v)} />
          <Check label="Written lease" v={draft.lease.isWritten} onV={v => set('lease.isWritten', v)} />
          <Money label="Monthly rent" v={draft.lease.monthlyRent} onV={v => set('lease.monthlyRent', v)} />
          <Money label="Security deposit" v={draft.lease.securityDeposit} onV={v => set('lease.securityDeposit', v)} />
        </Section>
      )}
      {has('RentOwed') && (
        <Section title="Rent owed">
          <Date label="As of" v={draft.rentOwed.asOfDate} onV={v => set('rentOwed.asOfDate', v)} />
          <Money label="Prior balance" v={draft.rentOwed.priorBalance} onV={v => set('rentOwed.priorBalance', v)} />
          <Money label="Current month rent" v={draft.rentOwed.currentMonthRent} onV={v => set('rentOwed.currentMonthRent', v)} />
          <Money label="Total" v={draft.rentOwed.total} onV={v => set('rentOwed.total', v)} />
        </Section>
      )}
      {has('AdditionalRent') && (
        <Section title="Additional rent and fees">
          <Money label="Late fees" v={draft.additionalRent.lateFees} onV={v => set('additionalRent.lateFees', v)} />
          <Money label="Attorney fees" v={draft.additionalRent.attorneyFees} onV={v => set('additionalRent.attorneyFees', v)} />
          <Money label="Other charges" v={draft.additionalRent.otherCharges} onV={v => set('additionalRent.otherCharges', v)} />
          <Input label="Other charges description" v={draft.additionalRent.otherChargesDescription} onV={v => set('additionalRent.otherChargesDescription', v)} cols={2} />
        </Section>
      )}
      {has('FilingFee') && (
        <Section title="Filing fee">
          <Money label="Amount claimed" v={draft.filingFee.amountClaimed} onV={v => set('filingFee.amountClaimed', v)} />
          <Money label="Filing fee" v={draft.filingFee.filingFee} onV={v => set('filingFee.filingFee', v)} />
          <Check label="Apply for fee waiver" v={draft.filingFee.applyForFeeWaiver} onV={v => set('filingFee.applyForFeeWaiver', v)} />
        </Section>
      )}
      {has('Subsidy') && (
        <Section title="Subsidy / rent control">
          <Check label="Rent controlled" v={draft.subsidy.isRentControlled} onV={v => set('subsidy.isRentControlled', v)} />
          <Check label="Subsidized housing" v={draft.subsidy.isSubsidized} onV={v => set('subsidy.isSubsidized', v)} />
          <Input label="Subsidy program" v={draft.subsidy.subsidyProgram} onV={v => set('subsidy.subsidyProgram', v)} cols={2} />
        </Section>
      )}
      {has('Notices') && (
        <Section title="Required notices">
          <Check label="Notice to cease served" v={draft.notices.noticeToCeaseServed} onV={v => set('notices.noticeToCeaseServed', v)} />
          <Date label="Notice to cease date" v={draft.notices.noticeToCeaseDate} onV={v => set('notices.noticeToCeaseDate', v)} />
          <Check label="Notice to quit served" v={draft.notices.noticeToQuitServed} onV={v => set('notices.noticeToQuitServed', v)} />
          <Date label="Notice to quit date" v={draft.notices.noticeToQuitDate} onV={v => set('notices.noticeToQuitDate', v)} />
          <Input label="Service method" v={draft.notices.serviceMethod} onV={v => set('notices.serviceMethod', v)} cols={2} />
        </Section>
      )}
      {has('Registration') && (
        <Section title="Registration / ownership">
          <Check label="Registered multiple dwelling" v={draft.registration.isRegisteredMultipleDwelling} onV={v => set('registration.isRegisteredMultipleDwelling', v)} />
          <Input label="Registration number" v={draft.registration.registrationNumber} onV={v => set('registration.registrationNumber', v)} />
          <Date label="Registration date" v={draft.registration.registrationDate} onV={v => set('registration.registrationDate', v)} />
          <Check label="Owner-occupied" v={draft.registration.isOwnerOccupied} onV={v => set('registration.isOwnerOccupied', v)} />
          <IntInput label="Unit count in building" v={draft.registration.unitCountInBuilding} onV={v => set('registration.unitCountInBuilding', v)} />
        </Section>
      )}
      {has('Certification') && (
        <Section title="Certification / signature">
          <Input label="Certifier name" v={draft.certification.certifierName} onV={v => set('certification.certifierName', v)} />
          <Input label="Certifier title" v={draft.certification.certifierTitle} onV={v => set('certification.certifierTitle', v)} />
          <Date label="Certification date" v={draft.certification.certificationDate} onV={v => set('certification.certificationDate', v)} />
          <Check label="Attorney reviewed" v={draft.certification.attorneyReviewed} onV={v => set('certification.attorneyReviewed', v)} />
        </Section>
      )}
      {has('Warrant') && (
        <Section title="Warrant of removal">
          <Date label="Judgment date" v={draft.warrant.judgmentDate} onV={v => set('warrant.judgmentDate', v)} />
          <Input label="Judgment docket #" v={draft.warrant.judgmentDocketNumber} onV={v => set('warrant.judgmentDocketNumber', v)} />
          <Date label="Requested execution date" v={draft.warrant.requestedExecutionDate} onV={v => set('warrant.requestedExecutionDate', v)} />
          <Check label="Tenant still in possession" v={draft.warrant.tenantStillInPossession} onV={v => set('warrant.tenantStillInPossession', v)} />
          <Check label="Payment received since judgment" v={draft.warrant.paymentReceivedSinceJudgment} onV={v => set('warrant.paymentReceivedSinceJudgment', v)} />
          <Money label="Amount paid since judgment" v={draft.warrant.amountPaidSinceJudgment} onV={v => set('warrant.amountPaidSinceJudgment', v)} />
        </Section>
      )}
    </div>
  )
}

// ─── Validation summary panel ───────────────────────────────────────────────
function ValidationSummaryCard({ validation, loading }: { validation: LtValidationSummary | undefined; loading: boolean }) {
  if (loading || !validation) return (
    <Card><CardHeader title="Validation summary" /><CardBody><Spinner /></CardBody></Card>
  )
  const errors = validation.issues.filter(i => i.severity === 'error')
  const warns = validation.issues.filter(i => i.severity === 'warn')
  return (
    <Card>
      <CardHeader
        title="Validation summary"
        subtitle={validation.isValid
          ? <span className="inline-flex items-center gap-1 text-emerald-700"><CheckCircle2 size={12} /> Ready to generate</span>
          : <span className="inline-flex items-center gap-1 text-rose-700"><AlertTriangle size={12} /> Issues found</span>}
      />
      <CardBody className="space-y-3 text-sm">
        {errors.length > 0 && (
          <div>
            <div className="font-medium text-rose-700 mb-1">Errors ({errors.length})</div>
            <ul className="space-y-1.5">
              {errors.map((i, k) => (
                <li key={k} className="text-xs">
                  <span className="font-medium">{i.section}.{i.field}</span> — {i.message}
                </li>
              ))}
            </ul>
          </div>
        )}
        {warns.length > 0 && (
          <div>
            <div className="font-medium text-amber-700 mb-1">Warnings ({warns.length})</div>
            <ul className="space-y-1.5">
              {warns.map((i, k) => (
                <li key={k} className="text-xs">
                  <span className="font-medium">{i.section}.{i.field}</span> — {i.message}
                </li>
              ))}
            </ul>
          </div>
        )}
        {validation.redactionFindings.length > 0 && (
          <div>
            <div className="font-medium text-rose-700 mb-1 inline-flex items-center gap-1">
              <ShieldAlert size={12} /> Redaction blocked ({validation.redactionFindings.length})
            </div>
            <ul className="space-y-1.5">
              {validation.redactionFindings.map((f, k) => (
                <li key={k} className="text-xs">
                  <Badge tone="rose">{f.pattern}</Badge>{' '}
                  <span className="font-medium">{f.fieldName}</span> — sample {f.sample}
                </li>
              ))}
            </ul>
          </div>
        )}
        {errors.length === 0 && warns.length === 0 && validation.redactionFindings.length === 0 && (
          <div className="text-emerald-700 inline-flex items-center gap-1"><CheckCircle2 size={14} /> All checks passed.</div>
        )}
      </CardBody>
    </Card>
  )
}

// ─── Approval / packet card ─────────────────────────────────────────────────
function ApprovalCard({
  bundle, schemas, ltAttorneyReviewed, canApprove,
  onMarkReviewed, onGeneratePacket, packetPending,
}: {
  bundle: LtFormBundleDto
  schemas: LtFormSchemaDto[]
  ltAttorneyReviewed: boolean
  canApprove: boolean
  onMarkReviewed: (v: boolean) => void
  onGeneratePacket: () => void
  packetPending: boolean
}) {
  const filing = schemas.filter(s => s.phase === 'Filing')
  const allFilingApproved = filing.every(f => bundle.approvals[f.formType]?.isApproved)
  return (
    <Card>
      <CardHeader title="Approvals & filing packet" />
      <CardBody className="space-y-3 text-sm">
        <div>
          <div className="text-xs text-slate-500 mb-1">Per-form approvals</div>
          <ul className="space-y-1">
            {schemas.map(s => {
              const a = bundle.approvals[s.formType]
              return (
                <li key={s.formType} className="flex items-center justify-between">
                  <span className="truncate text-xs">{s.formType}</span>
                  {a?.isApproved
                    ? <Badge tone="green"><CheckCircle2 size={10} className="inline mr-0.5" /> {fmtDateTime(a.approvedAtUtc)}</Badge>
                    : <Badge tone="gray">Pending</Badge>}
                </li>
              )
            })}
          </ul>
        </div>

        {canApprove && (
          <button className="btn-secondary w-full"
                  onClick={() => onMarkReviewed(!ltAttorneyReviewed)}>
            <FileCheck2 size={14} />
            {ltAttorneyReviewed ? 'Clear attorney review' : 'Mark case as attorney-reviewed'}
          </button>
        )}

        <div className="card p-3 bg-amber-50/60 ring-amber-200 text-xs text-amber-800 inline-flex items-start gap-2">
          <Info size={12} className="mt-0.5 shrink-0" />
          Final packet generation requires both (a) attorney review, and (b) every selected form to be approved.
        </div>

        <button className="btn-primary w-full"
                disabled={packetPending || !ltAttorneyReviewed || !allFilingApproved}
                onClick={onGeneratePacket}>
          {packetPending
            ? <Spinner size={14} className="text-white" />
            : <><Layers size={14} /> Generate filing packet</>}
        </button>
        {!ltAttorneyReviewed && <div className="text-xs text-slate-500 inline-flex items-center gap-1"><Lock size={11} /> Attorney must mark this case as reviewed first.</div>}
      </CardBody>
    </Card>
  )
}

// ─── Generated docs card ────────────────────────────────────────────────────
function GeneratedDocsCard({ documents }: { documents: GeneratedDocumentDto[] }) {
  return (
    <Card>
      <CardHeader title="Generated documents" subtitle="Most recent first — version history available per form" />
      <CardBody>
        {documents.length === 0
          ? <EmptyState title="Nothing generated yet" icon={<Sparkles size={20} />} />
          : (
            <ul className="divide-y divide-slate-100">
              {documents.map(g => (
                <li key={g.id} className="py-2.5 flex items-center justify-between text-sm gap-3">
                  <div className="min-w-0">
                    <div className="font-medium truncate">{g.fileName}</div>
                    <div className="text-xs text-slate-500">
                      {g.isMergedPacket
                        ? <Badge tone="violet"><Layers size={11} className="inline mr-0.5" /> Packet</Badge>
                        : <Badge tone="blue">{g.formType}</Badge>}
                      <span className="ml-2">v{g.version} · {fmtDateTime(g.generatedAtUtc)}</span>
                      {g.isCurrent && <Badge tone="green" className="ml-2">current</Badge>}
                    </div>
                  </div>
                  <a className="btn-secondary text-xs shrink-0" href={`/api/generated-documents/${g.id}/download`} target="_blank" rel="noreferrer">
                    <Download size={12} /> Download
                  </a>
                </li>
              ))}
            </ul>
          )}
      </CardBody>
    </Card>
  )
}

// ─── Section + input primitives ─────────────────────────────────────────────
function Section({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <section>
      <div className="mb-3">
        <h3 className="text-sm font-semibold text-slate-800">{title}</h3>
        {subtitle && <p className="text-xs text-slate-500">{subtitle}</p>}
      </div>
      <div className="grid sm:grid-cols-2 gap-3">{children}</div>
    </section>
  )
}
function Input({ label, v, onV, cols = 1 }: { label: string; v: string | null | undefined; onV: (v: string | null) => void; cols?: 1 | 2 }) {
  return (
    <div className={cols === 2 ? 'sm:col-span-2' : ''}>
      <label className="label">{label}</label>
      <input className="input" value={v ?? ''} onChange={e => onV(e.target.value || null)} />
    </div>
  )
}
function Date({ label, v, onV }: { label: string; v: string | null | undefined; onV: (v: string | null) => void }) {
  // Input expects yyyy-MM-dd
  const value = v ? v.slice(0, 10) : ''
  return (
    <div>
      <label className="label">{label}</label>
      <input className="input" type="date" value={value}
        onChange={e => onV(e.target.value ? new globalThis.Date(e.target.value).toISOString() : null)} />
    </div>
  )
}
function Money({ label, v, onV }: { label: string; v: number | null | undefined; onV: (v: number | null) => void }) {
  return (
    <div>
      <label className="label">{label}</label>
      <input className="input" type="number" step="0.01" value={v ?? ''}
        onChange={e => onV(e.target.value === '' ? null : globalThis.Number(e.target.value))} />
    </div>
  )
}
function IntInput({ label, v, onV }: { label: string; v: number | null | undefined; onV: (v: number | null) => void }) {
  return (
    <div>
      <label className="label">{label}</label>
      <input className="input" type="number" value={v ?? ''}
        onChange={e => onV(e.target.value === '' ? null : globalThis.Number(e.target.value))} />
    </div>
  )
}
function Check({ label, v, onV }: { label: string; v: boolean; onV: (v: boolean) => void }) {
  return (
    <label className="text-sm inline-flex items-center gap-2 mt-6">
      <input type="checkbox" checked={!!v} onChange={e => onV(e.target.checked)} />
      {label}
    </label>
  )
}
