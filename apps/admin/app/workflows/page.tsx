"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, TextArea, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function WorkflowsPage() {
  const [defJson, setDefJson] = useState(JSON.stringify({
    key: "onboarding",
    isActive: true,
    steps: [
      { id: "s1", type: "send", channel: "email", templateKey: "welcome", next: "s2" },
      { id: "s2", type: "delay", delaySeconds: 60, next: null }
    ]
  }, null, 2));
  const [startJson, setStartJson] = useState(JSON.stringify({
    workflowKey: "onboarding",
    recipient: "user@example.com",
    data: { name: "Ada" }
  }, null, 2));
  const [runId, setRunId] = useState("");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Workflows" subtitle="Save workflow definitions, start runs, inspect timeline" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Workflow definition JSON">
            <TextArea rows={10} value={defJson} onChange={(e) => setDefJson(e.target.value)} />
          </Field>
          <Button onClick={async () => setResult(await endpoints.saveWorkflow(JSON.parse(defJson)))}>Save workflow</Button>
          <Field label="Start request JSON">
            <TextArea rows={6} value={startJson} onChange={(e) => setStartJson(e.target.value)} />
          </Field>
          <Button onClick={async () => {
            const r = await endpoints.startWorkflow(JSON.parse(startJson));
            setResult(r);
            if (r.ok && r.data && typeof r.data === "object" && "runId" in (r.data as object))
              setRunId(String((r.data as { runId: string }).runId));
          }}>Start run</Button>
          <Field label="Run id"><Input value={runId} onChange={(e) => setRunId(e.target.value)} /></Field>
          <div className="flex gap-2">
            <Button variant="ghost" onClick={async () => setResult(await endpoints.getWorkflowRun(runId))}>Get run</Button>
            <Button variant="ghost" onClick={async () => setResult(await endpoints.getWorkflowTimeline(runId))}>Timeline</Button>
            <Button variant="danger" onClick={async () => setResult(await endpoints.cancelWorkflow(runId))}>Cancel</Button>
          </div>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
