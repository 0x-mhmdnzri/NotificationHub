'use client'
import { useQuery } from '@tanstack/react-query'
import { notificationsApi } from '@/lib/api/notifications'
export function useNotificationStatus(id:string|undefined){return useQuery({queryKey:['notification-status',id],queryFn:()=>notificationsApi.getStatus(id!),enabled:!!id,refetchInterval:q=>{const s=String((q.state.data as any)?.status??'').toLowerCase();return ['delivered','failed','cancelled','rejected'].includes(s)?false:2500}})}
