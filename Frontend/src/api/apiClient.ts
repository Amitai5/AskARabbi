export interface ApiProblemDetails {
  title?: string
  detail?: string
  status?: number
  code?: string
  traceId?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly traceId?: string

  constructor(status: number, problem: ApiProblemDetails) {
    super(problem.detail ?? problem.title ?? `The AskRabbi API returned HTTP ${status}.`)
    this.name = 'ApiError'
    this.status = status
    this.code = problem.code
    this.traceId = problem.traceId
  }
}

export interface ApiClient {
  readonly baseUrl: string
  request<T>(path: string, init?: RequestInit): Promise<T>
}

const DevelopmentApiBaseUrl = 'http://localhost:5090'
const ProductionApiBaseUrl = 'https://api.askarabbi.ai'
const DefaultApiBaseUrl = import.meta.env.PROD ? ProductionApiBaseUrl : DevelopmentApiBaseUrl

export function createApiClient(baseUrl = import.meta.env.VITE_API_BASE_URL || DefaultApiBaseUrl): ApiClient {
  const normalizedBaseUrl = baseUrl.replace(/\/+$/, '')

  return {
    baseUrl: normalizedBaseUrl,
    async request<T>(path: string, init: RequestInit = {}): Promise<T> {
      const headers = new Headers(init.headers)
      headers.set('Accept', 'application/json')
      if (init.body !== undefined && !headers.has('Content-Type')) {
        headers.set('Content-Type', 'application/json')
      }

      const response = await fetch(`${normalizedBaseUrl}${path}`, {
        ...init,
        credentials: 'include',
        headers,
      })

      if (!response.ok) {
        throw new ApiError(response.status, await readProblemDetails(response))
      }
      if (response.status === 204) {
        return undefined as T
      }

      return await response.json() as T
    },
  }
}

async function readProblemDetails(response: Response): Promise<ApiProblemDetails> {
  try {
    return await response.json() as ApiProblemDetails
  } catch {
    return { status: response.status }
  }
}
