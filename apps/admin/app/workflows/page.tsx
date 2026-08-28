"use client";
import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { ResponsePanel } from "@/components/response-panel";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { endpoints, ApiResult } from "@/lib/api";
import { toast } from "sonner";

export default function WorkflowsPage() {
  const [defJson, setDefJson] = useState(JSON.stringify({
    key: "onboarding", isActive: true,
    steps: [
      { id: "s1", type: "send", channel: "email", templateKey: "welcome", next: "s2" },
      { id: "s2", type: "delay", delaySeconds: 60, next: null },
    ],
  }, null, 2));
  const [startJson, setStartJson] = useState(JSON.stringify({
    workflowKey: "onboarding", recipient: "user@example.com", data: { name: "Ada" },
  }, null, 2));
  const [runId, setRunId] = useState("");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader
        title="Workflows"
        description="Multi-step journeys (send → delay → branch). Save a definition, start a run for a recipient, inspect timeline or cancel."
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Definition & run</CardTitle>
            <CardDescription>JSON editors map 1:1 to OpenAPI bodies for demos and power users.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label>Workflow definition</Label>
              <Textarea rows={10} value={defJson} onChange={(e) => setDefJson(e.target.value)} />
            </div>
            <Button onClick={async () => {
              try {
                const r = await endpoints.saveWorkflow(JSON.parse(defJson));
                setResult(r);
                r.ok ? toast.success("Saved") : toast.error(r.error);
              } catch { toast.error("Invalid JSON"); }
            }}>Save workflow</Button>
            <div className="space-y-2">
              <Label>Start request</Label>
              <Textarea rows={5} value={startJson} onChange={(e) => setStartJson(e.target.value)} />
            </div>
            <Button variant="secondary" onClick={async () => {
              try {
                const r = await endpoints.startWorkflow(JSON.parse(startJson));
                setResult(r);
                if (r.ok) {
                  const id = (r.data as { runId?: string })?.runId;
                  if (id) setRunId(id);
                  toast.success("Run started");
                } else toast.error(r.error);
              } catch { toast.error("Invalid JSON"); }
            }}>Start run</Button>
            <div className="space-y-2">
              <Label>Run id</Label>
              <Input className="font-mono text-xs" value={runId} onChange={(e) => setRunId(e.target.value)} />
            </div>
            <div className="flex gap-2">
              <Button variant="outline" onClick={async () => setResult(await endpoints.getWorkflowRun(runId))}>Get</Button>
              <Button variant="outline" onClick={async () => setResult(await endpoints.getWorkflowTimeline(runId))}>Timeline</Button>
              <Button variant="destructive" onClick={async () => {
                const r = await endpoints.cancelWorkflow(runId);
                setResult(r);
                r.ok ? toast.message("Cancelled") : toast.error(r.error);
              }}>Cancel</Button>
            </div>
          </CardContent>
        </Card>
        <ResponsePanel result={result} />
      </div>
    </>
  );
}
