import { api } from './client'
import type { TemplateDefinition } from '@/types/api'

function query(params: Record<string, string | undefined>) {
  const q = new URLSearchParams()
  Object.entries(params).forEach(([k, v]) => { if (v) q.set(k, v) })
  return q.toString() ? `?${q}` : ''
}

export const templatesApi = {
  save: (payload: TemplateDefinition) => api.post<TemplateDefinition>('/api/v1/templates', payload),
  list: (params: { tenantId?: string; channel?: string } = {}) => api.get<TemplateDefinition[]>(`/api/v1/templates${query(params)}`),
  get: (key: string, params: { channel: string; locale?: string; tenantId?: string }) => api.get<TemplateDefinition>(`/api/v1/templates/${encodeURIComponent(key)}${query(params)}`),
  delete: (key: string, params: { channel: string; locale?: string; tenantId?: string }) => api.delete<void>(`/api/v1/templates/${encodeURIComponent(key)}${query(params)}`),
  preview: (payload: unknown) => api.post<unknown>('/api/v1/templates/preview', payload),
}
