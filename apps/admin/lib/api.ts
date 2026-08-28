const DEFAULT_BASE = "http://localhost:5245";

export function getApiBase(): string {
  if (typeof window !== "undefined") {
    return localStorage.getItem("nh_api_base") || process.env.NEXT_PUBLIC_API_BASE_URL || DEFAULT_BASE;
  }
  return process.env.NEXT_PUBLIC_API_BASE_URL || DEFAULT_BASE;
}

export function getApiKey(): string {
  if (typeof window !== "undefined") {
    return localStorage.getItem("nh_api_key") || process.env.NEXT_PUBLIC_API_KEY || "";
  }
  return process.env.NEXT_PUBLIC_API_KEY || "";
}

export function setCredentials(base: string, key: string) {
  localStorage.setItem("nh_api_base", base.replace(/\/$/, ""));
  localStorage.setItem("nh_api_key", key);
}

export type ApiResult<T = unknown> = {
  ok: boolean;
  status: number;
  data: T | null;
  error?: string;
};

export async function api<T = unknown>(
  path: string,
  options: RequestInit & { query?: Record<string, string | undefined> } = {}
): Promise<ApiResult<T>> {
  const { query, ...init } = options;
  let url = `${getApiBase()}${path.startsWith("/") ? path : `/${path}`}`;
  if (query) {
    const qs = new URLSearchParams();
    Object.entries(query).forEach(([k, v]) => {
      if (v !== undefined && v !== "") qs.set(k, v);
    });
    const s = qs.toString();
    if (s) url += `?${s}`;
  }

  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body) headers.set("Content-Type", "application/json");
  const key = getApiKey();
  if (key) headers.set("X-Api-Key", key);

  try {
    const res = await fetch(url, { ...init, headers });
    const text = await res.text();
    let data: T | null = null;
    if (text) {
      try {
        data = JSON.parse(text) as T;
      } catch {
        data = text as unknown as T;
      }
    }
    if (!res.ok) {
      const errObj = data as { detail?: string; title?: string; message?: string; error?: string } | null;
      const msg =
        errObj?.detail || errObj?.title || errObj?.message || errObj?.error || text || res.statusText;
      return { ok: false, status: res.status, data, error: String(msg) };
    }
    return { ok: true, status: res.status, data };
  } catch (e) {
    return {
      ok: false,
      status: 0,
      data: null,
      error: e instanceof Error ? e.message : "Network error — is Host running on :5245?",
    };
  }
}

