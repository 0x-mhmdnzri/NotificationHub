/** Canonical server-side table query + response (mirrors backend PagedRequest / PagedResult). */

export type SortOrder = 'asc' | 'desc'

export interface PagedRequest {
  page?: number
  pageSize?: number
  sort?: string
  order?: SortOrder
  search?: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages?: number
  hasNext?: boolean
  hasPrevious?: boolean
}

export function normalizePagedResult<T>(raw: unknown): PagedResult<T> {
  if (!raw || typeof raw !== 'object') {
    return { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }
  }
  const r = raw as Record<string, unknown>
  const items = (r.items ?? r.Items ?? []) as T[]
  const page = Number(r.page ?? r.Page ?? 1)
  const pageSize = Number(r.pageSize ?? r.PageSize ?? 20)
  const totalCount = Number(r.totalCount ?? r.TotalCount ?? 0)
  const totalPages =
    r.totalPages != null
      ? Number(r.totalPages)
      : r.TotalPages != null
        ? Number(r.TotalPages)
        : pageSize > 0
          ? Math.ceil(totalCount / pageSize)
          : 0
  return {
    items: Array.isArray(items) ? items : [],
    page,
    pageSize,
    totalCount,
    totalPages,
    hasNext: Boolean(r.hasNext ?? r.HasNext ?? page * pageSize < totalCount),
    hasPrevious: Boolean(r.hasPrevious ?? r.HasPrevious ?? page > 1),
  }
}
