'use client'
import { createContext, useContext, useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { getTenantId } from '@/lib/auth/session'

interface TenantContextValue { tenantId?: string; setTenantId:(value:string)=>void }
const TenantContext=createContext<TenantContextValue|null>(null)
export function TenantProvider({children}:{children:React.ReactNode}){const [tenantId,setTenantIdState]=useState<string|undefined>(()=>getTenantId());const queryClient=useQueryClient();const value=useMemo(()=>({tenantId,setTenantId(value:string){setTenantIdState(value);if(typeof window!=='undefined')window.localStorage.setItem('notificationhub.tenantId',value);void queryClient.invalidateQueries()} }),[tenantId,queryClient]);return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>}
export function useTenant(){const value=useContext(TenantContext);if(!value)throw new Error('useTenant must be used inside TenantProvider');return value}
