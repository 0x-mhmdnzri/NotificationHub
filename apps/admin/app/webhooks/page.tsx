"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function WebhooksPage() {
  const [url, setUrl] = useState("https://webhook.site/your-id");
  const [secret, setSecret] = useState("whsec_demo");
  const [events, setEvents] = useState("sent,failed,delivered");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Webhooks" subtitle="Subscribe to delivery lifecycle events (HTTPS only in production rules)" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="URL"><Input value={url} onChange={(e) => setUrl(e.target.value)} /></Field>
          <Field label="Secret"><Input value={secret} onChange={(e) => setSecret(e.target.value)} /></Field>
          <Field label="Events (comma-separated)"><Input value={events} onChange={(e) => setEvents(e.target.value)} /></Field>
          <Button onClick={async () => setResult(await endpoints.createWebhook({
            url, secret, events: events.split(",").map((s) => s.trim()).filter(Boolean), isActive: true
          }))}>Create subscription</Button>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
