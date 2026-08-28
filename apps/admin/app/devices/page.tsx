"use client";
import { useState } from "react";
import { ColumnDef } from "@tanstack/react-table";
import { PageHeader } from "@/components/page-header";
import { DataTable } from "@/components/data-table";
import { ResponsePanel } from "@/components/response-panel";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { endpoints, asArray, ApiResult } from "@/lib/api";
import { toast } from "sonner";

type Row = Record<string, unknown>;
const columns: ColumnDef<Row>[] = [
  { id: "platform", header: "Platform", accessorFn: (r) => String(r.platform ?? r.Platform ?? "—") },
  { id: "token", header: "Token", accessorFn: (r) => String(r.token ?? r.Token ?? "—"),
    cell: ({ getValue }) => <span className="font-mono text-xs truncate max-w-[180px] block">{String(getValue())}</span> },
  { id: "locale", header: "Locale", accessorFn: (r) => String(r.locale ?? "—") },
];

export default function DevicesPage() {
  const [userId, setUserId] = useState("user-1");
  const [platform, setPlatform] = useState("ios");
  const [token, setToken] = useState("device-token-demo");
  const [rows, setRows] = useState<Row[]>([]);
  const [result, setResult] = useState<ApiResult | null>(null);

  async function list() {
    const r = await endpoints.listDevices(userId);
    setResult(r);
    if (r.ok) setRows(asArray<Row>(r.data));
    else toast.error(r.error);
  }

  return (
    <>
      <PageHeader title="Devices" description="Register push tokens per user so the hub can target mobile/web push channels." />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Register device</CardTitle>
            <CardDescription>POST /api/v1/devices then list by user.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2"><Label>User id</Label><Input value={userId} onChange={(e) => setUserId(e.target.value)} /></div>
            <div className="space-y-2">
              <Label>Platform</Label>
              <Select value={platform} onValueChange={setPlatform}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="ios">ios</SelectItem>
                  <SelectItem value="android">android</SelectItem>
                  <SelectItem value="web">web</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2"><Label>Token</Label><Input value={token} onChange={(e) => setToken(e.target.value)} /></div>
            <div className="flex gap-2">
              <Button onClick={async () => {
                const r = await endpoints.registerDevice({ userId, platform, token });
                setResult(r);
                if (r.ok) { toast.success("Registered"); list(); } else toast.error(r.error);
              }}>Register</Button>
              <Button variant="outline" onClick={list}>List devices</Button>
            </div>
          </CardContent>
        </Card>
        <div className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Devices for user</CardTitle></CardHeader>
            <CardContent><DataTable columns={columns} data={rows} emptyMessage="List a user to see tokens." /></CardContent>
          </Card>
          <ResponsePanel result={result} />
        </div>
      </div>
    </>
  );
}
