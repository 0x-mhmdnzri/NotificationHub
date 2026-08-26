#!/usr/bin/env bash
# Multi-platform publish for NotificationHub.Host (framework-dependent, ReadyToRun, compact)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${OUT_DIR:-$ROOT/artifacts/publish}"
CONFIG="${CONFIG:-Release}"
PROJECT="$ROOT/src/NotificationHub.Host/NotificationHub.Host.csproj"

# RIDs: x64, x86, arm (32), arm64 across OS families
RIDS=(
  linux-x64
  linux-arm64
  linux-arm
  win-x64
  win-x86
  win-arm64
  osx-x64
  osx-arm64
)

echo "==> Restoring solution (central package versions)"
dotnet restore "$ROOT/NotificationHub.sln" -c "$CONFIG"

echo "==> Building solution once"
dotnet build "$ROOT/NotificationHub.sln" -c "$CONFIG" --no-restore

for rid in "${RIDS[@]}"; do
  dest="$OUT/$rid"
  echo "==> Publish $rid -> $dest"
  dotnet publish "$PROJECT" \
    -c "$CONFIG" \
    -r "$rid" \
    --self-contained false \
    -p:PublishReadyToRun=true \
    -p:PublishSingleFile=false \
    -p:DebugType=portable \
    -o "$dest"
done

echo "==> Done. Artifacts under $OUT"
du -sh "$OUT"/* 2>/dev/null || true
