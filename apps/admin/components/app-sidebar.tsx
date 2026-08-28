"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Bell,
  FileText,
  Megaphone,
  GitBranch,
  Filter,
  Hash,
  Smartphone,
  Settings2,
  ShieldCheck,
  Webhook,
  Activity,
  Puzzle,
  KeyRound,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { getApiBase, getApiKey } from "@/lib/api";
import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";

const NAV: { href: string; label: string; icon: LucideIcon; group: string }[] = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard, group: "Overview" },
  { href: "/notifications", label: "Notifications", icon: Bell, group: "Messaging" },
  { href: "/templates", label: "Templates", icon: FileText, group: "Messaging" },
  { href: "/campaigns", label: "Campaigns", icon: Megaphone, group: "Messaging" },
  { href: "/workflows", label: "Workflows", icon: GitBranch, group: "Messaging" },
  { href: "/segments", label: "Segments", icon: Filter, group: "Audience" },
  { href: "/topics", label: "Topics", icon: Hash, group: "Audience" },
  { href: "/devices", label: "Devices", icon: Smartphone, group: "Audience" },
  { href: "/preferences", label: "Preferences", icon: Settings2, group: "Compliance" },
  { href: "/consents", label: "Consents", icon: ShieldCheck, group: "Compliance" },
  { href: "/webhooks", label: "Webhooks", icon: Webhook, group: "Integrations" },
  { href: "/engagement", label: "Engagement", icon: Activity, group: "Integrations" },
  { href: "/plugins", label: "Plugins", icon: Puzzle, group: "System" },
  { href: "/settings", label: "Settings", icon: KeyRound, group: "System" },
];

export function AppSidebar() {
  const path = usePathname();
  const [base, setBase] = useState("");
  const [hasKey, setHasKey] = useState(false);

  useEffect(() => {
    setBase(getApiBase());
    setHasKey(!!getApiKey());
  }, [path]);

  const groups = [...new Set(NAV.map((n) => n.group))];

  return (
    <aside className="hidden md:flex w-64 shrink-0 flex-col border-r bg-card/40">
      <div className="px-5 py-5">
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground text-sm font-bold">
            N
          </div>
          <div>
            <div className="font-semibold text-sm leading-none">NotificationHub</div>
            <div className="text-[11px] text-muted-foreground mt-1">Product demo console</div>
          </div>
        </div>
      </div>
      <Separator />
      <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-5">
        {groups.map((g) => (
          <div key={g}>
            <div className="px-2 mb-1.5 text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
              {g}
            </div>
            <div className="space-y-0.5">
              {NAV.filter((n) => n.group === g).map((item) => {
                const Icon = item.icon;
                const active = path === item.href;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    className={cn(
                      "flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm transition-colors",
                      active
                        ? "bg-primary text-primary-foreground shadow-sm"
                        : "text-muted-foreground hover:bg-muted hover:text-foreground"
                    )}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    {item.label}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </nav>
      <div className="p-3 border-t space-y-2">
        <div className="flex items-center justify-between text-[11px]">
          <span className="text-muted-foreground">API</span>
          <Badge variant={hasKey ? "success" : "warning"} className="font-normal">
            {hasKey ? "Key set" : "No key"}
          </Badge>
        </div>
        <p className="text-[10px] text-muted-foreground truncate font-mono" title={base}>
          {base || "…"}
        </p>
      </div>
    </aside>
  );
}
