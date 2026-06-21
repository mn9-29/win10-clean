export type CategoryKey =
  | 'apps'
  | 'privacy'
  | 'services'
  | 'gaming'
  | 'performance'
  | 'network'
  | 'updates'
  | 'ui'
  | 'maintenance'
  | 'cleanup'
  | 'system'
  | 'install'

export interface CatalogItem {
  id: string
  title: string
  desc: string
  category: CategoryKey
  work: boolean
  gaming: boolean
  basic: boolean
}

export type DeviceMode = 'gaming' | 'programming' | 'office'

export interface DashboardData {
  os: string
  osBuild: string
  computer: string
  user: string
  cpu: string
  cpuLoad: number // 0-100
  cpuCores: number
  memUsedGB: number
  memTotalGB: number
  gpu: string
  diskLabel: string
  diskUsedGB: number
  diskTotalGB: number
  uptime: string
  ramPillGB: number
  status: { label: string; ok: boolean }[]
}

export interface BridgeResponse<T = unknown> {
  id: string
  ok: boolean
  data?: T
  error?: string
}

export interface LogEvent {
  event: 'log'
  line: string
  level?: 'info' | 'ok' | 'warn' | 'err'
}

export interface ProgressEvent {
  event: 'progress'
  value: number // 0-100
  status: string
}

export interface DashboardEvent {
  event: 'dashboard'
  data: DashboardData
}

export type PushEvent = LogEvent | ProgressEvent | DashboardEvent

export interface ApplyRequest {
  ids: string[]
  restorePoint: boolean
  backupReg: boolean
}
