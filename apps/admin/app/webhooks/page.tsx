"use client";
import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { ResponsePanel } from "@/components/response-panel";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { endpoints, ApiResult } from "@/lib/api";
import { toast } from "sonner";

export default function WebhooksPage() {
  const [url, setUrl] = useState("https://webhook.site/your-id");
  const [secret, setSecret] = useState("whsec_demo");
  const [events, setEvents] = useState("sent,failed,delivered");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader
        title="Webhooks"
        description="Subscribe external systems to delivery lifecycle events. Prefer HTTPS endpoints; localhost may be rejected by Host validation."
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>New subscription</CardTitle>
            <CardDescription>POST /api/v1/webhooks</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2"><Label>URL</Label><Input value={url} onChange={(e) => setUrl(e.target.value)} /></div>
            <div className="space-y-2"><Label>Secret</Label><Input value={secret} onChange={(e) => setSecret(e.target.value)} /></div>
            <div className="space-y-2"><Label>Events</Label><Input value={events} onChange={(e) => setEvents(e.target.value)} /></div>
            <Button onClick={async () => {
              const r = await endpoints.createWebhook({
                url, secret,
                events: events.split(",").map((s) => s.trim()).filter(Boolean),
                isActive: true,
              });
              setResult(r);
              r.ok ? toast.success("Subscription created") : toast.error(r.error);
            }}>Create</Button>
          </CardContent>
        </Card>
        <ResponsePanel result={result} />
      </div>
    </>
  );
}
