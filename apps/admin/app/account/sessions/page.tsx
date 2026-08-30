'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '@/components/page-header'
import { SectionCard } from '@/components/section-card'
import { Button } from '@/components/ui/button'
import { identityApi } from '@/lib/api/identity'

export default function SessionsPage() {
  const qc = useQueryClient()
  const sessions = useQuery({ queryKey: ['auth', 'sessions'], queryFn: () => identityApi.sessions() })

  const revoke = useMutation({
    mutationFn: (id: string) => identityApi.revokeSession(id),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['auth', 'sessions'] }),
  })

  const revokeAll = useMutation({
    mutationFn: () => identityApi.revokeAllSessions(),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['auth', 'sessions'] }),
  })

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Sessions"
        description="Active sign-ins across devices. Revoking invalidates refresh tokens."
      />
      <div className="mb-4 flex justify-end">
        <Button variant="ghost" onClick={() => revokeAll.mutate()} disabled={revokeAll.isPending}>
          Revoke all
        </Button>
      </div>
      <SectionCard title="Active sessions">
        <div className="space-y-3">
          {(sessions.data ?? []).map((s) => (
            <div key={s.id} className="flex flex-col gap-2 rounded-xl border p-4 sm:flex-row sm:items-center">
              <div className="flex-1 text-sm">
                <div className="font-medium">{s.userAgent || 'Unknown device'}</div>
                <div className="text-xs text-muted-foreground">
                  {s.ip || '—'} · last seen {new Date(s.lastSeenAt).toLocaleString()} ·{' '}
                  {s.isActive ? 'active' : 'revoked'}
                </div>
              </div>
              {s.isActive && (
                <Button size="sm" variant="ghost" onClick={() => revoke.mutate(s.id)}>
                  Revoke
                </Button>
              )}
            </div>
          ))}
          {sessions.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
          {!sessions.isLoading && !(sessions.data?.length) && (
            <p className="text-sm text-muted-foreground">No sessions recorded.</p>
          )}
        </div>
      </SectionCard>
    </div>
  )
}
