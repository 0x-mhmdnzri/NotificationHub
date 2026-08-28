"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function TopicsPage() {
  const [key, setKey] = useState("product-updates");
  const [name, setName] = useState("Product updates");
  const [subscriberId, setSubscriberId] = useState("user-1");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Topics" subtitle="Pub/sub style topics and subscribers" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Key"><Input value={key} onChange={(e) => setKey(e.target.value)} /></Field>
          <Field label="Name"><Input value={name} onChange={(e) => setName(e.target.value)} /></Field>
          <Button onClick={async () => setResult(await endpoints.saveTopic({ key, name, isActive: true }))}>Save topic</Button>
          <Button variant="ghost" onClick={async () => setResult(await endpoints.listTopics())}>List topics</Button>
          <Field label="Subscriber id"><Input value={subscriberId} onChange={(e) => setSubscriberId(e.target.value)} /></Field>
          <div className="flex gap-2">
            <Button onClick={async () => setResult(await apiSubscribe(key, subscriberId))}>Subscribe</Button>
            <Button variant="ghost" onClick={async () => setResult(await endpoints.listTopics())}>Refresh</Button>
          </div>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}

async function apiSubscribe(key: string, subscriberId: string) {
  const { api } = await import("@/lib/api");
  return api(`/api/v1/topics/${encodeURIComponent(key)}/subscribe`, {
    method: "POST",
    query: { subscriberId, channel: "email", address: `${subscriberId}@example.com` },
  });
}
