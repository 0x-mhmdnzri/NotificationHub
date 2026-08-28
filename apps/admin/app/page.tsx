"use client";

import { useEffect, useState } from "react";
import { PageHeader, Card, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function DashboardPage() {
  const [live, setLive] = useState<ApiResult | null>(null);
  const [ready, setReady] = useState<ApiResult | null>(null);
  const [msg, setMsg] = useState<ApiResult | null>(null);
  const [plugins, setPlugins] = useState<ApiResult | null>(null);
  const [loading, setLoading] = useState(false);

  async function refresh() {
    setLoading(true);
    const [l, r, m, p] = await Promise.all([
      endpoints.healthLive(),
      endpoints.healthReady(),
      endpoints.messagingHealth(),
      endpoints.listPlugins(),
    ]);
    setLive(l);
    setReady(r);
    setMsg(m);
    setPlugins(p);
    setLoading(false);
  }

  useEffect(() => {
    refresh();
  }, []);

  const chip = (r: ApiResult | null, label: string) => (
    <div
      className={`rounded-lg border px-4 py-3 ${
        !r ? "border-slate-700" : r.ok ? "border-emerald-700/50 bg-emerald-950/20" : "border-amber-700/50 bg-amber-950/20"
      }`}
    >
      <div className="text-xs text-slate-400">{label}</div>
      <div className="text-lg font-semibold mt-1">
        {!r ? "…" : r.ok ? "OK" : `Fail (${r.status || "net"})`}
      </div>
    </div>
  );

  return (
    <>
      <PageHeader
        title="Dashboard"
        subtitle="Health of Host API, messaging stack, and loaded channel plugins"
      />
      <div className="flex gap-3 mb-6">
        <Button onClick={refresh} disabled={loading}>
          {loading ? "Refreshing…" : "Refresh"}
        </Button>
      </div>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-6">
        {chip(live, "Live")}
        {chip(ready, "Ready")}
        {chip(msg, "Messaging")}
        {chip(plugins, "Plugins")}
      </div>
      <div className="grid md:grid-cols-2 gap-4">
        <Card>
          <h2 className="text-sm font-medium text-slate-300 mb-2">Messaging health</h2>
          <ResultBox result={msg} />
        </Card>
        <Card>
          <h2 className="text-sm font-medium text-slate-300 mb-2">Plugins</h2>
          <ResultBox result={plugins} />
        </Card>
      </div>
    </>
  );
}
