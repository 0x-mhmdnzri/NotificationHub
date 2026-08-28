"use client";

import { useCallback, useEffect, useState } from "react";
import { ColumnDef } from "@tanstack/react-table";
import { PageHeader } from "@/components/page-header";
import { ResponsePanel } from "@/components/response-panel";
import { DataTable } from "@/components/data-table";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { endpoints, asArray, ApiResult } from "@/lib/api";
import { toast } from "sonner";
import { Loader2, RefreshCw, Save, Eye, Trash2 } from "lucide-react";

type TemplateRow = {
  key?: string;
  channel?: string;
  locale?: string;
  subject?: string;
  isActive?: boolean;
  version?: number;
  [k: string]: unknown;
};

const columns: ColumnDef<TemplateRow>[] = [
  { accessorKey: "key", header: "Key", cell: ({ row }) => <span className="font-medium">{row.original.key}</span> },
  { accessorKey: "channel", header: "Channel" },
  { accessorKey: "locale", header: "Locale" },
  {
    accessorKey: "subject",
    header: "Subject",
    cell: ({ row }) => <span className="truncate max-w-[200px] block">{row.original.subject}</span>,
  },
  {
    accessorKey: "isActive",
    header: "Active",
    cell: ({ row }) => (
      <Badge variant={row.original.isActive === false ? "secondary" : "success"}>
        {row.original.isActive === false ? "off" : "on"}
      </Badge>
    ),
  },
  { accessorKey: "version", header: "Ver" },
];

export default function TemplatesPage() {
  const [key, setKey] = useState("welcome");
  const [channel, setChannel] = useState("email");
  const [locale, setLocale] = useState("en");
  const [subject, setSubject] = useState("Welcome {{name}}");
  const [body, setBody] = useState("Hello {{name}}, welcome aboard!");
  const [htmlBody, setHtmlBody] = useState("<p>Hello <b>{{name}}</b></p>");
  const [rows, setRows] = useState<TemplateRow[]>([]);
  const [result, setResult] = useState<ApiResult | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setBusy(true);
    const r = await endpoints.listTemplates({ channel });
    setResult(r);
    if (r.ok) {
      setRows(asArray<TemplateRow>(r.data));
      toast.success("Templates loaded");
    } else toast.error(r.error || "Failed to list");
    setBusy(false);
  }, [channel]);

  useEffect(() => {
    load();
  }, [load]);

  async function save() {
    setBusy(true);
    const r = await endpoints.saveTemplate({
      key, channel, locale, subject, body, htmlBody, version: 1, isActive: true,
    });
    setResult(r);
    if (r.ok) {
      toast.success("Template saved");
      await load();
    } else toast.error(r.error || "Save failed");
    setBusy(false);
  }

  async function preview() {
    setBusy(true);
    const r = await endpoints.previewTemplate({
      recipient: "preview@example.com",
      channel,
      templateKey: key,
      data: { name: "Ada" },
    });
    setResult(r);
    if (r.ok) toast.success("Preview rendered");
    else toast.error(r.error || "Preview failed");
    setBusy(false);
  }

  async function remove() {
    setBusy(true);
    const r = await endpoints.deleteTemplate(key, channel, locale);
    setResult(r);
    if (r.ok) {
      toast.success("Deleted");
      await load();
    } else toast.error(r.error || "Delete failed");
    setBusy(false);
  }

  function pickRow(row: TemplateRow) {
    if (row.key) setKey(String(row.key));
    if (row.channel) setChannel(String(row.channel));
    if (row.locale) setLocale(String(row.locale));
    if (row.subject) setSubject(String(row.subject));
    if (row.body) setBody(String(row.body));
    if (row.htmlBody) setHtmlBody(String(row.htmlBody));
    toast.message("Loaded into editor");
  }

  const interactiveColumns: ColumnDef<TemplateRow>[] = [
    ...columns,
    {
      id: "actions",
      header: "",
      cell: ({ row }) => (
        <Button size="sm" variant="ghost" onClick={() => pickRow(row.original)}>
          Edit
        </Button>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Templates"
        description="Content operators maintain channel-specific copy with {{placeholders}}. List from the API, edit in the form, preview before send."
        actions={
          <Button variant="outline" onClick={load} disabled={busy}>
            <RefreshCw className={`h-4 w-4 ${busy ? "animate-spin" : ""}`} />
            Refresh list
          </Button>
        }
      />
      <Tabs defaultValue="library">
        <TabsList>
          <TabsTrigger value="library">Library</TabsTrigger>
          <TabsTrigger value="editor">Editor</TabsTrigger>
        </TabsList>
        <TabsContent value="library">
          <Card>
            <CardHeader>
              <CardTitle>Template library</CardTitle>
              <CardDescription>Sortable / filterable DataTable over GET /api/v1/templates</CardDescription>
            </CardHeader>
            <CardContent>
              <DataTable columns={interactiveColumns} data={rows} searchKey="key" searchPlaceholder="Filter by key…" />
            </CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="editor">
          <div className="grid gap-6 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Editor</CardTitle>
                <CardDescription>POST save · preview · DELETE</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-2">
                    <Label>Key</Label>
                    <Input value={key} onChange={(e) => setKey(e.target.value)} />
                  </div>
                  <div className="space-y-2">
                    <Label>Locale</Label>
                    <Input value={locale} onChange={(e) => setLocale(e.target.value)} />
                  </div>
                </div>
                <div className="space-y-2">
                  <Label>Channel</Label>
                  <Select value={channel} onValueChange={setChannel}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {["email", "sms", "push", "inapp", "chat"].map((c) => (
                        <SelectItem key={c} value={c}>{c}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Subject</Label>
                  <Input value={subject} onChange={(e) => setSubject(e.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label>Body</Label>
                  <Textarea rows={3} value={body} onChange={(e) => setBody(e.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label>HTML body</Label>
                  <Textarea rows={3} value={htmlBody} onChange={(e) => setHtmlBody(e.target.value)} />
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button disabled={busy} onClick={save}>
                    {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                    Save
                  </Button>
                  <Button variant="secondary" disabled={busy} onClick={preview}>
                    <Eye className="h-4 w-4" /> Preview
                  </Button>
                  <Button variant="destructive" disabled={busy} onClick={remove}>
                    <Trash2 className="h-4 w-4" /> Delete
                  </Button>
                </div>
              </CardContent>
            </Card>
            <ResponsePanel result={result} />
          </div>
        </TabsContent>
      </Tabs>
    </>
  );
}
