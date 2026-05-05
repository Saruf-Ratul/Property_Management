import { api } from './api'
import type {
  CaseActivityDto, CaseCommentDto, CaseDetail, CaseDocumentDto, CaseListItem,
  CaseStageCode, CaseStatusCode, ClientPortalDashboardDto, ClientPortalNotificationDto,
  PagedResult,
} from '@/types/portal'

export const portalApi = {
  dashboard: async () =>
    (await api.get<ClientPortalDashboardDto>('/client-portal/dashboard')).data,

  listCases: async (params: {
    page?: number; pageSize?: number; search?: string;
    stage?: CaseStageCode; status?: CaseStatusCode
  } = {}) =>
    (await api.get<PagedResult<CaseListItem>>('/client-portal/cases', { params })).data,

  getCase: async (id: string) =>
    (await api.get<CaseDetail>(`/client-portal/cases/${id}`)).data,

  timeline: async (id: string) =>
    (await api.get<CaseActivityDto[]>(`/client-portal/cases/${id}/timeline`)).data,

  comments: async (id: string) =>
    (await api.get<CaseCommentDto[]>(`/client-portal/cases/${id}/comments`)).data,

  documents: async (id: string) =>
    (await api.get<CaseDocumentDto[]>(`/client-portal/cases/${id}/documents`)).data,

  notifications: async (take = 25) =>
    (await api.get<ClientPortalNotificationDto[]>('/client-portal/notifications', { params: { take } })).data,

  addComment: async (id: string, body: string) =>
    (await api.post<CaseCommentDto>(`/client-portal/cases/${id}/comments`, { body })).data,

  uploadDocument: async (id: string, file: File, description?: string) => {
    const fd = new FormData()
    fd.append('file', file)
    if (description) fd.append('description', description)
    const r = await api.post<CaseDocumentDto>(`/client-portal/cases/${id}/documents`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return r.data
  },
}
