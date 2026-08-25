/** F23 — minimal Node/browser client (fetch) */
export function createClient({ baseUrl, apiKey }) {
  const headers = { "Content-Type": "application/json", "X-Api-Key": apiKey };
  return {
    async send(body) {
      const r = await fetch(`${baseUrl}/api/v1/notifications`, { method: "POST", headers, body: JSON.stringify(body) });
      if (!r.ok) throw new Error(await r.text());
      return r.json();
    },
    async identify(body) {
      const r = await fetch(`${baseUrl}/api/v1/cdp/identify`, { method: "POST", headers, body: JSON.stringify(body) });
      if (!r.ok) throw new Error(await r.text());
      return r.json();
    },
    async track(body) {
      const r = await fetch(`${baseUrl}/api/v1/cdp/track`, { method: "POST", headers, body: JSON.stringify(body) });
      if (!r.ok) throw new Error(await r.text());
      return r.json();
    },
    async inbox(userId) {
      const r = await fetch(`${baseUrl}/api/v1/inbox/${encodeURIComponent(userId)}`, { headers });
      if (!r.ok) throw new Error(await r.text());
      return r.json();
    }
  };
}
