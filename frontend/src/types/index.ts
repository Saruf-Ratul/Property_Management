export type Role = 'FirmAdmin' | 'Lawyer' | 'Paralegal' | 'ClientAdmin' | 'ClientUser' | 'Auditor'

export interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  fullName: string
  lawFirmId: string
  clientId: string | null
  roles: Role[]
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  expiresAtUtc: string
  user: User
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type CaseStageCode =
  | 'Intake' | 'Draft' | 'FormReview' | 'ReadyToFile' | 'Filed'
  | 'CourtDateScheduled' | 'Judgment' | 'Settlement' | 'Dismissed'
  | 'WarrantRequested' | 'Closed'

export type CaseStatusCode = 'Open' | 'OnHold' | 'Closed' | 'Cancelled'

export type CaseType =
  | 'LandlordTenantEviction'
  | 'LandlordTenantHoldover'
  | 'Other'

export type LtFormType =
  | 'VerifiedComplaint'
  | 'Summons'
  | 'CertificationByLandlord'
  | 'CertificationByAttorney'
  | 'CertificationOfLeaseAndRegistration'
  | 'LandlordCaseInformationStatement'
  | 'RequestForResidentialWarrantOfRemoval'

export type DocumentType =
  | 'Lease' | 'Ledger' | 'NoticeToQuit' | 'NoticeToCease'
  | 'RegistrationStatement' | 'CertifiedMail' | 'Court' | 'Generated' | 'Other'

export type SyncStatus = 'Started' | 'Succeeded' | 'Failed' | 'PartiallySucceeded'

export type PmsProvider = 'RentManager' | 'Yardi' | 'AppFolio' | 'Buildium' | 'PropertyFlow'

export interface ClientDto {
  id: string
  name: string
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  city: string | null
  state: string | null
  isActive: boolean
  integrationsCount: number
  casesCount: number
}

export interface PropertyDto {
  id: string
  integrationId: string
  externalId: string
  name: string
  addressLine1: string | null
  city: string | null
  state: string | null
  postalCode: string | null
  county: string | null
  unitCount: number | null
  isActive: boolean
}

export interface UnitDto {
  id: string
  propertyId: string
  propertyName: string
  externalId: string
  unitNumber: string
  bedrooms: number | null
  bathrooms: number | null
  marketRent: number | null
  isOccupied: boolean
}

export interface TenantDto {
  id: string
  externalId: string
  firstName: string
  lastName: string
  fullName: string
  email: string | null
  phone: string | null
  isActive: boolean
  currentBalance: number
  unitNumber: string | null
  propertyName: string | null
}

export interface LeaseDto {
  id: string
  externalId: string
  tenantId: string
  tenantName: string
  unitId: string
  unitNumber: string
  propertyName: string
  startDate: string
  endDate: string | null
  monthlyRent: number
  currentBalance: number
  isActive: boolean
}

export interface LedgerItemDto {
  id: string
  postedDate: string
  dueDate: string | null
  category: string
  description: string | null
  amount: number
  balance: number
  isCharge: boolean
  isPayment: boolean
}

export interface DelinquentTenantDto {
  tenantId: string
  tenantName: string
  leaseId: string
  propertyName: string
  unitNumber: string
  currentBalance: number
  monthlyRent: number
  daysDelinquent: number
  clientName: string
  clientId: string
  propertyId: string
}

export interface PropertyDetailDto {
  id: string
  integrationId: string
  integrationDisplayName: string
  provider: PmsProvider
  clientId: string
  clientName: string
  externalId: string
  name: string
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  state: string | null
  postalCode: string | null
  county: string | null
  unitCount: number | null
  occupiedUnitCount: number
  vacantUnitCount: number
  activeLeaseCount: number
  delinquentTenantCount: number
  outstandingBalance: number
  averageMarketRent: number
  isActive: boolean
  createdAtUtc: string
}

export interface TenantDetailDto {
  id: string
  externalId: string
  firstName: string
  lastName: string
  fullName: string
  email: string | null
  phone: string | null
  dateOfBirth: string | null
  isActive: boolean
  integrationId: string
  integrationDisplayName: string
  clientId: string
  clientName: string
  currentBalance: number
  activeLeaseId: string | null
  propertyId: string | null
  propertyName: string | null
  unitNumber: string | null
  monthlyRent: number | null
  leaseStart: string | null
  leaseEnd: string | null
}

export interface PropertyLedgerSummaryDto {
  propertyId: string
  totalCharges: number
  totalPayments: number
  outstandingBalance: number
  activeLeases: number
  delinquentLeases: number
  oldestUnpaidPostedAt: string | null
  recentItems: LedgerItemDto[]
}

export interface TopDelinquentPropertyDto {
  propertyId: string
  propertyName: string
  clientName: string
  clientId: string
  delinquentTenantCount: number
  outstandingBalance: number
  largestSingleBalance: number
}

export interface DelinquencyStatsDto {
  totalDelinquentTenants: number
  totalOutstandingBalance: number
  averageBalance: number
  oldestUnpaidDays: number
  topPropertiesByBalance: TopDelinquentPropertyDto[]
  oldestUnpaidTenants: DelinquentTenantDto[]
}

export interface PmsIntegrationDto {
  id: string
  clientId: string
  clientName: string
  provider: PmsProvider
  displayName: string
  baseUrl: string | null
  companyCode: string | null
  locationId: string | null
  isActive: boolean
  lastSyncAtUtc: string | null
  lastSyncStatus: SyncStatus | null
  lastSyncMessage: string | null
  syncIntervalMinutes: number
}

