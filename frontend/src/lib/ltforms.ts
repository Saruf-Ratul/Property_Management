import { api } from './api'
import type { LtFormType, GeneratedDocumentDto, PagedResult } from '@/types'
import type {
  LtCaseDetailDto, LtCaseSummaryDto, LtFormBundleDto, LtFormPhase, LtFormSchemaDto,
  LtFormDataSections, LtValidationSummary,
} from '@/types/ltforms'

export const ltApi = {
  list: async (params: { page?: number; pageSize?: number; phase?: LtFormPhase; clientId?: string; search?: string }) =>
    (await api.get<PagedResult<LtCaseSummaryDto>>('/lt-cases', { params })).data,

  get: async (id: string) => (await api.get<LtCaseDetailDto>(`/lt-cases/${id}`)).data,
  schemas: async () => (await api.get<LtFormSchemaDto[]>('/lt-cases/schemas')).data,
  createFromCase: async (caseId: string) =>
    (await api.post<LtCaseDetailDto>(`/lt-cases/create-from-case/${caseId}`)).data,

  getBundle: async (id: string) => (await api.get<LtFormBundleDto>(`/lt-cases/${id}/form-data`)).data,
  saveBundle: async (id: string, sections: LtFormDataSections) =>
    (await api.put<LtFormBundleDto>(`/lt-cases/${id}/form-data`, { sections })).data,

  validate: async (id: string, formType: LtFormType) =>
    (await api.get<LtValidationSummary>(`/lt-cases/${id}/validate/${formType}`)).data,

  setApproval: async (id: string, formType: LtFormType, isApproved: boolean) =>
    (await api.put(`/lt-cases/${id}/forms/${formType}/approval`, { isApproved })).data,

  setAttorneyReview: async (id: string, reviewed: boolean) =>
    (await api.put<LtCaseDetailDto>(`/lt-cases/${id}/attorney-review`, { reviewed })).data,

  generate: async (id: string, formType: LtFormType, overrides?: Record<string, string | null>) =>
    (await api.post<GeneratedDocumentDto>(`/lt-cases/${id}/generate-form/${formType}`, { overrides, preview: false })).data,

  /** Returns a Blob URL the caller can open in a new tab. */
  preview: async (id: string, formType: LtFormType, overrides?: Record<string, string | null>) => {
    const r = await api.post(`/lt-cases/${id}/generate-form/${formType}?preview=true`,
      { overrides, preview: true }, { responseType: 'blob' })
    const blob = r.data as Blob
    return URL.createObjectURL(blob)
  },

  generatePacket: async (id: string, forms: LtFormType[], requireApproval = true) =>
    (await api.post<GeneratedDocumentDto>(`/lt-cases/${id}/generate-packet`, { forms, requireApproval })).data,

  generated: async (id: string) =>
    (await api.get<GeneratedDocumentDto[]>(`/lt-cases/${id}/generated-documents`)).data,
}
