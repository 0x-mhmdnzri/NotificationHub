"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, TextArea, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function PreferencesPage() {
  const [userId, setUserId] = useState("user-1");
  const [json, setJson] = useState(JSON.stringify({
    userId: "user-1",
    channelOptIn: { email: true, sms: false, push: true },
    preferredChannel: "email",
    maxPerDay: 20
  }, null, 2));
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Preferences" subtitle="Per-user channel opt-in, quiet hours, caps" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="User id"><Input value={userId} onChange={(e) => setUserId(e.target.value)} /></Field>
          <Button variant="ghost" onClick={async () => setResult(await endpoints.getPreferences(userId))}>Load</Button>
          <Field label="Preference JSON"><TextArea rows={12} value={json} onChange={(e) => setJson(e.target.value)} /></Field>
          <Button onClick={async () => setResult(await endpoints.savePreferences(JSON.parse(json)))}>Save</Button>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
