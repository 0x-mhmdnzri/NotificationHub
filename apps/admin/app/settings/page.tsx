"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getApiBase, getApiKey, setCredentials, endpoints } from "@/lib/api";
import { toast } from "sonner";
import { CheckCircle2, Loader2 } from "lucide-react";

export default function SettingsPage() {
  const [base, setBase] = useState("http://localhost:5245");
  const [key, setKey] = useState("");
  const [testing, setTesting] = useState(false);

  useEffect(() => {
    setBase(getApiBase());
    setKey(getApiKey());
  }, []);

  function save() {
    setCredentials(base, key);
    toast.success("Credentials saved to this browser");
  }

  async function test() {
    setCredentials(base, key);
    setTesting(true);
    const r = await endpoints.healthLive();
    setTesting(false);
    if (r.ok) toast.success(`Connected — HTTP ${r.status}`);
    else toast.error(r.error || "Connection failed");
  }

  return (
    <>
      <PageHeader
        title="Settings"
        description="Connect this demo console to a running NotificationHub Host. The API key is sent as X-Api-Key on every request and stays in localStorage only."
      />
      <Card className="max-w-lg">
        <CardHeader>
          <CardTitle>API connection</CardTitle>
          <CardDescription>
            Default local Host is http://localhost:5245. Copy the bootstrap key printed when the Host starts for the first time.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="base">Base URL</Label>
            <Input id="base" value={base} onChange={(e) => setBase(e.target.value)} placeholder="http://localhost:5245" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="key">API key</Label>
            <Input
              id="key"
              type="password"
              value={key}
              onChange={(e) => setKey(e.target.value)}
              placeholder="nh_…"
              autoComplete="off"
            />
          </div>
          <div className="flex gap-2">
            <Button onClick={save}>
              <CheckCircle2 className="h-4 w-4" />
              Save
            </Button>
            <Button variant="outline" onClick={test} disabled={testing}>
              {testing ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              Test connection
            </Button>
          </div>
        </CardContent>
      </Card>
    </>
  );
}
