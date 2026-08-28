"use client";
import { useCallback, useEffect, useState } from "react";
import { ColumnDef } from "@tanstack/react-table";
import { PageHeader } from "@/components/page-header";
import { DataTable } from "@/components/data-table";
import { ResponsePanel } from "@/components/response-panel";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api, endpoints, asArray, ApiResult } from "@/lib/api";
import { toast } from "sonner";
import { RefreshCw } from "lucide-react";

type Row = { key?: string; name?: string; isActive?: boolean; [k: string]: unknown };

const columns: ColumnDef<Row>[] = [
  { accessorKey: "key", header: "Key", cell: ({ row }) => <span className="font-medium">{row.original.key}</span> },
  { accessorKey: "name", header: "Name" },
  {
    accessorKey: "isActive",
    header: "Active",
    cell: ({ row }) => (row.original.isActive === false ? "off" : "on"),
  },
];

export default function TopicsPage() {
  const [key, setKey] = useState("product-updates");
  const [name, setName] = useState("Product updates");
  const [subscriberId, setSubscriberId] = useState("user-1");
  const [rows, setRows] = useState<Row[]>([]);
  const [result, setResult] = useState<ApiResult | null>(null);

  const load = useCallback(async () => {
    const r = await endpoints.listTopics();
    setResult(r);
    if (r.ok) setRows(asArray<Row>(r.data));
  }, []);

  useEffect(() => { load(); }, [load]);

  return (
    <>
      <PageHeader
        title="Topics"
        description="Pub/sub style distribution lists. Create a topic, subscribe users, list from the API."
        actions={<Button variant="outline" onClick={load}><RefreshCw className="h-4 w-4" />Refresh</Button>}
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Manage</CardTitle>
            <CardDescription>Create topic and subscribe a demo user.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2"><Label>Key</Label><Input value={key} onChange={(e) => setKey(e.target.value)} /></div>
            <div className="space-y-2"><Label>Name</Label><Input value={name} onChange={(e) => setName(e.target.value)} /></div>
            <Button onClick={async () => {
              const r = await endpoints.saveTopic({ key, name, isActive: true });
              setResult(r);
              if (r.ok) { toast.success("Topic saved"); load(); } else toast.error(r.error);
            }}>Save topic</Button>
            <div className="space-y-2"><Label>Subscriber id</Label><Input value={subscriberId} onChange={(e) => setSubscriberId(e.target.value)} /></div>
            <Button variant="secondary" onClick={async () => {
              const r = await api(`/api/v1/topics/${encodeURIComponent(key)}/subscribe`, {
                method: "POST",
                query: { subscriberId, channel: "email", address: `${subscriberId}@example.com` },
              });
              setResult(r);
              r.ok ? toast.success("Subscribed") : toast.error(r.error);
            }}>Subscribe</Button>
          </CardContent>
        </Card>
        <div className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Topics</CardTitle></CardHeader>
            <CardContent>
              <DataTable columns={columns} data={rows} searchKey="key" />
            </CardContent>
          </Card>
          <ResponsePanel result={result} />
        </div>
      </div>
    </>
  );
}