export const endpoints = {
  sendNotification: (body: unknown) => api("/api/v1/notifications", { method: "POST", body: JSON.stringify(body) }),
  sendSync: (body: unknown) => api("/api/v1/notifications/sync", { method: "POST", body: JSON.stringify(body) }),
  getStatus: (id: string) => api(`/api/v1/notifications/${id}`),
  listPlugins: () => api("/api/v1/plugins"),
  listTemplates: (q?: { tenantId?: string; channel?: string }) => api("/api/v1/templates", { query: q }),
  saveTemplate: (body: unknown) => api("/api/v1/templates", { method: "POST", body: JSON.stringify(body) }),
  deleteTemplate: (key: string, channel: string, locale?: string, tenantId?: string) =>
    api(`/api/v1/templates/${encodeURIComponent(key)}`, { method: "DELETE", query: { channel, locale, tenantId } }),
  previewTemplate: (body: unknown) => api("/api/v1/templates/preview", { method: "POST", body: JSON.stringify(body) }),
  getPreferences: (userId: string, tenantId?: string) =>
    api(`/api/v1/preferences/${encodeURIComponent(userId)}`, { query: { tenantId } }),
  savePreferences: (body: unknown) => api("/api/v1/preferences", { method: "PUT", body: JSON.stringify(body) }),
  createWebhook: (body: unknown) => api("/api/v1/webhooks", { method: "POST", body: JSON.stringify(body) }),
  recordConsent: (body: unknown) => api("/api/v1/consents", { method: "POST", body: JSON.stringify(body) }),
  listConsents: (subjectId: string, tenantId?: string) =>
    api(`/api/v1/consents/${encodeURIComponent(subjectId)}`, { query: { tenantId } }),
  evaluateConsent: (q: { subjectId: string; purpose: string; channel?: string; tenantId?: string }) =>
    api("/api/v1/consents/evaluate", { method: "POST", query: q }),
  saveWorkflow: (body: unknown) => api("/api/v1/workflows", { method: "POST", body: JSON.stringify(body) }),
  startWorkflow: (body: unknown) => api("/api/v1/workflows/start", { method: "POST", body: JSON.stringify(body) }),
  getWorkflowRun: (runId: string) => api(`/api/v1/workflows/runs/${runId}`),
  getWorkflowTimeline: (runId: string) => api(`/api/v1/workflows/runs/${runId}/timeline`),
  cancelWorkflow: (runId: string) => api(`/api/v1/workflows/runs/${runId}/cancel`, { method: "POST" }),
  messagingHealth: () => api("/api/v1/admin/messaging/health"),
  saveSegment: (body: unknown) => api("/api/v1/segments", { method: "POST", body: JSON.stringify(body) }),
  getSegment: (key: string, tenantId?: string) =>
    api(`/api/v1/segments/${encodeURIComponent(key)}`, { query: { tenantId } }),
  matchSegment: (key: string, body: unknown, tenantId?: string) =>
    api(`/api/v1/segments/${encodeURIComponent(key)}/match`, {
      method: "POST",
      body: JSON.stringify(body),
      query: { tenantId },
    }),
  trackEngagement: (body: unknown) => api("/api/v1/engagement", { method: "POST", body: JSON.stringify(body) }),
  listEngagement: (id: string) => api(`/api/v1/notifications/${id}/engagement`),
  engagementStats: (q?: { from?: string; to?: string; tenantId?: string }) =>
    api("/api/v1/engagement/stats", { query: q }),
  registerDevice: (body: unknown) => api("/api/v1/devices", { method: "POST", body: JSON.stringify(body) }),
  listDevices: (userId: string, tenantId?: string) =>
    api(`/api/v1/devices/${encodeURIComponent(userId)}`, { query: { tenantId } }),
  saveTopic: (body: unknown) => api("/api/v1/topics", { method: "POST", body: JSON.stringify(body) }),
  listTopics: (tenantId?: string) => api("/api/v1/topics", { query: { tenantId } }),
  createCampaign: (body: unknown) => api("/api/v1/campaigns", { method: "POST", body: JSON.stringify(body) }),
  addRecipients: (id: string, body: unknown) =>
    api(`/api/v1/campaigns/${id}/recipients`, { method: "POST", body: JSON.stringify(body) }),
  startCampaign: (id: string) => api(`/api/v1/campaigns/${id}/send`, { method: "POST" }),
  cancelCampaign: (id: string) => api(`/api/v1/campaigns/${id}/cancel`, { method: "POST" }),
  getCampaign: (id: string) => api(`/api/v1/campaigns/${id}`),
  getCampaignProgress: (id: string) => api(`/api/v1/campaigns/${id}/progress`),
  sendBroadcast: (body: unknown) => api("/api/v1/broadcasts", { method: "POST", body: JSON.stringify(body) }),
  healthLive: () => api("/health/live"),
  healthReady: () => api("/health/ready"),
};

/** Normalize list payloads from API into arrays for DataTable */
export function asArray<T = Record<string, unknown>>(data: unknown): T[] {
  if (Array.isArray(data)) return data as T[];
  if (data && typeof data === "object") {
    const o = data as Record<string, unknown>;
    for (const k of ["items", "data", "results", "value", "plugins", "templates", "topics"]) {
      if (Array.isArray(o[k])) return o[k] as T[];
    }
    // single object → one row
    return [data as T];
  }
  return [];
}
