'use client'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(() => new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 15_000,
        gcTime: 5 * 60_000,
        retry: (failureCount, error: any) => {
          if (error?.status === 401 || error?.status === 403 || error?.status === 404) return false
          return failureCount < 2
        },
        refetchOnWindowFocus: false,
      },
      mutations: { retry: 0 },
    },
  }))

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}
