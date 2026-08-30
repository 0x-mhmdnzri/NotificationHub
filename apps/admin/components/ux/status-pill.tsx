'use client'

import { Badge } from '@/components/ui/badge'
import { formatStatus, statusTone } from '@/lib/ux/labels'

export function StatusPill({ status }: { status?: string | null }) {
  return <Badge variant={statusTone(status)}>{formatStatus(status)}</Badge>
}
