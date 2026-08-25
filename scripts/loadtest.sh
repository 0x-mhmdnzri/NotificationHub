#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE_URL="${BASE_URL:-http://localhost:8080}"
API_KEY="${API_KEY:-dev-secret-key-change-me}"
TOTAL="${TOTAL:-500}"
CONCURRENCY="${CONCURRENCY:-25}"
CHANNEL="${CHANNEL:-email}"

echo "Running NotificationHub load test..."
dotnet run --project "$ROOT/tools/loadtest/NotificationHub.LoadTest.csproj" -c Release -- \
  --baseUrl "$BASE_URL" \
  --apiKey "$API_KEY" \
  --total "$TOTAL" \
  --concurrency "$CONCURRENCY" \
  --channel "$CHANNEL"
