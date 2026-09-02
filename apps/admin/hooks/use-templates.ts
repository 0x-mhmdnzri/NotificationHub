'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { templatesApi } from '@/lib/api/templates'
import { useTenant } from '@/providers/tenant-provider'
import type { TemplateDefinition } from '@/types/api'
import type { PagedRequest } from '@/types/paging'

/** Always returns an array of templates (extracts items from PagedResult). */
export function useTemplates(channel?: string) {
  const { tenantId } = useTenant()
  return useQuery({
    queryKey: ['templates', tenantId, channel],
    queryFn: () => templatesApi.list({ tenantId, channel, page: 1, pageSize: 100 }),
    staleTime: 30_000,
    select: (r) => (Array.isArray(r) ? r : (r?.items ?? [])),
  })
}

export function useTemplatesPaged(params: { channel?: string } & PagedRequest) {
  const { tenantId } = useTenant()
  return useQuery({
    queryKey: [
      'templates',
      'paged',
      tenantId,
      params.channel,
      params.page,
      params.pageSize,
      params.sort,
      params.order,
      params.search,
    ],
    queryFn: () =>
      templatesApi.list({
        tenantId,
        channel: params.channel,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
        sort: params.sort,
        order: params.order,
        search: params.search,
      }),
    staleTime: 20_000,
    placeholderData: (prev) => prev,
  })
}

export function useSaveTemplate() {
  const { tenantId } = useTenant()
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: TemplateDefinition) =>
      templatesApi.save({ ...payload, tenantId: payload.tenantId ?? tenantId }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }),
  })
}

export function useDeleteTemplate() {
  const { tenantId } = useTenant()
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: { key: string; channel: string; locale?: string }) =>
      templatesApi.delete(input.key, { ...input, tenantId }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }),
  })
}
