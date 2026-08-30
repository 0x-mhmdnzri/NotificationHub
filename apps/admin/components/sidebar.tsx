'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import {
  LayoutDashboard,
  Send,
  Workflow,
  FileText,
  Users,
  Smartphone,
  SlidersHorizontal,
  ShieldCheck,
  Radio,
  Webhook,
  BarChart3,
  PlugZap,
  Layers3,
  BellRing,
  ChevronRight,
  Building2,
  UserCog,
  Megaphone,
  Activity,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useAuth } from '@/providers/auth-provider'
import { Perm } from '@/lib/auth/permissions'

const groups = [
  {
    label: 'Overview',
    items: [['/dashboard', 'Overview', LayoutDashboard, null]] as const,
  },
  {
    label: 'Send',
    items: [
      ['/notifications', 'Send notification', BellRing, null],
      ['/broadcasts', 'Broadcast', Megaphone, null],
      ['/campaigns', 'Campaigns', Send, null],
    ] as const,
  },
  {
    label: 'Content',
    items: [
      ['/templates', 'Templates', FileText, null],
      ['/topics', 'Topics', Radio, null],
      ['/segments', 'Segments', Users, null],
    ] as const,
  },
  {
    label: 'Automation',
    items: [['/workflows', 'Workflows', Workflow, null]] as const,
  },
  {
    label: 'Audience',
    items: [
      ['/devices', 'Devices', Smartphone, null],
      ['/preferences', 'Preferences', SlidersHorizontal, null],
      ['/consents', 'Consents', ShieldCheck, null],
    ] as const,
  },
  {
    label: 'Analytics',
    items: [['/engagement', 'Engagement', BarChart3, null]] as const,
  },
  {
    label: 'Integrations',
    items: [
      ['/webhooks', 'Webhooks', Webhook, null],
      ['/plugins', 'Plugins', PlugZap, null],
    ] as const,
  },
  {
    label: 'Operations',
    items: [
      ['/notifications/status', 'Delivery status', Activity, null],
      ['/organization/members', 'Members', UserCog, Perm.MemberRead],
      ['/organization/settings', 'Organization', Building2, Perm.OrganizationRead],
      ['/account/sessions', 'Sessions', ShieldCheck, null],
    ] as const,
  },
]

export function Sidebar() {
  const path = usePathname()
  const { can, isAuthenticated } = useAuth()

  return (
    <aside className="fixed inset-y-0 left-0 z-40 hidden w-[270px] border-r bg-card lg:block">
      <div className="flex h-full flex-col">
        <div className="flex h-[72px] items-center gap-3 border-b px-6">
          <div className="grid h-9 w-9 place-items-center rounded-xl bg-primary text-primary-foreground shadow-lg shadow-primary/25">
            <Layers3 size={18} />
          </div>
          <div>
            <div className="font-bold tracking-tight">NotificationHub</div>
            <div className="text-[10px] uppercase tracking-[.2em] text-muted-foreground">Operations</div>
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
                    <ChevronRight size={14} className="opacity-0 transition group-hover:opacity-50" />
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
              <span className="text-xs font-medium">Systems healthy</span>
            </div>
            <p className="text-xs leading-5 text-muted-foreground">Messaging infrastructure is operating normally.</p>
          </div>
        </div>
      </div>
    </aside>
  )
}
