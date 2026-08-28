"use client";

import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { DataTable } from "@/components/data-table";
import { endpoints, asArray, ApiResult } from "@/lib/api";
import { ColumnDef } from "@tanstack/react-table";
import { Activity, RefreshCw, Server, Wifi, Puzzle } from "lucide-react";
import { toast } from "sonner";
import Link from "next/link";

type PluginRow = { name?: string; channel?: string; id?: string; [k: string]: unknown };

const pluginColumns: ColumnDef<PluginRow>[] = [
  {
    accessorKey: "name",
    header: "Name",
    cell: ({ row }) => (
      <span className="font-medium">
        {String(row.original.name ?? row.original.id ?? row.original.channel ?? "—")}
      </span>
    ),
  },
  {
    id: "channel",
    header: "Channel",
    accessorFn: (r) => String(r.channel ?? r.Channel ?? "—"),
  },
  {
    id: "details",
    header: "Raw",
    cell: ({ row }) => (
      <span className="text-xs text-muted-foreground font-mono truncate max-w-[240px] block">
        {JSON.stringify(row.original).slice(0, 80)}
      </span>
    ),
  },
];

function StatusCard({
  title,
  result,
  icon: Icon,
}: {
  title: string;
  result: ApiResult | null;
  icon: React.ComponentType<{ className?: string }>;
}) {
  const ok = result?.ok;
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium">{title}</CardTitle>
        <Icon className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-bold">
          {!result ? "…" : ok ? "Healthy" : "Issue"}
        </div>
        <div className="mt-1">
          <Badge variant={!result ? "secondary" : ok ? "success" : "destructive"}>
            {!result ? "checking" : ok ? `HTTP ${result.status}` : result.error?.slice(0, 40) || "fail"}
          </Badge>
        </div>
      </CardContent>
    </Card>
  );
}

export default function DashboardPage() {
  const [live, setLive] = useState<ApiResult | null>(null);
  const [ready, setReady] = useState<ApiResult | null>(null);
  const [msg, setMsg] = useState<ApiResult | null>(null);
  const [plugins, setPlugins] = useState<ApiResult | null>(null);
  const [loading, setLoading] = useState(false);

  const refresh = useCallback(async () => {
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
    if (!l.ok && l.status === 0) toast.error("Cannot reach API — check Settings & Host");
    else toast.success("Dashboard refreshed");
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const pluginRows = asArray<PluginRow>(plugins?.data);

  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Live health of the NotificationHub Host: process liveness, dependency readiness, messaging stack, and loaded channel plugins."
        actions={
          <Button onClick={refresh} disabled={loading} variant="outline">
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            Refresh
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 mb-8">
        <StatusCard title="Liveness" result={live} icon={Activity} />
        <StatusCard title="Readiness" result={ready} icon={Server} />
        <StatusCard title="Messaging" result={msg} icon={Wifi} />
        <StatusCard title="Plugins API" result={plugins} icon={Puzzle} />
      </div>

      <div className="grid gap-6 lg:grid-cols-5">
        <Card className="lg:col-span-3">
          <CardHeader>
            <CardTitle>Channel plugins</CardTitle>
            <CardDescription>
              Microkernel extensions currently registered in the Host. Empty list usually means plugins failed to load or API key lacks permission.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <DataTable
              columns={pluginColumns}
              data={pluginRows}
              searchKey="name"
              searchPlaceholder="Filter plugins…"
              emptyMessage="No plugins returned. Open Plugins page or verify Host startup logs."
            />
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Quick actions</CardTitle>
            <CardDescription>Common demo flows for stakeholders.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-2">
            <Button asChild variant="secondary" className="justify-start">
              <Link href="/notifications">Send a notification</Link>
            </Button>
            <Button asChild variant="secondary" className="justify-start">
              <Link href="/templates">Manage templates</Link>
            </Button>
            <Button asChild variant="secondary" className="justify-start">
              <Link href="/campaigns">Run a campaign</Link>
            </Button>
            <Button asChild variant="outline" className="justify-start">
              <Link href="/settings">Configure API key</Link>
            </Button>
          </CardContent>
          <CardContent>
            <p className="text-xs text-muted-foreground leading-relaxed">
              Messaging payload (raw):
            </p>
            <pre className="mt-2 max-h-40 overflow-auto rounded-md bg-muted/50 p-2 text-[10px] font-mono">
              {msg?.data ? JSON.stringify(msg.data, null, 2) : "—"}
            </pre>
          </CardContent>
        </Card>
      </div>
    </>
  );
}
