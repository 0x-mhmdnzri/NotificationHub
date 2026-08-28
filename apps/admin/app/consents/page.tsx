"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, Select, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function ConsentsPage() {
  const [subjectId, setSubjectId] = useState("user-1");
  const [purpose, setPurpose] = useState("marketing");
  const [channel, setChannel] = useState("email");
  const [granted, setGranted] = useState("true");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Consents" subtitle="Record and evaluate GDPR-style purpose consents" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Subject id"><Input value={subjectId} onChange={(e) => setSubjectId(e.target.value)} /></Field>
          <Field label="Purpose"><Input value={purpose} onChange={(e) => setPurpose(e.target.value)} /></Field>
          <Field label="Channel"><Input value={channel} onChange={(e) => setChannel(e.target.value)} /></Field>
          <Field label="Granted">
            <Select value={granted} onChange={(e) => setGranted(e.target.value)}>
              <option value="true">true</option>
              <option value="false">false</option>
            </Select>
          </Field>
          <Button onClick={async () => setResult(await endpoints.recordConsent({
            subjectId, purpose, channel, granted: granted === "true", source: "admin-panel"
          }))}>Record</Button>
          <Button variant="ghost" onClick={async () => setResult(await endpoints.listConsents(subjectId))}>List</Button>
          <Button variant="ghost" onClick={async () => setResult(await endpoints.evaluateConsent({ subjectId, purpose, channel }))}>Evaluate</Button>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
