"use client";
import { useEffect, useState } from "react";
import { PageHeader, Card, Button, ResultBox } from "@/components/Shell";
import { endpoints, ApiResult } from "@/lib/api";

export default function PluginsPage() {
  const [result, setResult] = useState<ApiResult | null>(null);

  async function load() {
    setResult(await endpoints.listPlugins());
  }

  useEffect(() => { load(); }, []);

  return (
    <>
      <PageHeader title="Plugins" subtitle="Channel plugins loaded by the Host (microkernel)" />
      <Button onClick={load} className="mb-4">Refresh</Button>
      <Card><ResultBox result={result} /></Card>
    </>
  );
}
