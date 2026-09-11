import { describe, expect, it, vi } from 'vitest'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'
import { createBackendConversationSettingsClient } from './conversationSettingsClient.ts'
import { DefaultReadingPreferences } from '../reading/readingPreferences.ts'

describe('Reading preference API contract', () => {
  it('loads account reading preferences with cancellation', async () => {
    const request = vi.fn().mockResolvedValue(DefaultReadingPreferences)
    const client = createBackendConversationSettingsClient({ baseUrl: '', request })
    const controller = new AbortController()

    expect(await client.getReadingPreferences(controller.signal)).toEqual(DefaultReadingPreferences)
    expect(request).toHaveBeenCalledWith('/api/conversation-settings/reading', { signal: controller.signal })
  })

  it('persists only the reading preferences through their dedicated endpoint', async () => {
    const preferences = { ...DefaultReadingPreferences, textSize: 'large' as const, theme: 'dark' as const, focusLongContent: true }
    const request = vi.fn().mockResolvedValue(preferences)
    const client = createBackendConversationSettingsClient({ baseUrl: '', request })

    expect(await client.updateReadingPreferences(preferences)).toEqual(preferences)
    expect(request).toHaveBeenCalledWith('/api/conversation-settings/reading', { method: 'PUT', body: JSON.stringify(preferences) })
  })
})

describe('Personalization location API contract', () => {
  it('sends only selected identifiers and uses server-resolved location metadata', async () => {
    const { personalization } = await createDemoApplicationClients().conversationSettingsClient.getPersonalization()
    if (!personalization) { throw new Error('Missing test profile') }
    const draft = { ...personalization, birthLocation: { kind: 'zip' as const, id: '91302', timeZone: 'Untrusted/Zone', latitude: 0, longitude: 0 }, currentLocation: { kind: 'city' as const, id: '281184', label: 'Untrusted label' } }
    const returned = { ...personalization, currentLocation: { kind: 'city', id: '281184', label: 'Jerusalem, Israel', timeZone: 'Asia/Jerusalem' } }
    const request = vi.fn().mockResolvedValue({ isConfigured: true, personalization: returned })
    const client = createBackendConversationSettingsClient({ baseUrl: '', request })

    const saved = await client.updatePersonalization(draft)

    const body = JSON.parse(request.mock.calls[0][1].body)
    expect(body.birthLocation).toEqual({ kind: 'zip', id: '91302' })
    expect(body.currentLocation).toEqual({ kind: 'city', id: '281184' })
    expect(body.birthDateTime).toBe('2001-12-17T09:30:00')
    expect(saved.currentLocation).toEqual(returned.currentLocation)
    expect(saved.birthTimeZone).toBe('America/Los_Angeles')
  })

  it('loads supported locations through the authenticated settings client with cancellation', async () => {
    const request = vi.fn().mockResolvedValue([])
    const client = createBackendConversationSettingsClient({ baseUrl: '', request })
    const controller = new AbortController()

    await client.getLocations(controller.signal)

    expect(request).toHaveBeenCalledWith('/api/conversation-settings/locations', { signal: controller.signal })
  })
})
