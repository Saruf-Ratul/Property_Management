import { api, unwrapError } from './api'
import type {
  ApiResponse, CreatePmsIntegrationRequest, PmsConnectionTestRequest,
  PmsConnectionTestResult, PmsIntegrationDto, PmsSyncRequest, PmsSyncResult,
  SyncLogDto, UpdatePmsIntegrationRequest,
} from '@/types/pms'
import type { PagedResult } from '@/types'

/**
 * Thin client around the PMS Integration module. The backend wraps every response
 * in { success, message, data, errors } — these helpers unwrap that envelope and
 * surface useful error messages to the UI.
 */
export const pmsApi = {
  list: async (params: { page?: number; pageSize?: number; search?: string; clientId?: string } = {}) => {
    const r = await api.get<ApiResponse<PagedResult<PmsIntegrationDto>>>('/pms-integrations', { params })
    return assertOk(r.data)
  },

  get: async (id: string) => {
    const r = await api.get<ApiResponse<PmsIntegrationDto>>(`/pms-integrations/${id}`)
    return assertOk(r.data)
  },

  create: async (req: CreatePmsIntegrationRequest) => {
    const r = await api.post<ApiResponse<PmsIntegrationDto>>('/pms-integrations', req)
    return assertOk(r.data)
  },

  update: async (id: string, req: UpdatePmsIntegrationRequest) => {
    const r = await api.put<ApiResponse<PmsIntegrationDto>>(`/pms-integrations/${id}`, req)
    return assertOk(r.data)
  },

  testStored: async (id: string) => {
    const r = await api.post<ApiResponse<PmsConnectionTestResult>>(`/pms-integrations/${id}/test`)
    return assertOk(r.data)
  },

  testAdHoc: async (req: PmsConnectionTestRequest) => {
    const r = await api.post<ApiResponse<PmsConnectionTestResult>>('/pms-integrations/test', req)
    return assertOk(r.data)
  },

  sync: async (id: string, req: PmsSyncRequest = { fullSync: true, runInBackground: true }) => {
    const r = await api.post<ApiResponse<PmsSyncResult>>(`/pms-integrations/${id}/sync`, req)
    return assertOk(r.data)
  },

  syncLogs: async (id: string, take = 25) => {
    const r = await api.get<ApiResponse<SyncLogDto[]>>(`/pms-integrations/${id}/sync-logs`, { params: { take } })
    return assertOk(r.data)
  },
}

function assertOk<T>(res: ApiResponse<T>): { data: T; message: string } {
  if (!res.success || res.data === undefined) {
    const detail = res.errors?.length ? ` (${res.errors.join('; ')})` : ''
    throw new Error(`${res.message}${detail}`)
  }
  return { data: res.data, message: res.message }
}

export { unwrapError }
