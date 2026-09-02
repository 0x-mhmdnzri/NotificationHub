'use client'

/**
 * Throughput chart is disabled until a real time-series metrics endpoint exists.
 * Do not invent daily series on the client.
 */
export function DashboardChart({ hasData = false }: { hasData?: boolean }) {
  if (!hasData) {
    return (
      <div className="flex h-[290px] w-full items-center justify-center rounded-xl border border-dashed text-sm text-muted-foreground">
        No throughput series available yet
      </div>
    )
  }
  return null
}
