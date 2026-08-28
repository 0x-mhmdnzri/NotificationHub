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
  { id: "purpose", header: "Purpose", accessorFn: (r) => String(r.purpose ?? "—") },
  { id: "channel", header: "Channel", accessorFn: (r) => String(r.channel ?? "—") },
  { id: "granted", header: "Granted", accessorFn: (r) => String(r.granted ?? "—") },
  { id: "source", header: "Source", accessorFn: (r) => String(r.source ?? "—") },
];

export default function ConsentsPage() {
  const [subjectId, setSubjectId] = useState("user-1");
  const [purpose, setPurpose] = useState("marketing");
  const [channel, setChannel] = useState("email");
  const [granted, setGranted] = useState("true");
  const [rows, setRows] = useState<Row[]>([]);
  const [result, setResult] = useState<ApiResult | null>(null);

  async function list() {
    const r = await endpoints.listConsents(subjectId);
    setResult(r);
    if (r.ok) setRows(asArray<Row>(r.data));
  }

  return (
    <>
      <PageHeader title="Consents" description="Record and evaluate purpose-based consent before marketing sends (compliance path)." />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Consent actions</CardTitle>
            <CardDescription>Record → list → evaluate for a subject.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2"><Label>Subject</Label><Input value={subjectId} onChange={(e) => setSubjectId(e.target.value)} /></div>
            <div className="space-y-2"><Label>Purpose</Label><Input value={purpose} onChange={(e) => setPurpose(e.target.value)} /></div>
            <div className="space-y-2"><Label>Channel</Label><Input value={channel} onChange={(e) => setChannel(e.target.value)} /></div>
            <div className="space-y-2">
              <Label>Granted</Label>
              <Select value={granted} onValueChange={setGranted}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="true">true</SelectItem>
                  <SelectItem value="false">false</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button onClick={async () => {
                const r = await endpoints.recordConsent({
                  subjectId, purpose, channel, granted: granted === "true", source: "admin-panel",
                });
                setResult(r);
                if (r.ok) { toast.success("Recorded"); list(); } else toast.error(r.error);
              }}>Record</Button>
              <Button variant="outline" onClick={list}>List</Button>
              <Button variant="secondary" onClick={async () => {
                const r = await endpoints.evaluateConsent({ subjectId, purpose, channel });
                setResult(r);
                r.ok ? toast.success("Evaluated") : toast.error(r.error);
              }}>Evaluate</Button>
            </div>
          </CardContent>
        </Card>
        <div className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Consent history</CardTitle></CardHeader>
            <CardContent><DataTable columns={columns} data={rows} emptyMessage="List a subject to see history." /></CardContent>
          </Card>
          <ResponsePanel result={result} />
        </div>
      </div>
    </>
  );
}
