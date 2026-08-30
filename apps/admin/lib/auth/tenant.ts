import { getTenantId } from './session'

export function resolveTenantId(explicit?: string | null) {
  return explicit ?? getTenantId()
}
