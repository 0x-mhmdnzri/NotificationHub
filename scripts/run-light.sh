#!/usr/bin/env bash
# Run Host optimized for local/dev with minimal overhead
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_TieredCompilation=1
export DOTNET_TC_QuickJitForLoops=1
export DOTNET_ReadyToRun=1
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
# Server GC only makes sense for multi-core production; for light local use workstation GC
export DOTNET_gcServer=0
export DOTNET_GCConserveMemory=5

dotnet run --project "$ROOT/src/NotificationHub.Host/NotificationHub.Host.csproj" \
  -c Release \
  --no-launch-profile \
  "$@"
