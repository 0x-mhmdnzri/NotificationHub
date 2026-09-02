import { api } from './client'
import type { TemplateDefinition } from '@/types/api'
import { normalizePagedResult, type PagedRequest, type PagedResult } from '@/types/paging'

function query(params: Record<string, string | number | undefined | null>) {
  const q = new URLSearchParams()
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== '') q.set(k, String(v))
  })
  return q.toString() ? `?${q}` : ''
}

export type TemplateListItem = {
  key: string
  channel: string
  locale: string
  subject: string
  version: number
  isActive: boolean
  tenantId?: string | null
}

/** Backend now returns PagedResult; tolerate legacy bare arrays. */
function asTemplateList(raw: unknown): PagedResult<TemplateListItem> {
  if (Array.isArray(raw)) {
    return {
      items: raw as TemplateListItem[],
      page: 1,
      pageSize: raw.length || 20,
      totalCount: raw.length,
      totalPages: 1,
    }
  }
  if (raw && typeof raw === 'object') {
    const o = raw as Record<string, unknown>
    if (o.value && typeof o.value === 'object') return normalizePagedResult<TemplateListItem>(o.value)
    if (o.data && typeof o.data === 'object' && !Array.isArray(o.data)) {
      return normalizePagedResult<TemplateListItem>(o.data)
    }
  }
  return normalizePagedResult<TemplateListItem>(raw)
}

export const templatesApi = {
  save: (payload: TemplateDefinition) => api.post<TemplateDefinition>('/api/v1/templates', payload),

  list: async (
    params: { tenantId?: string; channel?: string } & PagedRequest = {},
  ): Promise<PagedResult<TemplateListItem>> => {
    const raw = await api.get<unknown>(
      `/api/v1/templates${query({
        tenantId: params.tenantId,
        channel: params.channel,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 100,
        sort: params.sort,
        order: params.order,
        search: params.search,
      })}`,
    )
    return asTemplateList(raw)
  },

  get: (key: string, params: { channel: string; locale?: string; tenantId?: string }) =>
    api.get<TemplateDefinition>(`/api/v1/templates/${encodeURIComponent(key)}${query(params)}`),

  delete: (key: string, params: { channel: string; locale?: string; tenantId?: string }) =>
    api.delete<void>(`/api/v1/templates/${encodeURIComponent(key)}${query(params)}`),

  preview: (payload: unknown) => api.post<unknown>('/api/v1/templates/preview', payload),
}
