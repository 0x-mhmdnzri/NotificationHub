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

export default function SegmentsPage() {
  const [key, setKey] = useState("active-users");
  const [rulesJson, setRulesJson] = useState(JSON.stringify([{ field: "plan", operator: "eq", value: "pro" }], null, 2));
  const [attrs, setAttrs] = useState(JSON.stringify({ plan: "pro" }, null, 2));
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Segments" description="Rule-based audiences for campaigns. Save rules, then test whether a profile matches." />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Segment rules</CardTitle>
            <CardDescription>field / operator / value — evaluated server-side.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2"><Label>Key</Label><Input value={key} onChange={(e) => setKey(e.target.value)} /></div>
            <div className="space-y-2"><Label>Rules JSON</Label><Textarea rows={6} value={rulesJson} onChange={(e) => setRulesJson(e.target.value)} /></div>
            <div className="flex gap-2">
              <Button onClick={async () => {
                const r = await endpoints.saveSegment({ key, rules: JSON.parse(rulesJson), matchAll: true });
                setResult(r); r.ok ? toast.success("Saved") : toast.error(r.error);
              }}>Save</Button>
              <Button variant="outline" onClick={async () => setResult(await endpoints.getSegment(key))}>Get</Button>
            </div>
            <div className="space-y-2"><Label>Match attributes</Label><Textarea rows={4} value={attrs} onChange={(e) => setAttrs(e.target.value)} /></div>
            <Button variant="secondary" onClick={async () => {
              const r = await endpoints.matchSegment(key, JSON.parse(attrs));
              setResult(r); r.ok ? toast.success("Matched") : toast.error(r.error);
            }}>Test match</Button>
          </CardContent>
        </Card>
        <ResponsePanel result={result} />
      </div>
    </>
  );
}
