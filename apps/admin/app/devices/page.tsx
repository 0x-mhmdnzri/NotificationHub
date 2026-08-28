"use client";
import { useState } from "react";
import { PageHeader, Card, Field, Input, Select, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function DevicesPage() {
  const [userId, setUserId] = useState("user-1");
  const [platform, setPlatform] = useState("ios");
  const [token, setToken] = useState("device-token-demo");
  const [result, setResult] = useState<ApiResult | null>(null);

  return (
    <>
      <PageHeader title="Devices" subtitle="Register push device tokens per user" />
      <div className="grid md:grid-cols-2 gap-4">
        <Card className="space-y-3">
          <Field label="User id"><Input value={userId} onChange={(e) => setUserId(e.target.value)} /></Field>
          <Field label="Platform">
            <Select value={platform} onChange={(e) => setPlatform(e.target.value)}>
              <option value="ios">ios</option>
              <option value="android">android</option>
              <option value="web">web</option>
            </Select>
          </Field>
          <Field label="Token"><Input value={token} onChange={(e) => setToken(e.target.value)} /></Field>
          <Button onClick={async () => setResult(await endpoints.registerDevice({ userId, platform, token }))}>Register</Button>
          <Button variant="ghost" onClick={async () => setResult(await endpoints.listDevices(userId))}>List</Button>
        </Card>
        <Card><ResultBox result={result} /></Card>
      </div>
    </>
  );
}
