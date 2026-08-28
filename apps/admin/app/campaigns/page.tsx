"use client";

import { useState } from "react";
import { PageHeader } from "@/components/page-header";
import { ResponsePanel } from "@/components/response-panel";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { endpoints, ApiResult } from "@/lib/api";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";

const STEPS = ["Create", "Recipients", "Send", "Track"] as const;

export default function CampaignsPage() {
  const [step, setStep] = useState(0);
  const [name, setName] = useState("Spring promo");
  const [templateKey, setTemplateKey] = useState("welcome");
  const [channels, setChannels] = useState("email");
  const [campaignId, setCampaignId] = useState("");
  const [addresses, setAddresses] = useState("a@example.com\nb@example.com");
  const [result, setResult] = useState<ApiResult | null>(null);
  const [busy, setBusy] = useState(false);

  async function create() {
    setBusy(true);
    const r = await endpoints.createCampaign({
      name,
      templateKey,
      channels: channels.split(",").map((s) => s.trim()).filter(Boolean),
      data: { campaign: name },
    });
    setResult(r);
    if (r.ok) {
      const id = (r.data as { id?: string })?.id;
      if (id) {
        setCampaignId(id);
        setStep(1);
        toast.success("Campaign created");
      }
    } else toast.error(r.error || "Create failed");
    setBusy(false);
  }

  async function addRecipients() {
    if (!campaignId) return toast.error("Create a campaign first");
    setBusy(true);
    const r = await endpoints.addRecipients(campaignId, {
      addresses: addresses.split("\n").map((s) => s.trim()).filter(Boolean),
      channels: channels.split(",").map((s) => s.trim()).filter(Boolean),
    });
    setResult(r);
    if (r.ok) {
      setStep(2);
      toast.success("Recipients added");
    } else toast.error(r.error || "Failed");
    setBusy(false);
  }

  async function start() {
    if (!campaignId) return;
    setBusy(true);
    const r = await endpoints.startCampaign(campaignId);
    setResult(r);
    if (r.ok) {
      setStep(3);
      toast.success("Campaign started");
    } else toast.error(r.error || "Start failed");
    setBusy(false);
  }

  async function progress() {
    if (!campaignId) return;
    setBusy(true);
    const r = await endpoints.getCampaignProgress(campaignId);
    setResult(r);
    if (r.ok) toast.success("Progress updated");
    else toast.error(r.error || "Failed");
    setBusy(false);
  }

  return (
    <>
      <PageHeader
        title="Campaigns"
        description="Guided demo for batch broadcast: create → import recipients → send → watch progress. Mirrors how a marketer would operate the product."
      />
      <div className="flex flex-wrap gap-2 mb-6">
        {STEPS.map((s, i) => (
          <Badge key={s} variant={i === step ? "default" : i < step ? "success" : "secondary"}>
            {i + 1}. {s}
          </Badge>
        ))}
      </div>
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Campaign wizard</CardTitle>
            <CardDescription>Campaign id is stored after create so you can continue mid-flow.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Template key</Label>
              <Input value={templateKey} onChange={(e) => setTemplateKey(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Channels (comma-separated)</Label>
              <Input value={channels} onChange={(e) => setChannels(e.target.value)} />
            </div>
            <Button disabled={busy} onClick={create}>
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              1. Create
            </Button>
            <div className="space-y-2">
              <Label>Campaign id</Label>
              <Input className="font-mono text-xs" value={campaignId} onChange={(e) => setCampaignId(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Recipients (one per line)</Label>
              <Textarea rows={4} value={addresses} onChange={(e) => setAddresses(e.target.value)} />
            </div>
            <div className="flex flex-wrap gap-2">
              <Button disabled={busy || !campaignId} onClick={addRecipients}>2. Add recipients</Button>
              <Button disabled={busy || !campaignId} onClick={start}>3. Send</Button>
              <Button variant="outline" disabled={busy || !campaignId} onClick={progress}>4. Progress</Button>
              <Button
                variant="destructive"
                disabled={busy || !campaignId}
                onClick={async () => {
                  setBusy(true);
                  const r = await endpoints.cancelCampaign(campaignId);
                  setResult(r);
                  setBusy(false);
                  if (r.ok) toast.message("Cancelled");
                }}
              >
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
        <ResponsePanel result={result} />
      </div>
    </>
  );
}
