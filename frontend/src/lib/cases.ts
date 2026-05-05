import { api } from './api'
import type {
  AssigneeDto, CaseActivityDto, CaseCommentDto, CaseDetail, CaseDocumentDto,
  CaseListItem, CasePaymentDto, CaseSnapshotDto, CaseStageCode, CaseStatusCode,
  GeneratedDocumentDto, LedgerItemDto, PagedResult, CaseListTab, CaseStageDto,
  CaseStatusDto,
} from '@/types'

export const casesApi = {
  list: async (params: {
    page?: number; pageSize?: number; search?: string; tab?: CaseListTab
    clientId?: string; stage?: CaseStageCode; status?: CaseStatusCode
    assignedAttorneyId?: string; assignedParalegalId?: string
    createdFrom?: string; createdTo?: string
  }) =>
    (await api.get<PagedResult<CaseListItem>>('/cases', { params })).data,

  get: async (id: string) => (await api.get<CaseDetail>(`/cases/${id}`)).data,
  snapshot: async (id: string) => (await api.get<CaseSnapshotDto>(`/cases/${id}/snapshot`)).data,
  comments: async (id: string) => (await api.get<CaseCommentDto[]>(`/cases/${id}/comments`)).data,
  payments: async (id: string) => (await api.get<CasePaymentDto[]>(`/cases/${id}/payments`)).data,
  documents: async (id: string) => (await api.get<CaseDocumentDto[]>(`/cases/${id}/documents`)).data,
  activities: async (id: string) => (await api.get<CaseActivityDto[]>(`/cases/${id}/activities`)).data,
  generated: async (id: string) =>
    (await api.get<GeneratedDocumentDto[]>(`/cases/${id}/lt-forms/generated`)).data,
  ledger: async (leaseId: string) =>
    (await api.get<LedgerItemDto[]>(`/leases/${leaseId}/ledger`)).data,

  stages: async () => (await api.get<CaseStageDto[]>(`/cases/stages`)).data,
  statuses: async () => (await api.get<CaseStatusDto[]>(`/cases/statuses`)).data,
  assignees: async () => (await api.get<AssigneeDto[]>(`/cases/assignees`)).data,

  changeStage: async (id: string, body: { stageCode: CaseStageCode; note?: string | null }) =>
    (await api.put<CaseDetail>(`/cases/${id}/stage`, body)).data,
  changeStatus: async (id: string, body: { statusCode: CaseStatusCode; note?: string | null }) =>
    (await api.put<CaseDetail>(`/cases/${id}/status`, body)).data,
  assign: async (id: string, body: { attorneyId?: string | null; paralegalId?: string | null; note?: string | null }) =>
    (await api.put<CaseDetail>(`/cases/${id}/assign`, body)).data,
  close: async (id: string, body: { outcome?: string | null; notes?: string | null }) =>
    (await api.put<CaseDetail>(`/cases/${id}/close`, body)).data,
  snapshotPms: async (id: string) => (await api.post<CaseDetail>(`/cases/${id}/snapshot`)).data,

  addComment: async (id: string, body: { body: string; isInternal: boolean }) =>
    (await api.post<CaseCommentDto>(`/cases/${id}/comments`, body)).data,
  addPayment: async (id: string, body: {
    receivedOnUtc: string; amount: number;
    method?: string | null; reference?: string | null; notes?: string | null
  }) => (await api.post<CasePaymentDto>(`/cases/${id}/payments`, body)).data,

  createFromPms: async (body: {
    pmsLeaseId: string; clientId: string; title?: string;
    caseType: 'LandlordTenantEviction' | 'LandlordTenantHoldover' | 'Other';
    assignedAttorneyId?: string | null; assignedParalegalId?: string | null;
    description?: string | null; complianceConfirmed: boolean
  }) => (await api.post<CaseDetail>('/cases/create-from-pms', body)).data,
}
