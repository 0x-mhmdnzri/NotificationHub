'use client'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { templatesApi } from '@/lib/api/templates'
import { useTenant } from '@/providers/tenant-provider'
import type { TemplateDefinition } from '@/types/api'

export function useTemplates(channel?: string) {
  const { tenantId } = useTenant()
  return useQuery({ queryKey: ['templates', tenantId, channel], queryFn: () => templatesApi.list({ tenantId, channel }), staleTime: 30_000 })
}

export function useSaveTemplate() {
  const { tenantId } = useTenant(); const qc = useQueryClient()
  return useMutation({ mutationFn: (payload: TemplateDefinition) => templatesApi.save({ ...payload, tenantId: payload.tenantId ?? tenantId }), onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }) })
}

export function useDeleteTemplate() {
  const { tenantId } = useTenant(); const qc = useQueryClient()
  return useMutation({ mutationFn: (input: { key: string; channel: string; locale?: string }) => templatesApi.delete(input.key, { ...input, tenantId }), onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }) })
}
