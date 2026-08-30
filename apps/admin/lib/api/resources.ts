import { api } from './client'
import type {
  AddRecipientsRequest, BroadcastRequest, ConsentRecord, CreateCampaignRequest, EngagementEvent,
  RegisterDeviceRequest, SegmentDefinition, SegmentMatchPayload, TemplateDefinition, TopicDefinition,
  UserPreference, WebhookSubscription, WorkflowDefinition, WorkflowStartRequest
} from '@/types/api'

export const resourcesApi = {
  plugins: () => api.get<unknown[]>('/api/v1/plugins'),
  preferences: {
    get: (userId: string, tenantId?: string) => api.get<unknown>(`/api/v1/preferences/${encodeURIComponent(userId)}${tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''}`),
    save: (payload: UserPreference) => api.put<unknown>('/api/v1/preferences', payload),
  },
  webhooks: { create: (payload: WebhookSubscription) => api.post<unknown>('/api/v1/webhooks', payload) },
  consents: {
    record: (payload: ConsentRecord) => api.post<unknown>('/api/v1/consents', payload),
    list: (subjectId: string, tenantId?: string) => api.get<unknown[]>(`/api/v1/consents/${encodeURIComponent(subjectId)}${tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''}`),
    evaluate: (params: { subjectId: string; purpose: string; channel?: string; tenantId?: string }) => {
      const q = new URLSearchParams({ subjectId: params.subjectId, purpose: params.purpose })
      if (params.channel) q.set('channel', params.channel); if (params.tenantId) q.set('tenantId', params.tenantId)
      return api.post<unknown>(`/api/v1/consents/evaluate?${q}`)
    },
  },
  workflows: {
    save: (payload: WorkflowDefinition) => api.post<unknown>('/api/v1/workflows', payload),
    start: (payload: WorkflowStartRequest) => api.post<unknown>('/api/v1/workflows/start', payload),
    getRun: (runId: string) => api.get<unknown>(`/api/v1/workflows/runs/${encodeURIComponent(runId)}`),
    timeline: (runId: string) => api.get<unknown>(`/api/v1/workflows/runs/${encodeURIComponent(runId)}/timeline`),
    cancel: (runId: string) => api.post<unknown>(`/api/v1/workflows/runs/${encodeURIComponent(runId)}/cancel`),
  },
  segments: {
    save: (payload: SegmentDefinition) => api.post<unknown>('/api/v1/segments', payload),
    get: (key: string, tenantId?: string) => api.get<unknown>(`/api/v1/segments/${encodeURIComponent(key)}${tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''}`),
    match: (key: string, payload: SegmentMatchPayload, tenantId?: string) => api.post<unknown>(`/api/v1/segments/${encodeURIComponent(key)}/match${tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''}`, payload),
  },
  engagement: {
    track: (payload: EngagementEvent) => api.post<unknown>('/api/v1/engagement', payload),
    list: (notificationId: string) => api.get<unknown[]>(`/api/v1/notifications/${encodeURIComponent(notificationId)}/engagement`),
    stats: (params: { from?: string; to?: string; tenantId?: string } = {}) => {
      const q = new URLSearchParams(); if (params.from) q.set('from', params.from); if (params.to) q.set('to', params.to); if (params.tenantId) q.set('tenantId', params.tenantId)
      return api.get<unknown>(`/api/v1/engagement/stats${q.toString() ? `?${q}` : ''}`)
    },
  },
  devices: {
    register: (payload: RegisterDeviceRequest) => api.post<unknown>('/api/v1/devices', payload),
    unregister: (params: { userId: string; token: string; tenantId?: string }) => {
      const q = new URLSearchParams({ userId: params.userId, token: params.token }); if (params.tenantId) q.set('tenantId', params.tenantId)
      return api.delete<void>(`/api/v1/devices?${q}`)
    },
    list: (userId: string, tenantId?: string) => api.get<unknown[]>(`/api/v1/devices/${encodeURIComponent(userId)}${tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''}`),
  },
  topics: {
    save: (payload: TopicDefinition) => api.post<unknown>('/api/v1/topics', payload),
    list: (tenantId?: string) => api.get<unknown[]>(`/api/v1/topics${tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''}`),
    subscribe: (key: string, params: { subscriberId: string; channel?: string; address?: string; tenantId?: string }) => api.post<unknown>(`/api/v1/topics/${encodeURIComponent(key)}/subscribe?${new URLSearchParams(Object.fromEntries(Object.entries(params).filter(([,v]) => v !== undefined)))}`),
    unsubscribe: (key: string, subscriberId: string, tenantId?: string) => api.post<unknown>(`/api/v1/topics/${encodeURIComponent(key)}/unsubscribe?${new URLSearchParams({ subscriberId, ...(tenantId ? { tenantId } : {}) })}`),
    subscribers: (key: string, tenantId?: string) => api.get<unknown[]>(`/api/v1/topics/${encodeURIComponent(key)}/subscribers${tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : ''}`),
  },
  campaigns: {
    create: (payload: CreateCampaignRequest) => api.post<unknown>('/api/v1/campaigns', payload),
    recipients: (id: string, payload: AddRecipientsRequest) => api.post<unknown>(`/api/v1/campaigns/${encodeURIComponent(id)}/recipients`, payload),
    importCsv: (id: string, file: FormData) => api.post<unknown>(`/api/v1/campaigns/${encodeURIComponent(id)}/recipients/import`, file),
    send: (id: string) => api.post<unknown>(`/api/v1/campaigns/${encodeURIComponent(id)}/send`),
    cancel: (id: string) => api.post<unknown>(`/api/v1/campaigns/${encodeURIComponent(id)}/cancel`),
    get: (id: string) => api.get<unknown>(`/api/v1/campaigns/${encodeURIComponent(id)}`),
    progress: (id: string) => api.get<unknown>(`/api/v1/campaigns/${encodeURIComponent(id)}/progress`),
  },
  messagingHealth: () => api.get<unknown>('/api/v1/admin/messaging/health'),
  broadcasts: { send: (payload: BroadcastRequest) => api.post<unknown>('/api/v1/broadcasts', payload) },
}
