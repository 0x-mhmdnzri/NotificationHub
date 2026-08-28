"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, Select, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function EngagementPage() {
  const [notificationId, setNotificationId] = useState("");
  const [eventType, setEventType] = useState("open");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Engagement" subtitle="Track opens/clicks and view aggregate stats" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Notification id"><Input value={notificationId} onChange={(e) => setNotificationId(e.target.value)} /></Field>
          <Field label="Event type">
            <Select value={eventType} onChange={(e) => setEventType(e.target.value)}>
              {["open", "click", "unsubscribe", "bounce", "complaint"].map((e) => (
                <option key={e} value={e}>{e}</option>
              ))}
            </Select>
          </Field>
          <Button onClick={async () => setResult(await endpoints.trackEngagement({
            notificationId: notificationId || null, eventType, occurredAt: new Date().toISOString()
          }))}>Track event</Button>
          <Button variant="ghost" disabled={!notificationId} onClick={async () => setResult(await endpoints.listEngagement(notificationId))}>List for notification</Button>
          <Button variant="ghost" onClick={async () => setResult(await endpoints.engagementStats())}>Stats</Button>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
