import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient, type ApiClient } from './apiClient.ts'
import { createBackendAuthClient } from '../features/auth/backendAuthClient.ts'
import { createBackendConversationClient } from '../features/conversations/conversationClient.ts'
import { createBackendConversationSettingsClient } from '../features/personalization/conversationSettingsClient.ts'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('API client', () => {
  it('sends JSON requests with the browser session cookie enabled', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ value: 1 }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)
    const client = createApiClient('http://localhost:5090/')

    const result = await client.request<{ value: number }>('/api/example', { method: 'POST', body: JSON.stringify({ question: 'Why?' }) })

    expect(result.value).toBe(1)
    expect(fetchMock).toHaveBeenCalledWith('http://localhost:5090/api/example', expect.objectContaining({
      method: 'POST',
      credentials: 'include',
    }))
    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json')
  })

  it('returns structured API failure details', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      title: 'Persistence unavailable',
      detail: 'The application database is unavailable.',
      code: 'persistence_unavailable',
      traceId: 'trace-1',
    }), { status: 503, headers: { 'Content-Type': 'application/problem+json' } })))
    const client = createApiClient()

    const action = client.request('/api/conversations')

    await expect(action).rejects.toMatchObject({
      status: 503,
      code: 'persistence_unavailable',
      traceId: 'trace-1',
      message: 'The application database is unavailable.',
    })
  })
})

describe('backend adapters', () => {
  it('treats an unauthorized session lookup as signed out', async () => {
    const apiClient = createMockApiClient(vi.fn().mockRejectedValue(new ApiError(401, { detail: 'Authentication required' })))
    const client = createBackendAuthClient({ apiClient, navigate: vi.fn() })

    const session = await client.getSession()

    expect(session).toBeNull()
  })

  it('starts purpose-specific backend-owned WorkOS flows', async () => {
    const navigate = vi.fn()
    const client = createBackendAuthClient({ apiClient: createMockApiClient(vi.fn()), navigate })

    await client.signInWithEmail('amitai+study@example.com')
    await client.signInWithSocialProvider('google')
    await client.signUp()

    expect(navigate).toHaveBeenNthCalledWith(1, 'http://localhost:5090/api/user/login?email=amitai%2Bstudy%40example.com')
    expect(navigate).toHaveBeenNthCalledWith(2, 'http://localhost:5090/api/user/login?provider=google')
    expect(navigate).toHaveBeenNthCalledWith(3, 'http://localhost:5090/api/user/login?screen=sign-up')
  })

  it('confirms password resets only through the backend', async () => {
    const request = vi.fn().mockResolvedValue(undefined)
    const client = createBackendAuthClient({ apiClient: createMockApiClient(request), navigate: vi.fn() })

    await client.confirmPasswordReset('reset-token', 'LongEnoughPassword!42')

    expect(request).toHaveBeenCalledWith('/api/user/reset-password', {
      method: 'POST',
      body: JSON.stringify({ token: 'reset-token', newPassword: 'LongEnoughPassword!42' }),
    })
  })

  it('sends an idempotency ID when appending a conversation message', async () => {
    const response = {
      status: 'stored',
      conversation: {
        id: 'conversation-id',
        title: 'New conversation',
        enabledSourceKeys: ['collection:Torah'],
        messages: [],
        createdAtUtc: '2026-08-25T00:00:00Z',
        updatedAtUtc: '2026-08-25T00:00:00Z',
      },
    }
    const request = vi.fn().mockResolvedValue(response)
    const client = createBackendConversationClient(createMockApiClient(request))

    await client.appendMessage('conversation-id', 'message-id', 'Why?')

    expect(request).toHaveBeenCalledWith('/api/conversations/conversation-id/messages?compact=true', {
      method: 'POST',
      body: JSON.stringify({ messageId: 'message-id', content: 'Why?' }),
    })
  })

  it('creates a conversation only through a first-message request', async () => {
    const request = vi.fn().mockResolvedValue({ status: 'answered', conversation: {}, message: null })
    const client = createBackendConversationClient(createMockApiClient(request))

    await client.createWithMessage('first-message-id', 'Why do customs differ?', ['collection:Torah'])

    expect(request).toHaveBeenCalledWith('/api/conversations?compact=true', {
      method: 'POST',
      body: JSON.stringify({ messageId: 'first-message-id', content: 'Why do customs differ?', enabledSourceKeys: ['collection:Torah'] }),
    })
  })

  it('sends a local birth time without adding a UTC offset', async () => {
    const request = vi.fn().mockResolvedValue({
      isConfigured: true,
      personalization: {
        fullName: 'Amitai Erfanian',
        birthDateTime: '2001-12-17T09:30:00',
        birthTimeZone: 'America/Los_Angeles',
        conversationLanguage: 'English',
        quotationLanguage: 'Hebrew',
        religiousMovement: 'Conservadox',
        jewishHeritage: 'Mizrahi',
        additionalContext: null,
      },
    })
    const client = createBackendConversationSettingsClient(createMockApiClient(request))

    await client.updatePersonalization({
      fullName: 'Amitai Erfanian',
      birthDateTime: '2001-12-17T09:30',
      birthTimeZone: 'America/Los_Angeles',
      conversationLanguage: 'English',
      quotationLanguage: 'Hebrew',
      religiousMovement: 'Conservadox',
      jewishHeritage: 'Mizrahi',
      additionalContext: '',
    })

    expect(request).toHaveBeenCalledWith('/api/conversation-settings/personalization', {
      method: 'PUT',
      body: expect.stringContaining('"birthDateTime":"2001-12-17T09:30:00"'),
    })
    const body = JSON.parse(request.mock.calls[0][1].body as string) as { birthDateTime: string }
    expect(body.birthDateTime.endsWith('Z')).toBe(false)
  })

  it('persists conversation defaults through the account settings API', async () => {
    const preferences = { showSourceContextByDefault: false, emailProductUpdates: true }
    const request = vi.fn().mockResolvedValue(preferences)
    const client = createBackendConversationSettingsClient(createMockApiClient(request))

    const result = await client.updatePreferences(preferences)

    expect(result).toEqual(preferences)
    expect(request).toHaveBeenCalledWith('/api/conversation-settings/preferences', {
      method: 'PUT',
      body: JSON.stringify(preferences),
    })
  })
})

function createMockApiClient(request: ApiClient['request']): ApiClient {
  return {
    baseUrl: 'http://localhost:5090',
    request,
  }
}
