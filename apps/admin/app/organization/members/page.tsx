'use client'

import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '@/components/page-header'
import { SectionCard } from '@/components/section-card'
import { Button } from '@/components/ui/button'
import { RequirePermission } from '@/components/require-auth'
import { useAuth } from '@/providers/auth-provider'
import { identityApi } from '@/lib/api/identity'
import { Perm } from '@/lib/auth/permissions'

export default function MembersPage() {
  const { me, can } = useAuth()
  const orgId = me?.tenant?.id
  const qc = useQueryClient()
  const [email, setEmail] = useState('')
  const [roleName, setRoleName] = useState('Viewer')

  const members = useQuery({
    queryKey: ['org', orgId, 'members'],
    queryFn: () => identityApi.listMembers(orgId!),
    enabled: !!orgId,
  })

  const invite = useMutation({
    mutationFn: () => identityApi.invite({ email, roleName, organizationId: orgId }),
    onSuccess: () => {
      setEmail('')
      void qc.invalidateQueries({ queryKey: ['org', orgId, 'members'] })
    },
  })

  const setStatus = useMutation({
    mutationFn: ({ membershipId, status }: { membershipId: string; status: string }) =>
      identityApi.setMemberStatus(orgId!, membershipId, status),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['org', orgId, 'members'] }),
  })

  return (
    <div className="p-6 lg:p-8">
      <PageHeader title="Members" description="Invite teammates and manage organization roles." />
      <RequirePermission permission={Perm.MemberRead}>
        {!orgId && (
          <div className="mb-4 rounded-xl border p-4 text-sm text-muted-foreground">
            Select an organization from the top bar to manage members.
          </div>
        )}
        {orgId && can(Perm.MemberInvite) && (
          <SectionCard className="mb-6">
            <div className="mb-3 text-sm font-medium">Invite member</div>
            <div className="flex flex-col gap-3 sm:flex-row">
              <input
                className="flex-1 rounded-xl border bg-background px-3 py-2 text-sm"
                placeholder="email@company.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
              <select
                className="rounded-xl border bg-background px-3 py-2 text-sm"
                value={roleName}
                onChange={(e) => setRoleName(e.target.value)}
              >
                <option>Viewer</option>
                <option>Operator</option>
                <option>OrganizationAdmin</option>
              </select>
              <Button
                disabled={!email || invite.isPending}
                onClick={() => invite.mutate()}
              >
                Send invite
              </Button>
            </div>
            {invite.isError && (
              <p className="mt-2 text-xs text-destructive">Invite failed</p>
            )}
          </SectionCard>
        )}
        {orgId && (
          <SectionCard>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead className="text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="pb-3 pr-4">User</th>
                    <th className="pb-3 pr-4">Status</th>
                    <th className="pb-3 pr-4">Roles</th>
                    <th className="pb-3">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {(members.data ?? []).map((m) => (
                    <tr key={m.membershipId} className="border-t">
                      <td className="py-3 pr-4">
                        <div className="font-medium">{m.displayName || m.email}</div>
                        <div className="text-xs text-muted-foreground">{m.email}</div>
                      </td>
                      <td className="py-3 pr-4">{m.status}</td>
                      <td className="py-3 pr-4">{m.roles.join(', ') || '—'}</td>
                      <td className="py-3">
                        {can(Perm.MemberSuspend) && m.status === 'Active' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setStatus.mutate({ membershipId: m.membershipId, status: 'Suspended' })}
                          >
                            Suspend
                          </Button>
                        )}
                        {can(Perm.MemberSuspend) && m.status === 'Suspended' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setStatus.mutate({ membershipId: m.membershipId, status: 'Active' })}
                          >
                            Reactivate
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {members.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
            </div>
          </SectionCard>
        )}
      </RequirePermission>
    </div>
  )
}
