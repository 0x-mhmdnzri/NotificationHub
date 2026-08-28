"use client";
import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { ResponsePanel } from "@/components/response-panel";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { endpoints, ApiResult } from "@/lib/api";
import { toast } from "sonner";

export default function PreferencesPage() {
  const [userId, setUserId] = useState("user-1");
  const [json, setJson] = useState(JSON.stringify({
    userId: "user-1",
    channelOptIn: { email: true, sms: false, push: true },
    preferredChannel: "email",
    maxPerDay: 20,
  }, null, 2));
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Preferences" description="Per-user channel opt-in, quiet hours, and daily caps — enforced before delivery." />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>User preference</CardTitle>
            <CardDescription>Load existing preference, edit JSON, save.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2"><Label>User id</Label><Input value={userId} onChange={(e) => setUserId(e.target.value)} /></div>
            <Button variant="outline" onClick={async () => {
              const r = await endpoints.getPreferences(userId);
              setResult(r);
              if (r.ok && r.data) setJson(JSON.stringify(r.data, null, 2));
              r.ok ? toast.success("Loaded") : toast.error(r.error);
            }}>Load</Button>
            <div className="space-y-2"><Label>JSON</Label><Textarea rows={12} value={json} onChange={(e) => setJson(e.target.value)} /></div>
            <Button onClick={async () => {
              const r = await endpoints.savePreferences(JSON.parse(json));
              setResult(r);
              r.ok ? toast.success("Saved") : toast.error(r.error);
            }}>Save</Button>
          </CardContent>
        </Card>
        <ResponsePanel result={result} />
      </div>
    </>
  );
}
