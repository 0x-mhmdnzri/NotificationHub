"use client";

import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { ResponsePanel } from "@/components/response-panel";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { endpoints, ApiResult } from "@/lib/api";
import { toast } from "sonner";
import { Loader2, Send, Search } from "lucide-react";

export default function NotificationsPage() {
  const [recipient, setRecipient] = useState("user@example.com");
  const [channel, setChannel] = useState("email");
  const [templateKey, setTemplateKey] = useState("welcome");
  const [priority, setPriority] = useState("1");
  const [dataJson, setDataJson] = useState('{\n  "name": "Ada"\n}');
  const [lookupId, setLookupId] = useState("");
  const [result, setResult] = useState<ApiResult | null>(null);
  const [busy, setBusy] = useState(false);

  function body() {
    return {
      recipient,
      channel,
      templateKey,
      priority: Number(priority),
      data: JSON.parse(dataJson || "{}"),
      allowFallback: true,
    };
  }

  async function send(sync: boolean) {
    setBusy(true);
    try {
      const r = sync ? await endpoints.sendSync(body()) : await endpoints.sendNotification(body());
      setResult(r);
      if (r.ok) {
        toast.success(sync ? "Sent synchronously" : "Queued for delivery");
        const d = r.data as { id?: string; notificationId?: string } | null;
        const id = d?.id || d?.notificationId;
        if (id) setLookupId(String(id));
      } else toast.error(r.error || "Send failed");
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Invalid JSON in data");
    }
    setBusy(false);
  }

  async function lookup() {
    if (!lookupId.trim()) return toast.error("Enter a notification id");
    setBusy(true);
    const r = await endpoints.getStatus(lookupId.trim());
    setResult(r);
    if (r.ok) toast.success("Status loaded");
    else toast.error(r.error || "Not found");
    setBusy(false);
  }

  return (
    <>
      <PageHeader
        title="Notifications"
        description="Primary product action: accept a message into the hub (async queue) or deliver synchronously, then track lifecycle status by id."
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Compose</CardTitle>
            <CardDescription>
              Uses POST /api/v1/notifications (outbox + workers) or /sync for immediate provider call in the request path.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label>Recipient</Label>
              <Input value={recipient} onChange={(e) => setRecipient(e.target.value)} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Channel</Label>
                <Select value={channel} onValueChange={setChannel}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {["email", "sms", "push", "inapp", "chat"].map((c) => (
                      <SelectItem key={c} value={c}>{c}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Priority</Label>
                <Select value={priority} onValueChange={setPriority}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="0">Low</SelectItem>
                    <SelectItem value="1">Normal</SelectItem>
                    <SelectItem value="2">High</SelectItem>
                    <SelectItem value="3">Critical</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-2">
              <Label>Template key</Label>
              <Input value={templateKey} onChange={(e) => setTemplateKey(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Template data (JSON)</Label>
              <Textarea rows={5} value={dataJson} onChange={(e) => setDataJson(e.target.value)} />
            </div>
            <div className="flex gap-2">
              <Button disabled={busy} onClick={() => send(false)}>
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                Queue send
              </Button>
              <Button variant="secondary" disabled={busy} onClick={() => send(true)}>
                Send sync
              </Button>
            </div>
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Track status</CardTitle>
              <CardDescription>GET /api/v1/notifications/{"{id}"} — filled automatically after a successful send when the API returns an id.</CardDescription>
            </CardHeader>
            <CardContent className="flex gap-2">
              <Input value={lookupId} onChange={(e) => setLookupId(e.target.value)} placeholder="uuid" className="font-mono text-sm" />
              <Button variant="outline" disabled={busy} onClick={lookup}>
                <Search className="h-4 w-4" />
                Lookup
              </Button>
            </CardContent>
          </Card>
          <ResponsePanel result={result} />
        </div>
      </div>
    </>
  );
}
