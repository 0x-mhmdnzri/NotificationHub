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
      <PageHeader title="اعضا" description="دعوت همکاران و مدیریت نقش‌های سازمان." />
      <RequirePermission permission={Perm.MemberRead}>
        {!orgId && (
          <div className="mb-4 rounded-xl border p-4 text-sm text-muted-foreground">
            برای مدیریت اعضا، از نوار بالا یک سازمان انتخاب کنید.
          </div>
        )}
        {orgId && can(Perm.MemberInvite) && (
          <SectionCard title="دعوت عضو" className="mb-6">
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
                <option value="Viewer">بیننده</option>
                <option value="Operator">اپراتور</option>
                <option value="OrganizationAdmin">مدیر سازمان</option>
              </select>
              <Button disabled={!email || invite.isPending} onClick={() => invite.mutate()}>
                ارسال دعوت
              </Button>
            </div>
            {invite.isError && <p className="mt-2 text-xs text-destructive">دعوت ناموفق بود</p>}
          </SectionCard>
        )}
        {orgId && (
          <SectionCard title="اعضای تیم">
            <div className="overflow-x-auto">
              <table className="w-full text-right text-sm">
                <thead className="text-xs text-muted-foreground">
                  <tr>
                    <th className="pb-3 pl-4">کاربر</th>
                    <th className="pb-3 pl-4">وضعیت</th>
                    <th className="pb-3 pl-4">نقش‌ها</th>
                    <th className="pb-3">اقدامات</th>
                  </tr>
                </thead>
                <tbody>
                  {(members.data ?? []).map((m) => (
                    <tr key={m.membershipId} className="border-t">
                      <td className="py-3 pl-4">
                        <div className="font-medium">{m.displayName || m.email}</div>
                        <div className="text-xs text-muted-foreground">{m.email}</div>
                      </td>
                      <td className="py-3 pl-4">{m.status === 'Active' ? 'فعال' : m.status === 'Suspended' ? 'تعلیق' : m.status}</td>
                      <td className="py-3 pl-4">{m.roles.join(', ') || '—'}</td>
                      <td className="py-3">
                        {can(Perm.MemberSuspend) && m.status === 'Active' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setStatus.mutate({ membershipId: m.membershipId, status: 'Suspended' })}
                          >
                            تعلیق
                          </Button>
                        )}
                        {can(Perm.MemberSuspend) && m.status === 'Suspended' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setStatus.mutate({ membershipId: m.membershipId, status: 'Active' })}
                          >
                            فعال‌سازی مجدد
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {members.isLoading && <p className="text-sm text-muted-foreground">در حال بارگذاری…</p>}
            </div>
          </SectionCard>
        )}
      </RequirePermission>
    </div>
  )
}
