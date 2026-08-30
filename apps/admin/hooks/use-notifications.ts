'use client'

import { useMutation, useQuery } from '@tanstack/react-query'
import { notificationsApi } from '@/lib/api/notifications'
import type { NotificationRequest } from '@/types/api'

export function useNotificationStatus(id: string) {
  return useQuery({
    queryKey: ['notifications', 'status', id],
    queryFn: () => notificationsApi.getStatus(id),
    enabled: Boolean(id),
    refetchInterval: query => {
      const status = query.state.data?.status?.toLowerCase()
      return status && ['delivered', 'failed', 'cancelled', 'completed'].includes(status) ? false : 3_000
    },
  })
}

export function useSendNotification() {
  return useMutation({ mutationFn: (payload: NotificationRequest) => notificationsApi.send(payload) })
}

export function useSendNotificationSync() {
  return useMutation({ mutationFn: (payload: NotificationRequest) => notificationsApi.sendSync(payload) })
}

export function usePreviewNotification() {
  return useMutation({ mutationFn: (payload: NotificationRequest) => notificationsApi.preview(payload) })
}
