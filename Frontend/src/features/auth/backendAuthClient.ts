import { ApiError, createApiClient, type ApiClient } from '../../api/apiClient.ts'
import type { AuthClient, AuthenticatedUser } from './authTypes.ts'

interface UserSessionResponse {
  id: string
  displayName: string
  email: string
  isEmailVerified: boolean
  profileImageUrl?: string
}

interface LogoutResponse {
  redirectUri: string
}

interface BackendAuthClientOptions {
  apiClient?: ApiClient
  navigate?(url: string): void
}

export function createBackendAuthClient(options: BackendAuthClientOptions = {}): AuthClient {
  const apiClient = options.apiClient ?? createApiClient()
  const navigate = options.navigate ?? ((url: string) => window.location.assign(url))
  let sessionRequest: Promise<AuthenticatedUser | null> | null = null

  function beginAuthentication(parameters: Record<string, string> = {}) {
    const query = new URLSearchParams(parameters).toString()
    navigate(`${apiClient.baseUrl}/api/user/login${query.length === 0 ? '' : `?${query}`}`)
    return Promise.resolve(null)
  }

  return {
    getSession() {
      sessionRequest ??= loadSession(apiClient).finally(() => {
        sessionRequest = null
      })
      return sessionRequest
    },
    signInWithEmail(email) {
      return beginAuthentication({ email })
    },
    signInWithSocialProvider(provider) {
      return beginAuthentication({ provider })
    },
    signUp() {
      return beginAuthentication({ screen: 'sign-up' })
    },
    requestPasswordReset(email) {
      return apiClient.request<void>('/api/user/forgot-password', {
        method: 'POST',
        body: JSON.stringify({ email }),
      })
    },
    confirmPasswordReset(token, newPassword) {
      return apiClient.request<void>('/api/user/reset-password', {
        method: 'POST',
        body: JSON.stringify({ token, newPassword }),
      })
    },
    async signOut() {
      try {
        const response = await apiClient.request<LogoutResponse>('/api/user/logout', { method: 'POST' })
        navigate(response.redirectUri)
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          navigate('/')
          return
        }
        throw error
      }
    },
  }
}

async function loadSession(apiClient: ApiClient) {
  try {
    return mapUser(await apiClient.request<UserSessionResponse>('/api/user/session'))
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null
    }
    throw error
  }
}

function mapUser(response: UserSessionResponse): AuthenticatedUser {
  return {
    id: response.id,
    name: response.displayName,
    email: response.email,
    initials: getInitials(response.displayName),
    isEmailVerified: response.isEmailVerified,
    profileImageUrl: response.profileImageUrl,
  }
}

function getInitials(name: string) {
  return name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}
