import { api } from './client'
import type { NotificationRequest, NotificationStatus } from '@/types/api'
export const notificationsApi={
 send:(payload:NotificationRequest)=>api.post<unknown>('/api/v1/notifications',payload),
 sendSync:(payload:NotificationRequest)=>api.post<unknown>('/api/v1/notifications/sync',payload),
 getStatus:(id:string)=>api.get<NotificationStatus>(`/api/v1/notifications/${encodeURIComponent(id)}`),
 preview:(payload:NotificationRequest)=>api.post<unknown>('/api/v1/templates/preview',payload),
}
