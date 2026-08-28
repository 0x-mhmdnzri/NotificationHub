"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ReactNode, useEffect, useState } from "react";
import { getApiBase, getApiKey, setCredentials } from "@/lib/api";
import clsx from "clsx";

const NAV = [
  { href: "/", label: "Dashboard", icon: "◈" },
  { href: "/notifications", label: "Notifications", icon: "✉" },
  { href: "/templates", label: "Templates", icon: "📄" },
  { href: "/campaigns", label: "Campaigns", icon: "📢" },
  { href: "/workflows", label: "Workflows", icon: "⟳" },
  { href: "/segments", label: "Segments", icon: "◎" },
  { href: "/topics", label: "Topics", icon: "＃" },
  { href: "/devices", label: "Devices", icon: "📱" },
  { href: "/preferences", label: "Preferences", icon: "⚙" },
  { href: "/consents", label: "Consents", icon: "✓" },
  { href: "/webhooks", label: "Webhooks", icon: "↗" },
  { href: "/engagement", label: "Engagement", icon: "◉" },
  { href: "/plugins", label: "Plugins", icon: "⬡" },
  { href: "/settings", label: "Settings", icon: "🔑" },
];

export function Shell({ children }: { children: ReactNode }) {
  const path = usePathname();
  const [base, setBase] = useState("");
  const [keyHint, setKeyHint] = useState("");

  useEffect(() => {
    setBase(getApiBase());
    const k = getApiKey();
    setKeyHint(k ? `${k.slice(0, 6)}…` : "not set");
  }, [path]);

  return (
    <div className="min-h-screen flex bg-slate-950 text-slate-100">
      <aside className="w-60 shrink-0 border-r border-slate-800 bg-slate-900/80 flex flex-col">
        <div className="px-4 py-5 border-b border-slate-800">
          <div className="text-lg font-semibold tracking-tight text-white">NotificationHub</div>
          <div className="text-xs text-slate-400 mt-0.5">Admin Console</div>
        </div>
        <nav className="flex-1 overflow-y-auto py-3 px-2 space-y-0.5">
          {NAV.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={clsx(
                "flex items-center gap-2 rounded-lg px-3 py-2 text-sm transition",
                path === item.href
                  ? "bg-brand-600/20 text-brand-100 border border-brand-600/30"
                  : "text-slate-300 hover:bg-slate-800 hover:text-white"
              )}
            >
              <span className="opacity-70 w-5 text-center">{item.icon}</span>
              {item.label}
            </Link>
          ))}
        </nav>
        <div className="p-3 border-t border-slate-800 text-[11px] text-slate-500 space-y-1">
          <div className="truncate">API: {base || "…"}</div>
          <div>Key: {keyHint}</div>
        </div>
      </aside>
      <main className="flex-1 overflow-auto">
        <div className="max-w-5xl mx-auto px-6 py-8">{children}</div>
      </main>
    </div>
  );
}

export function PageHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div className="mb-6">
      <h1 className="text-2xl font-semibold text-white tracking-tight">{title}</h1>
      {subtitle && <p className="text-slate-400 text-sm mt-1">{subtitle}</p>}
    </div>
  );
}

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={clsx("rounded-xl border border-slate-800 bg-slate-900/60 p-5 shadow-sm", className)}>
      {children}
    </div>
  );
}

export function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <label className="block space-y-1.5">
      <span className="text-xs font-medium text-slate-400 uppercase tracking-wide">{label}</span>
      {children}
    </label>
  );
}

export function Input(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={clsx(
        "w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100",
        "placeholder:text-slate-600 focus:outline-none focus:ring-2 focus:ring-brand-500/40 focus:border-brand-500",
        props.className
      )}
    />
  );
}

export function TextArea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={clsx(
        "w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 font-mono",
        "placeholder:text-slate-600 focus:outline-none focus:ring-2 focus:ring-brand-500/40 focus:border-brand-500",
        props.className
      )}
    />
  );
}

export function Select(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className={clsx(
        "w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100",
        "focus:outline-none focus:ring-2 focus:ring-brand-500/40 focus:border-brand-500",
        props.className
      )}
    />
  );
}

export function Button({
  children,
  variant = "primary",
  className,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "ghost" | "danger" }) {
  return (
    <button
      {...props}
      className={clsx(
        "inline-flex items-center justify-center rounded-lg px-4 py-2 text-sm font-medium transition disabled:opacity-50",
        variant === "primary" && "bg-brand-600 hover:bg-brand-500 text-white",
        variant === "ghost" && "border border-slate-700 hover:bg-slate-800 text-slate-200",
        variant === "danger" && "bg-red-600/90 hover:bg-red-500 text-white",
        className
      )}
    >
      {children}
    </button>
  );
}

export function ResultBox({ result }: { result: { ok: boolean; status: number; data: unknown; error?: string } | null }) {
  if (!result) return null;
  return (
    <pre
      className={clsx(
        "mt-4 rounded-lg border p-4 text-xs overflow-auto max-h-96 font-mono",
        result.ok ? "border-emerald-800/50 bg-emerald-950/30 text-emerald-100" : "border-red-800/50 bg-red-950/30 text-red-100"
      )}
    >
      {result.ok
        ? JSON.stringify(result.data, null, 2)
        : `HTTP ${result.status}\n${result.error}\n\n${JSON.stringify(result.data, null, 2)}`}
    </pre>
  );
}
