"""F23 — minimal Python client."""
from __future__ import annotations
import urllib.request
import json
from typing import Any

class NotificationHubClient:
    def __init__(self, base_url: str, api_key: str):
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key

    def _req(self, method: str, path: str, body: dict | None = None) -> Any:
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request(
            f"{self.base_url}{path}",
            data=data,
            method=method,
            headers={"Content-Type": "application/json", "X-Api-Key": self.api_key},
        )
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read().decode())

    def send(self, payload: dict) -> Any:
        return self._req("POST", "/api/v1/notifications", payload)

    def identify(self, payload: dict) -> Any:
        return self._req("POST", "/api/v1/cdp/identify", payload)

    def track(self, payload: dict) -> Any:
        return self._req("POST", "/api/v1/cdp/track", payload)
