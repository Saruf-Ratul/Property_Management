import type { PmsIntegrationDto, PmsProvider, SyncStatus, SyncLogDto } from './index'

export interface ApiResponse<T> {
  success: boolean
  message: string
  data?: T
  errors?: string[]
}

export interface CreatePmsIntegrationRequest {
  clientId: string
  provider: PmsProvider
  displayName: string
  baseUrl?: string | null
  username?: string | null
  password?: string | null
  companyCode?: string | null
  locationId?: string | null
  syncIntervalMinutes: number
}

export interface UpdatePmsIntegrationRequest {
  displayName: string
  baseUrl?: string | null
  username?: string | null
  /** Empty/undefined keeps the previously stored password. */
  password?: string | null
  companyCode?: string | null
  locationId?: string | null
  syncIntervalMinutes: number
  isActive: boolean
}

export interface PmsConnectionTestRequest {
  provider?: PmsProvider | null
  baseUrl?: string | null
  username?: string | null
  password?: string | null
  companyCode?: string | null
  locationId?: string | null
  apiKey?: string | null
}

export interface PmsConnectionTestResult {
  isConnected: boolean
  message: string
  serverVersion?: string | null
  latencyMs: number
  testedAtUtc: string
}

export interface PmsSyncRequest {
  fullSync?: boolean
  syncProperties?: boolean
  syncUnits?: boolean
  syncTenants?: boolean
  syncLeases?: boolean
  syncLedgerItems?: boolean
  runInBackground?: boolean
}

export interface PmsSyncResult {
  syncLogId: string
  integrationId: string
  status: SyncStatus
  propertiesSynced: number
  unitsSynced: number
  tenantsSynced: number
  leasesSynced: number
  ledgerItemsSynced: number
  startedAtUtc: string
  finishedAtUtc?: string | null
  message?: string | null
  errorDetail?: string | null
  backgroundJobId?: string | null
}

export type { PmsIntegrationDto, SyncLogDto, SyncStatus, PmsProvider }
