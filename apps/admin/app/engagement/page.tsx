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
  { id: "eventType", header: "Event", accessorFn: (r) => String(r.eventType ?? "—") },
  { id: "occurredAt", header: "When", accessorFn: (r) => String(r.occurredAt ?? "—") },
  { id: "channel", header: "Channel", accessorFn: (r) => String(r.channel ?? "—") },
];

export default function EngagementPage() {
  const [notificationId, setNotificationId] = useState("");
  const [eventType, setEventType] = useState("open");
  const [rows, setRows] = useState<Row[]>([]);
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Engagement" description="Track opens and clicks, inspect per-notification history, and pull aggregate stats for the demo." />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Track & query</CardTitle>
            <CardDescription>POST engagement · GET by notification · stats</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2"><Label>Notification id</Label><Input className="font-mono text-xs" value={notificationId} onChange={(e) => setNotificationId(e.target.value)} /></div>
            <div className="space-y-2">
              <Label>Event type</Label>
              <Select value={eventType} onValueChange={setEventType}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {["open", "click", "unsubscribe", "bounce", "complaint"].map((e) => (
                    <SelectItem key={e} value={e}>{e}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button onClick={async () => {
                const r = await endpoints.trackEngagement({
                  notificationId: notificationId || null,
                  eventType,
                  occurredAt: new Date().toISOString(),
                });
                setResult(r);
                r.ok ? toast.success("Tracked") : toast.error(r.error);
              }}>Track</Button>
              <Button variant="outline" disabled={!notificationId} onClick={async () => {
                const r = await endpoints.listEngagement(notificationId);
                setResult(r);
                if (r.ok) setRows(asArray<Row>(r.data));
              }}>List</Button>
              <Button variant="secondary" onClick={async () => {
                const r = await endpoints.engagementStats();
                setResult(r);
                r.ok ? toast.success("Stats loaded") : toast.error(r.error);
              }}>Stats</Button>
            </div>
          </CardContent>
        </Card>
        <div className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Events</CardTitle></CardHeader>
            <CardContent><DataTable columns={columns} data={rows} emptyMessage="Track or list to populate." /></CardContent>
          </Card>
          <ResponsePanel result={result} />
        </div>
      </div>
    </>
  );
}
