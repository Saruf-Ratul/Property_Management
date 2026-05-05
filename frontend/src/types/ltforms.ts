import type { LtFormType } from './index'

export type LtFormPhase = 'Filing' | 'TrialCertification' | 'Warrant'

export interface LtCaseSummaryDto {
  id: string
  caseId: string
  caseNumber: string
  caseTitle: string
  clientName: string
  clientId: string
  stageCode: string
  stageName: string
  statusCode: string
  statusName: string
  phase: LtFormPhase
  phaseName: string
  attorneyReviewed: boolean
  formsApproved: number
  formsTotal: number
  generatedFormCount: number
  generatedPacketCount: number
  latestGeneratedAtUtc: string | null
  totalDue: number | null
  createdAtUtc: string
}

export interface LtCaseDetailDto {
  id: string
  caseId: string
  caseNumber: string
  caseTitle: string
  clientName: string
  clientId: string
  phase: LtFormPhase
  attorneyReviewed: boolean
  attorneyReviewedAtUtc: string | null
  attorneyReviewedByName: string | null
  premisesAddressLine1: string | null
  premisesCity: string | null
  premisesCounty: string | null
  premisesState: string | null
  premisesPostalCode: string | null
  landlordName: string | null
  rentDue: number | null
  lateFees: number | null
  otherCharges: number | null
  totalDue: number | null
  rentDueAsOf: string | null
  isRegisteredMultipleDwelling: boolean
  registrationNumber: string | null
}

export interface LtFormSchemaDto {
  formType: LtFormType
  displayName: string
  phase: LtFormPhase
  isPublicCourtForm: boolean
  relevantSections: string[]
}

export interface LtFormApprovalDto {
  isApproved: boolean
  approvedAtUtc: string | null
  approvedBy: string | null
}

export interface LtFormBundleDto {
  ltCaseId: string
  sections: LtFormDataSections
  approvals: Record<LtFormType, LtFormApprovalDto>
  updatedAtUtc: string | null
}

export interface RedactionFinding {
  fieldName: string
  pattern: string
  sample: string
}

export interface LtValidationIssue {
  severity: 'error' | 'warn' | string
  section: string
  field: string
  message: string
}

export interface LtValidationSummary {
  isValid: boolean
  issues: LtValidationIssue[]
  redactionFindings: RedactionFinding[]
}

// ─── Structured form-data sections ──────────────────────────────────────────
export interface LtFormDataSections {
  caption: CaptionSection
  attorney: AttorneySection
  plaintiff: PlaintiffSection
  defendant: DefendantSection
  premises: PremisesSection
  lease: LeaseSection
  rentOwed: RentOwedSection
  additionalRent: AdditionalRentSection
  filingFee: FilingFeeSection
  subsidy: SubsidyRentControlSection
  notices: RequiredNoticesSection
  registration: RegistrationOwnershipSection
  certification: CertificationSection
  warrant: WarrantSection
}
export interface CaptionSection { courtName: string | null; courtVenue: string | null; countyName: string | null; docketNumber: string | null; caseNumber: string | null; filingDate: string | null }
export interface AttorneySection { firmName: string | null; attorneyName: string | null; barNumber: string | null; email: string | null; phone: string | null; officeAddressLine1: string | null; officeAddressLine2: string | null; officeCity: string | null; officeState: string | null; officePostalCode: string | null }
export interface PlaintiffSection { name: string | null; addressLine1: string | null; addressLine2: string | null; city: string | null; state: string | null; postalCode: string | null; phone: string | null; email: string | null; isCorporate: boolean }
export interface DefendantSection { firstName: string | null; lastName: string | null; phone: string | null; email: string | null; additionalOccupants: string | null }
export interface PremisesSection { addressLine1: string | null; addressLine2: string | null; city: string | null; county: string | null; state: string | null; postalCode: string | null; unitNumber: string | null }
export interface LeaseSection { startDate: string | null; endDate: string | null; isMonthToMonth: boolean; isWritten: boolean; monthlyRent: number | null; securityDeposit: number | null; rentDueDay: string | null }
export interface RentOwedSection { asOfDate: string | null; priorBalance: number | null; currentMonthRent: number | null; total: number | null }
export interface AdditionalRentSection { lateFees: number | null; attorneyFees: number | null; otherCharges: number | null; otherChargesDescription: string | null }
export interface FilingFeeSection { amountClaimed: number | null; filingFee: number | null; applyForFeeWaiver: boolean }
export interface SubsidyRentControlSection { isRentControlled: boolean; isSubsidized: boolean; subsidyProgram: string | null }
export interface RequiredNoticesSection { noticeToCeaseServed: boolean; noticeToCeaseDate: string | null; noticeToQuitServed: boolean; noticeToQuitDate: string | null; serviceMethod: string | null }
export interface RegistrationOwnershipSection { isRegisteredMultipleDwelling: boolean; registrationNumber: string | null; registrationDate: string | null; isOwnerOccupied: boolean; unitCountInBuilding: number | null }
export interface CertificationSection { certifierName: string | null; certifierTitle: string | null; certificationDate: string | null; attorneyReviewed: boolean }
export interface WarrantSection { judgmentDate: string | null; judgmentDocketNumber: string | null; requestedExecutionDate: string | null; tenantStillInPossession: boolean; paymentReceivedSinceJudgment: boolean; amountPaidSinceJudgment: number | null }
