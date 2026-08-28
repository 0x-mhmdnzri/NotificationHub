"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, TextArea, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function SegmentsPage() {
  const [key, setKey] = useState("active-users");
  const [rulesJson, setRulesJson] = useState(JSON.stringify([{ field: "plan", operator: "eq", value: "pro" }], null, 2));
  const [attrs, setAttrs] = useState(JSON.stringify({ plan: "pro" }, null, 2));
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Segments" subtitle="Define audience rules and test attribute matching" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Key"><Input value={key} onChange={(e) => setKey(e.target.value)} /></Field>
          <Field label="Rules JSON"><TextArea rows={6} value={rulesJson} onChange={(e) => setRulesJson(e.target.value)} /></Field>
          <Button onClick={async () => setResult(await endpoints.saveSegment({ key, rules: JSON.parse(rulesJson), matchAll: true }))}>Save</Button>
          <Button variant="ghost" onClick={async () => setResult(await endpoints.getSegment(key))}>Get</Button>
          <Field label="Match attributes JSON"><TextArea rows={4} value={attrs} onChange={(e) => setAttrs(e.target.value)} /></Field>
          <Button onClick={async () => setResult(await endpoints.matchSegment(key, JSON.parse(attrs)))}>Match</Button>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
