"use client";

import { useState } from "react";
import { PageHeader, Card, Field, Input, TextArea, Select, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function TemplatesPage() {
  const [key, setKey] = useState("welcome");
  const [channel, setChannel] = useState("email");
  const [locale, setLocale] = useState("en");
  const [subject, setSubject] = useState("Welcome {{name}}");
  const [body, setBody] = useState("Hello {{name}}, welcome aboard!");
  const [htmlBody, setHtmlBody] = useState("<p>Hello <b>{{name}}</b></p>");
  const [result, setResult] = useState<ApiResult | null>(null);
  const [busy, setBusy] = useState(false);

  async function list() {
    setBusy(true);
    setResult(await endpoints.listTemplates({ channel }));
    setBusy(false);
  }

  async function save() {
    setBusy(true);
    setResult(
      await endpoints.saveTemplate({
        key, channel, locale, subject, body, htmlBody, version: 1, isActive: true,
      })
    );
    setBusy(false);
  }

  async function preview() {
    setBusy(true);
    setResult(
      await endpoints.previewTemplate({
        recipient: "preview@example.com",
        channel,
        templateKey: key,
        data: { name: "Ada" },
      })
    );
    setBusy(false);
  }

  async function remove() {
    setBusy(true);
    setResult(await endpoints.deleteTemplate(key, channel, locale));
    setBusy(false);
  }

  return (
    <>
      <PageHeader title="Templates" subtitle="Create, list, preview, and delete notification templates" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="Key"><Input value={key} onChange={(e) => setKey(e.target.value)} /></Field>
          <Field label="Channel">
            <Select value={channel} onChange={(e) => setChannel(e.target.value)}>
              {["email", "sms", "push", "inapp", "chat"].map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </Select>
          </Field>
          <Field label="Locale"><Input value={locale} onChange={(e) => setLocale(e.target.value)} /></Field>
          <Field label="Subject"><Input value={subject} onChange={(e) => setSubject(e.target.value)} /></Field>
          <Field label="Body"><TextArea rows={3} value={body} onChange={(e) => setBody(e.target.value)} /></Field>
          <Field label="HTML body"><TextArea rows={3} value={htmlBody} onChange={(e) => setHtmlBody(e.target.value)} /></Field>
          <div className="flex flex-wrap gap-2">
            <Button disabled={busy} onClick={save}>Save</Button>
            <Button variant="ghost" disabled={busy} onClick={list}>List</Button>
            <Button variant="ghost" disabled={busy} onClick={preview}>Preview</Button>
            <Button variant="danger" disabled={busy} onClick={remove}>Delete</Button>
          </div>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
