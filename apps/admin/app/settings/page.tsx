"use client";

import { useEffect, useState } from "react";
import { PageHeader, Card, Field, Input, Button } from "@/components/Shell";
import { getApiBase, getApiKey, setCredentials } from "@/lib/api";

export default function SettingsPage() {
  const [base, setBase] = useState("http://localhost:5245");
  const [key, setKey] = useState("");
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    setBase(getApiBase());
    setKey(getApiKey());
  }, []);

  function save() {
    setCredentials(base, key);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  }

  return (
    <>
      <PageHeader
        title="Settings"
        subtitle="Point the console at NotificationHub Host and set the Admin API key (X-Api-Key)"
      />
      <Card className="max-w-lg space-y-4">
        <Field label="API base URL">
          <Input value={base} onChange={(e) => setBase(e.target.value)} placeholder="http://localhost:5245" />
        </Field>
        <Field label="API key">
          <Input
            type="password"
            value={key}
            onChange={(e) => setKey(e.target.value)}
            placeholder="Bootstrap key from Host startup logs"
          />
        </Field>
        <p className="text-xs text-slate-500">
          Host bootstrap prints an API key on first run (ApiKeyBootstrapper). Stored in browser localStorage only.
        </p>
        <Button onClick={save}>{saved ? "Saved" : "Save credentials"}</Button>
      </Card>
    </>
  );
}
