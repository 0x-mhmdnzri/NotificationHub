#!/usr/bin/env bash
# Measure Host cold-start: process start → first successful HTTP response.
# Usage: ./scripts/measure-cold-start.sh [iterations]
# Requires: published Host, Postgres reachable, ASPNETCORE_ENVIRONMENT set.
set -euo pipefail
ITERATIONS=${1:-5}
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_DIR="${PUBLISH_DIR:-$PROJECT_ROOT/artifacts/publish-host}"
URL="${COLD_START_URL:-http://127.0.0.1:5245/health/live}"
PORT="${ASPNETCORE_URLS:-http://127.0.0.1:5245}"

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "Publishing Host to $PUBLISH_DIR ..."
  dotnet publish "$PROJECT_ROOT/src/Host/NotificationHub.Host/NotificationHub.Host.csproj" \
    -c Release -o "$PUBLISH_DIR" --verbosity quiet
fi

echo "iterations=$ITERATIONS url=$URL"
RESULTS=()
for i in $(seq 1 "$ITERATIONS"); do
  # Kill any leftover
  pkill -f "NotificationHub.Host.dll" 2>/dev/null || true
  sleep 0.5
  START_NS=$(date +%s%N)
  (
    cd "$PUBLISH_DIR"
    ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}" \
    ASPNETCORE_URLS="$PORT" \
    DOTNET_TieredPGO=1 \
    DOTNET_TC_QuickJitForLoops=1 \
    DOTNET_GCDynamicAdaptationMode=1 \
      dotnet NotificationHub.Host.dll >"/tmp/nh-cold-$i.log" 2>&1 &
    echo $! >"/tmp/nh-cold-$i.pid"
  )
  # Poll until healthy or timeout 60s
  OK=0
  for _ in $(seq 1 120); do
    if curl -sf -o /dev/null "$URL" 2>/dev/null; then
      OK=1
      break
    fi
    sleep 0.25
  done
  END_NS=$(date +%s%N)
  PID=$(cat "/tmp/nh-cold-$i.pid" 2>/dev/null || echo "")
  if [[ -n "$PID" ]]; then kill "$PID" 2>/dev/null || true; fi
  MS=$(( (END_NS - START_NS) / 1000000 ))
  if [[ "$OK" -eq 1 ]]; then
    echo "run $i: ${MS}ms (ok)"
    RESULTS+=("$MS")
  else
    echo "run $i: TIMEOUT/FAIL (${MS}ms) — see /tmp/nh-cold-$i.log"
  fi
done

if [[ ${#RESULTS[@]} -gt 0 ]]; then
  # simple avg
  SUM=0
  for v in "${RESULTS[@]}"; do SUM=$((SUM + v)); done
  AVG=$((SUM / ${#RESULTS[@]}))
  echo "---"
  echo "n=${#RESULTS[@]} avg=${AVG}ms values=${RESULTS[*]}"
fi
