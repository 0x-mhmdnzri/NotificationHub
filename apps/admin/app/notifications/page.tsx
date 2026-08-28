"use client";

import { useState } from "react";
import { PageHeader, Card, Field, Input, TextArea, Select, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function NotificationsPage() {
  const [recipient, setRecipient] = useState("user@example.com");
  const [channel, setChannel] = useState("email");
  const [templateKey, setTemplateKey] = useState("welcome");
  const [priority, setPriority] = useState("1");
  const [dataJson, setDataJson] = useState('{\n  "name": "Ada"\n}');
  const [lookupId, setLookupId] = useState("");
  const [result, setResult] = useState<ApiResult | null>(null);
  const [busy, setBusy] = useState(false);

  function buildBody() {
    let data: Record<string, unknown> = {};
    try {
      data = JSON.parse(dataJson || "{}");
    } catch {
      throw new Error("Data JSON is invalid");
    }
    return {
      recipient,
      channel,
      templateKey,
      priority: Number(priority),
      data,
      allowFallback: true,
    };
  }

  async function send(sync: boolean) {
    setBusy(true);
    try {
      const body = buildBody();
      const r = sync ? await endpoints.sendSync(body) : await endpoints.sendNotification(body);
      setResult(r);
      if (r.ok && r.data && typeof r.data === "object") {
        const d = r.data as { id?: string; notificationId?: string };
        const id = d.id || d.notificationId;
        if (id) setLookupId(String(id));
      }
    } catch (e) {
      setResult({ ok: false, status: 0, data: null, error: e instanceof Error ? e.message : "error" });
    }
    setBusy(false);
  }

  async function lookup() {
    if (!lookupId) return;
    setBusy(true);
    setResult(await endpoints.getStatus(lookupId));
    setBusy(false);
  }

  return (
    <>
      <PageHeader title="Notifications" subtitle="Send async/sync notifications and look up delivery status" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Recipient">
            <Input value={recipient} onChange={(e) => setRecipient(e.target.value)} />
          </Field>
          <Field label="Channel">
            <Select value={channel} onChange={(e) => setChannel(e.target.value)}>
              {["email", "sms", "push", "inapp", "chat"].map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </Select>
          </Field>
          <Field label="Template key">
            <Input value={templateKey} onChange={(e) => setTemplateKey(e.target.value)} />
          </Field>
          <Field label="Priority (0-3)">
            <Select value={priority} onChange={(e) => setPriority(e.target.value)}>
              <option value="0">0 Low</option>
              <option value="1">1 Normal</option>
              <option value="2">2 High</option>
              <option value="3">3 Critical</option>
            </Select>
          </Field>
          <Field label="Template data (JSON)">
            <TextArea rows={5} value={dataJson} onChange={(e) => setDataJson(e.target.value)} />
          </Field>
          <div className="flex gap-2">
            <Button disabled={busy} onClick={() => send(false)}>Send (queue)</Button>
            <Button variant="ghost" disabled={busy} onClick={() => send(true)}>Send sync</Button>
          </div>
        </Card>
        <Card className="space-y-3">
          <Field label="Notification id">
            <Input value={lookupId} onChange={(e) => setLookupId(e.target.value)} placeholder="uuid" />
          </Field>
          <Button variant="ghost" disabled={busy || !lookupId} onClick={lookup}>Get status</Button>
          <ResultBox result={result} />
        </Card>
      </div>
    </>
  );
}
