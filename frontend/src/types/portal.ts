import type {
  CaseActivityDto, CaseCommentDto, CaseDetail, CaseDocumentDto,
  CaseListItem, CaseStageCode, CaseStatusCode, PagedResult,
} from './index'

export interface UpcomingCourtDateDto {
  caseId: string
  caseNumber: string
  caseTitle: string
  courtDateUtc: string
  courtVenue: string | null
  courtDocketNumber: string | null
}

export interface ClientPortalNotificationDto {
  id: string
  caseId: string
  caseNumber: string
  caseTitle: string
  occurredAtUtc: string
  activityType: string
  summary: string
  severity: 'default' | 'info' | string
  isHighlighted: boolean
}

export interface ClientPortalDashboardDto {
  clientId: string
  clientName: string
  totalCases: number
  activeCases: number
  closedCases: number
  casesInFiling: number
  casesInTrialOrJudgment: number
  casesAwaitingWarrant: number
  totalAmountInControversy: number
  documentsAvailableCount: number
  unreadNotificationCount: number
  nextCourtDateUtc: string | null
  upcomingCourtDates: UpcomingCourtDateDto[]
  recentActivity: ClientPortalNotificationDto[]
}

export type {
  CaseActivityDto, CaseCommentDto, CaseDetail, CaseDocumentDto,
  CaseListItem, CaseStageCode, CaseStatusCode, PagedResult,
}
