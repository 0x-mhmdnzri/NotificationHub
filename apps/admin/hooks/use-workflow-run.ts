'use client'
import { useQuery } from '@tanstack/react-query'
import { resourcesApi } from '@/lib/api/resources'

export function useWorkflowRun(runId?: string) {
  return useQuery({
    queryKey: ['workflow-run', runId],
    queryFn: () => resourcesApi.workflows.getRun(runId!),
    enabled: Boolean(runId),
    refetchInterval: 2500,
  })
}

export function useWorkflowTimeline(runId?: string) {
  return useQuery({
    queryKey: ['workflow-timeline', runId],
    queryFn: () => resourcesApi.workflows.timeline(runId!),
    enabled: Boolean(runId),
    refetchInterval: 2500,
  })
}
