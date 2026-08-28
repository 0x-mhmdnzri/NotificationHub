"use client";

import { useState } from "react";
import { PageHeader, Card, Field, Input, TextArea, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function CampaignsPage() {
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
    if (r.ok && r.data && typeof r.data === "object") {
      const id = (r.data as { id?: string }).id;
      if (id) setCampaignId(id);
    }
    setBusy(false);
  }

  async function addRecipients() {
    if (!campaignId) return;
    setBusy(true);
    setResult(
      await endpoints.addRecipients(campaignId, {
        addresses: addresses.split("\n").map((s) => s.trim()).filter(Boolean),
        channels: channels.split(",").map((s) => s.trim()).filter(Boolean),
      })
    );
    setBusy(false);
  }

  async function start() {
    if (!campaignId) return;
    setBusy(true);
    setResult(await endpoints.startCampaign(campaignId));
    setBusy(false);
  }

  async function progress() {
    if (!campaignId) return;
    setBusy(true);
    setResult(await endpoints.getCampaignProgress(campaignId));
    setBusy(false);
  }

  async function cancel() {
    if (!campaignId) return;
    setBusy(true);
    setResult(await endpoints.cancelCampaign(campaignId));
    setBusy(false);
  }

  return (
    <>
      <PageHeader title="Campaigns" subtitle="Create broadcast campaigns, add recipients, send, track progress" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Name"><Input value={name} onChange={(e) => setName(e.target.value)} /></Field>
          <Field label="Template key"><Input value={templateKey} onChange={(e) => setTemplateKey(e.target.value)} /></Field>
          <Field label="Channels (comma-separated)">
            <Input value={channels} onChange={(e) => setChannels(e.target.value)} />
          </Field>
          <Button disabled={busy} onClick={create}>1. Create campaign</Button>
          <Field label="Campaign id">
            <Input value={campaignId} onChange={(e) => setCampaignId(e.target.value)} />
          </Field>
          <Field label="Recipient addresses (one per line)">
            <TextArea rows={4} value={addresses} onChange={(e) => setAddresses(e.target.value)} />
          </Field>
          <div className="flex flex-wrap gap-2">
            <Button disabled={busy || !campaignId} onClick={addRecipients}>2. Add recipients</Button>
            <Button disabled={busy || !campaignId} onClick={start}>3. Send</Button>
            <Button variant="ghost" disabled={busy || !campaignId} onClick={progress}>Progress</Button>
            <Button variant="danger" disabled={busy || !campaignId} onClick={cancel}>Cancel</Button>
          </div>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
