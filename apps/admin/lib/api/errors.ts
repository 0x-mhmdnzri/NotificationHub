export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  [key: string]: unknown
}

export class ApiError extends Error {
  readonly status: number
  readonly details?: ProblemDetails

  constructor(message: string, status: number, details?: ProblemDetails) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}
