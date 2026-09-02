'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import {
  LayoutDashboard, Send, Workflow, FileText, Users, Smartphone, ShieldCheck, Radio,
  Webhook, BarChart3, PlugZap, Layers3, BellRing, ChevronLeft, Building2, UserCog,
  Megaphone, Activity, SlidersHorizontal,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useAuth } from '@/providers/auth-provider'
import { Perm } from '@/lib/auth/permissions'
import { useT } from '@/lib/i18n'

export function Sidebar() {
  const path = usePathname()
  const { can, isAuthenticated } = useAuth()
  const t = useT()

  const groups = [
    { label: t('navOverview'), items: [['/dashboard', t('overview'), LayoutDashboard, null]] as const },
    {
      label: t('navSend'),
      items: [
        ['/notifications', t('sendNotification'), BellRing, null],
        ['/broadcasts', t('broadcast'), Megaphone, null],
        ['/campaigns', t('campaigns'), Send, null],
      ] as const,
    },
    {
      label: t('navContent'),
      items: [
        ['/templates', t('templates'), FileText, null],
        ['/topics', t('topics'), Radio, null],
        ['/segments', t('segments'), Users, null],
      ] as const,
    },
    {
      label: t('navAutomation'),
      items: [
        ['/workflows', t('workflows'), Workflow, null],
        ['/workflows/live', t('deliveryFlow'), Activity, null],
      ] as const,
    },
    {
      label: t('navAudience'),
      items: [
        ['/devices', t('devices'), Smartphone, null],
        ['/preferences', t('preferences'), SlidersHorizontal, null],
        ['/consents', t('consents'), ShieldCheck, null],
      ] as const,
    },
    { label: t('navAnalytics'), items: [['/engagement', t('engagement'), BarChart3, null]] as const },
    {
      label: t('navIntegrations'),
      items: [
        ['/webhooks', t('webhooks'), Webhook, null],
        ['/plugins', t('plugins'), PlugZap, null],
      ] as const,
    },
    {
      label: t('navOperations'),
      items: [
        ['/notifications/status', t('deliveryStatus'), Activity, null],
        ['/organization/members', t('members'), UserCog, Perm.MemberRead],
        ['/organization/settings', t('organization'), Building2, Perm.OrganizationRead],
        ['/account/sessions', t('sessions'), ShieldCheck, null],
      ] as const,
    },
  ]

  return (
    <aside className="fixed inset-y-0 right-0 z-40 hidden w-[270px] border-l bg-card lg:block">
      <div className="flex h-full flex-col">
        <div className="flex h-[72px] items-center gap-3 border-b px-6">
          <div className="grid h-9 w-9 place-items-center rounded-xl bg-primary text-primary-foreground shadow-lg shadow-primary/25">
            <Layers3 size={18} />
          </div>
          <div>
            <div className="font-bold tracking-tight">{t('appName')}</div>
            <div className="text-[10px] uppercase tracking-[.2em] text-muted-foreground">{t('appTagline')}</div>
          </div>
        </div>
        <div className="flex-1 overflow-y-auto p-4">
          {groups.map((g) => {
            const items = g.items.filter(([, , , perm]) => !perm || !isAuthenticated || can(perm))
            if (!items.length) return null
            return (
              <div key={g.label} className="mb-6">
                <div className="mb-2 px-3 text-[10px] font-semibold uppercase tracking-[.18em] text-muted-foreground">
                  {g.label}
                </div>
                {items.map(([href, label, Icon]) => (
                  <Link
                    key={href}
                    href={href}
                    className={cn(
                      'group mb-1 flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-muted-foreground transition-all hover:bg-muted hover:text-foreground',
                      path === href || path.startsWith(href + '/')
                        ? 'bg-primary/10 font-medium text-primary shadow-sm'
                        : '',
                    )}
                  >
                    <Icon size={17} />
                    <span className="flex-1">{label}</span>
                    <ChevronLeft size={14} className="opacity-0 transition group-hover:opacity-50" />
                  </Link>
                ))}
              </div>
            )
          })}
        </div>
        <div className="border-t p-4">
          <div className="rounded-2xl bg-gradient-to-br from-primary/10 via-primary/5 to-transparent p-4">
            <div className="mb-2 flex items-center gap-2">
              <span className="h-2 w-2 animate-pulse rounded-full bg-emerald-500" />
              <span className="text-xs font-medium">{t('systemsHealthy')}</span>
            </div>
            <p className="text-xs leading-5 text-muted-foreground">{t('systemsHealthyDesc')}</p>
          </div>
        </div>
      </div>
    </aside>
  )
}
