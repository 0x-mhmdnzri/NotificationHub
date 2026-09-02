import { api } from './client'

export interface FlowNodeState {
  id: string
  title: string
  subtitle: string
  category: string
  count: number
  active: boolean
}

export interface FlowItemDto {
  id: string
  recipient: string
  channel: string
  providerId?: string | null
  status: string
  attemptCount: number
  latencyMs?: number | null
  errorHuman?: string | null
  createdAt: string
  updatedAt: string
  correlationId?: string | null
  category?: string | null
}

export interface FlowEventDto {
  at: string
  message: string
  severity: 'info' | 'success' | 'warn' | 'error' | string
  notificationId?: string | null
  recipient?: string | null
}

export interface NotificationFlowSnapshot {
  queued: number
  sending: number
  delivered: number
  failed: number
  avgLatencyMs?: number | null
  nodes: FlowNodeState[]
  items: FlowItemDto[]
  events: FlowEventDto[]
}

export const flowApi = {
  snapshot: (take = 80) => api.get<NotificationFlowSnapshot>(`/api/v1/notifications/flow?take=${take}`),
}
