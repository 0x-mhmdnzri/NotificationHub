"use client";

import { useCallback, useEffect, useState } from "react";
import { ColumnDef } from "@tanstack/react-table";
import { PageHeader } from "@/components/page-header";
import { DataTable } from "@/components/data-table";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { endpoints, asArray, ApiResult } from "@/lib/api";
import { ResponsePanel } from "@/components/response-panel";
import { toast } from "sonner";
import { RefreshCw } from "lucide-react";

type Row = Record<string, unknown>;

const columns: ColumnDef<Row>[] = [
  {
    id: "name",
    header: "Name",
    accessorFn: (r) => String(r.name ?? r.Name ?? r.id ?? "—"),
    cell: ({ getValue }) => <span className="font-medium">{String(getValue())}</span>,
  },
  {
    id: "channel",
    header: "Channel",
    accessorFn: (r) => String(r.channel ?? r.Channel ?? "—"),
  },
  {
    id: "type",
    header: "Type",
    accessorFn: (r) => String(r.type ?? r.Type ?? r.kind ?? "plugin"),
  },
];

export default function PluginsPage() {
  const [rows, setRows] = useState<Row[]>([]);
  const [result, setResult] = useState<ApiResult | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setBusy(true);
    const r = await endpoints.listPlugins();
    setResult(r);
    if (r.ok) {
      setRows(asArray<Row>(r.data));
      toast.success("Plugins loaded");
    } else toast.error(r.error || "Failed");
    setBusy(false);
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <>
      <PageHeader
        title="Plugins"
        description="Microkernel channel adapters loaded by the Host at startup. This is the extension surface of the product."
        actions={
          <Button variant="outline" onClick={load} disabled={busy}>
            <RefreshCw className={`h-4 w-4 ${busy ? "animate-spin" : ""}`} />
            Refresh
          </Button>
        }
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Registered plugins</CardTitle>
            <CardDescription>GET /api/v1/plugins</CardDescription>
          </CardHeader>
          <CardContent>
            <DataTable columns={columns} data={rows} searchKey="name" emptyMessage="No plugins loaded." />
          </CardContent>
        </Card>
        <ResponsePanel result={result} />
      </div>
    </>
  );
}
