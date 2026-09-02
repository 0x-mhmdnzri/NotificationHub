'use client'

import { Bell, Command, Menu, Moon, Sun, Building2, LogOut } from 'lucide-react'
import { useState } from 'react'
import Link from 'next/link'
import { Button } from './ui/button'
import { useTenant } from '@/providers/tenant-provider'
import { useAuth } from '@/providers/auth-provider'

export function Topbar() {
  const [dark, setDark] = useState(false)
  const { tenantId, setTenantId } = useTenant()
  const { me, organizations, logout, isAuthenticated } = useAuth()

  const toggle = () => {
    document.documentElement.classList.toggle('dark')
    setDark(!dark)
  }

  const initials = (me?.user.displayName || me?.user.email || 'AN')
    .split(/[\s@]/)
    .filter(Boolean)
    .slice(0, 2)
    .map((s) => s[0]?.toUpperCase())
    .join('') || 'AN'

  return (
    <header className="sticky top-0 z-30 flex h-[72px] items-center gap-4 border-b glass px-5 lg:px-8">
      <Button variant="ghost" size="icon" className="lg:hidden">
        <Menu size={20} />
      </Button>
      <div className="hidden items-center gap-2 rounded-xl border bg-background/70 px-3 py-2 text-xs md:flex">
        <Building2 size={15} className="text-primary" />
        <select
          value={tenantId ?? ''}
          onChange={(e) => setTenantId(e.target.value)}
          className="max-w-[200px] bg-transparent font-medium outline-none"
          disabled={!organizations.length}
        >
          {!organizations.length && <option value="">سازمانی نیست</option>}
          {organizations.map((o) => (
            <option key={o.organizationId} value={o.organizationId}>
              {o.name}
            </option>
          ))}
        </select>
      </div>
      <div className="ms-auto flex items-center gap-2">
        <div className="hidden max-w-sm items-center gap-2 rounded-xl border bg-background/70 px-3 py-2 lg:flex">
          <Command size={14} className="text-muted-foreground" />
          <span className="text-xs text-muted-foreground">جستجوی سریع</span>
          <kbd className="ms-8 rounded border px-1.5 py-0.5 text-[10px] text-muted-foreground">⌘K</kbd>
        </div>
        <Button variant="ghost" size="icon" onClick={toggle}>
          {dark ? <Sun size={18} /> : <Moon size={18} />}
        </Button>
        <Button variant="ghost" size="icon" className="relative">
          <Bell size={18} />
          <span className="absolute end-2 top-2 h-1.5 w-1.5 rounded-full bg-primary" />
        </Button>
        <div className="ms-2 flex items-center gap-3 border-s ps-4">
          <div className="hidden text-start sm:block">
            <div className="text-sm font-medium">{me?.user.displayName || me?.user.email || 'مهمان'}</div>
            <div className="text-[11px] text-muted-foreground">
              {me?.roles?.[0] || (isAuthenticated ? 'عضو' : 'خارج‌شده')}
            </div>
          </div>
          <Link
            href="/account/sessions"
            className="grid h-9 w-9 place-items-center rounded-xl bg-gradient-to-br from-primary to-violet-400 text-xs font-bold text-white"
            title="Sessions"
          >
            {initials}
          </Link>
          {isAuthenticated && (
            <Button variant="ghost" size="icon" title="Logout" onClick={() => void logout()}>
              <LogOut size={16} />
            </Button>
          )}
        </div>
      </div>
    </header>
  )
}