export interface SyncLogDto {
  id: string
  integrationId: string
  startedAtUtc: string
  finishedAtUtc: string | null
  status: SyncStatus
  propertiesSynced: number
  unitsSynced: number
  tenantsSynced: number
  leasesSynced: number
  ledgerItemsSynced: number
  message: string | null
  errorDetail: string | null
}

export interface CaseListItem {
  id: string
  caseNumber: string
  title: string
  caseType: CaseType
  clientName: string
  clientId: string
  stageName: string
  stageCode: CaseStageCode
  statusName: string
  statusCode: CaseStatusCode
  assignedAttorney: string | null
  filedOnUtc: string | null
  courtDateUtc: string | null
  amountInControversy: number | null
  createdAtUtc: string
}

export interface LtCaseDto {
  id: string
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
  attorneyReviewed: boolean
}

export interface CaseDetail {
  id: string
  caseNumber: string
  title: string
  caseType: CaseType
  clientId: string
  clientName: string
  caseStageId: string
  stageName: string
  stageCode: CaseStageCode
  caseStatusId: string
  statusName: string
  statusCode: CaseStatusCode
  assignedAttorneyId: string | null
  assignedAttorney: string | null
  assignedParalegalId: string | null
  assignedParalegal: string | null
  pmsLeaseId: string | null
  pmsTenantId: string | null
  pmsPropertyId: string | null
  pmsUnitId: string | null
  pmsSnapshotJson: string | null
  pmsSnapshotTakenAtUtc: string | null
  amountInControversy: number | null
  filedOnUtc: string | null
  courtDateUtc: string | null
  courtDocketNumber: string | null
  courtVenue: string | null
  outcome: string | null
  description: string | null
  createdAtUtc: string
  ltCase: LtCaseDto | null
}

export interface CaseStageDto {
  id: string
  code: CaseStageCode
  name: string
  sortOrder: number
  isTerminal: boolean
}

export interface CaseStatusDto {
  id: string
  code: CaseStatusCode
  name: string
  isTerminal: boolean
}

export interface CaseCommentDto {
  id: string
  caseId: string
  authorName: string
  body: string
  isInternal: boolean
  createdAtUtc: string
}

export interface CasePaymentDto {
  id: string
  receivedOnUtc: string
  amount: number
  method: string | null
  reference: string | null
  notes: string | null
}

export interface CaseDocumentDto {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  documentType: DocumentType
  description: string | null
  isClientVisible: boolean
  createdAtUtc: string
}

export interface CaseActivityDto {
  id: string
  occurredAtUtc: string
  activityType: string
  summary: string
  details: string | null
  actorName: string | null
}

export interface GeneratedDocumentDto {
  id: string
  caseId: string
  formType: LtFormType | null
  fileName: string
  version: number
  isMergedPacket: boolean
  isCurrent: boolean
  sizeBytes: number
  generatedBy: string | null
  generatedAtUtc: string
}

export interface LtFormDataDto {
  id: string
  ltCaseId: string
  formType: LtFormType
  dataJson: string
  isApproved: boolean
  approvedAtUtc: string | null
  approvedBy: string | null
}

export interface LtFormAutofillResponse {
  formType: LtFormType
  fields: Record<string, string | null>
}

export interface AssigneeDto {
  id: string
  fullName: string
  email: string
  role: Role
}

export interface CaseSnapshotData {
  property: {
    name: string | null
    addressLine1: string | null
    city: string | null
    state: string | null
    postalCode: string | null
    county: string | null
  } | null
  unit: { unitNumber: string | null; bedrooms: number | null; bathrooms: number | null; marketRent: number | null } | null
  tenant: { firstName: string | null; lastName: string | null; email: string | null; phone: string | null } | null
  lease: {
    startDate: string | null
    endDate: string | null
    monthlyRent: number | null
    securityDeposit: number | null
    isMonthToMonth: boolean
    currentBalance: number | null
  } | null
  ledger: Array<{
    postedDate: string
    category: string
    description: string | null
    amount: number
    balance: number
    isCharge: boolean
    isPayment: boolean
  }> | null
}

export interface CaseSnapshotDto {
  caseId: string
  takenAtUtc: string | null
  data: CaseSnapshotData | null
}

export type CaseListTab = 'All' | 'Active' | 'Filed' | 'Closed'

export interface DashboardStats {
  totalCases: number
  activeCases: number
  closedCases: number
  delinquentTenants: number
  totalOutstandingBalance: number
  casesByStage: { code: CaseStageCode; name: string; count: number }[]
  casesByClient: { clientId: string; clientName: string; count: number }[]
  recentActivity: { occurredAtUtc: string; summary: string; caseNumber: string | null }[]
  pmsSyncStatus: {
    integrationId: string
    displayName: string
    clientName: string
    lastSyncAtUtc: string | null
    lastSyncStatus: SyncStatus | null
  }[]
}

export interface AuditLogDto {
  id: string
  occurredAtUtc: string
  action: string
  entityType: string
  entityId: string | null
  summary: string | null
  userEmail: string | null
  ipAddress: string | null
}

export interface AuditLogDetailDto {
  id: string
  occurredAtUtc: string
  action: string
  entityType: string
  entityId: string | null
  summary: string | null
  userEmail: string | null
  userId: string | null
  lawFirmId: string | null
  ipAddress: string | null
  userAgent: string | null
  payloadJson: string | null
  oldValueJson: string | null
  newValueJson: string | null
}
