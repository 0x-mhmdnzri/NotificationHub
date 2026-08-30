'use client'

import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '@/components/page-header'
import { SectionCard } from '@/components/section-card'
import { Button } from '@/components/ui/button'
import { RequirePermission } from '@/components/require-auth'
import { useAuth } from '@/providers/auth-provider'
import { identityApi } from '@/lib/api/identity'
import { Perm } from '@/lib/auth/permissions'

export default function OrgSettingsPage() {
  const { me } = useAuth()
  const orgId = me?.tenant?.id
  const qc = useQueryClient()
  const [name, setName] = useState('')

  const org = useQuery({
    queryKey: ['org', orgId],
    queryFn: () => identityApi.getOrganization(orgId!),
    enabled: !!orgId,
  })

  useEffect(() => {
    if (org.data?.name) setName(org.data.name)
  }, [org.data?.name])

  const save = useMutation({
    mutationFn: () => identityApi.updateOrganization(orgId!, { name }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['org', orgId] }),
  })

  return (
    <div className="p-6 lg:p-8">
      <PageHeader title="Organization" description="Organization profile and status." />
      <RequirePermission permission={Perm.OrganizationRead}>
        {!orgId && (
          <p className="text-sm text-muted-foreground">No active organization in session.</p>
        )}
        {org.data && (
          <SectionCard>
            <div className="grid gap-4 max-w-lg">
              <label className="text-sm">
                <span className="mb-1 block text-muted-foreground">Name</span>
                <input
                  className="w-full rounded-xl border bg-background px-3 py-2"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  disabled={!me?.permissions.includes(Perm.OrganizationUpdate)}
                />
              </label>
              <div className="text-sm">
                <span className="text-muted-foreground">Status: </span>
                {org.data.status}
              </div>
              <div className="text-sm">
                <span className="text-muted-foreground">Type: </span>
                {org.data.type}
              </div>
              {me?.permissions.includes(Perm.OrganizationUpdate) && (
                <Button disabled={save.isPending || name === org.data.name} onClick={() => save.mutate()}>
                  Save changes
                </Button>
              )}
            </div>
          </SectionCard>
        )}
      </RequirePermission>
    </div>
  )
}
