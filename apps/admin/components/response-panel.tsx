"use client";
import { ApiResult } from "@/lib/api";
import { formatJson } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { CheckCircle2, XCircle } from "lucide-react";

export function ResponsePanel({ result, title = "API response" }: { result: ApiResult | null; title?: string }) {
  if (!result) {
    return (
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">{title}</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">Run an action to see the live response from the Host API.</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={result.ok ? "border-emerald-500/30" : "border-destructive/40"}>
      <CardHeader className="pb-3 flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base flex items-center gap-2">
          {result.ok ? (
            <CheckCircle2 className="h-4 w-4 text-emerald-500" />
          ) : (
            <XCircle className="h-4 w-4 text-destructive" />
          )}
          {title}
        </CardTitle>
        <Badge variant={result.ok ? "success" : "destructive"}>HTTP {result.status || "—"}</Badge>
      </CardHeader>
      <CardContent>
        {!result.ok && result.error && (
          <p className="text-sm text-destructive mb-3 font-medium">{result.error}</p>
        )}
        <pre className="rounded-lg bg-muted/50 p-3 text-xs overflow-auto max-h-80 font-mono leading-relaxed">
          {formatJson(result.data)}
        </pre>
      </CardContent>
    </Card>
  );
}
