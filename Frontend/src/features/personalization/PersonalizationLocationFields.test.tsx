import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'
import { PersonalizationPage } from './PersonalizationPage.tsx'
import type { PersonalizationProfile } from './personalizationTypes.ts'

async function setup() {
  const { conversationSettingsClient: client } = createDemoApplicationClients()
  const { personalization: profile } = await client.getPersonalization()
  if (!profile) { throw new Error('Missing test profile') }
  const onSave = vi.fn(async (value: PersonalizationProfile) => value)
  return { client, profile, onSave, user: userEvent.setup() }
}

describe('Personalization location fields', () => {
  it('selects an international current city without changing the birthplace', async () => {
    const { client, profile, onSave, user } = await setup()
    render(<PersonalizationPage profile={profile} client={client} onSave={onSave} onBack={vi.fn()} />)
    await user.selectOptions(screen.getByLabelText('Current location location type'), 'city')
    await user.type(screen.getByLabelText('Current location search cities'), 'Jerusalem')
    await user.selectOptions(screen.getByLabelText('Current location city'), '281184')
    expect(screen.getByLabelText('Birthplace U.S. ZIP code')).toHaveValue('91302')
    expect(screen.queryByLabelText('Birth time zone')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Save personalization' }))
    expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
      currentLocation: expect.objectContaining({ kind: 'city', id: '281184', timeZone: 'Asia/Jerusalem' }),
      birthLocation: expect.objectContaining({ kind: 'zip', id: '91302' }),
      birthTimeZone: 'America/Los_Angeles',
    }))
  })

  it('copies the current location once without linking future edits', async () => {
    const { client, profile, onSave, user } = await setup()
    render(<PersonalizationPage profile={profile} client={client} onSave={onSave} onBack={vi.fn()} />)
    const current = screen.getByLabelText('Current location U.S. ZIP code')
    await user.clear(current)
    await user.type(current, '10001')
    await user.click(screen.getByRole('button', { name: 'Use current location as birthplace' }))
    await user.clear(current)
    await user.type(current, '91302')
    expect(screen.getByLabelText('Birthplace U.S. ZIP code')).toHaveValue('10001')
  })

  it('keeps ZIP entry available when city loading fails and allows retry', async () => {
    const { client, profile, onSave, user } = await setup()
    const cities = await client.getLocations()
    client.getLocations = vi.fn().mockRejectedValueOnce(new Error('Offline')).mockResolvedValue(cities)
    render(<PersonalizationPage profile={profile} client={client} onSave={onSave} onBack={vi.fn()} />)
    expect(await screen.findByText(/The city list could not be loaded/)).toBeVisible()
    expect(screen.getByLabelText('Current location U.S. ZIP code')).toBeEnabled()
    await user.click(screen.getByRole('button', { name: 'Retry city list' }))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Retry city list' })).not.toBeInTheDocument())
    await user.selectOptions(screen.getByLabelText('Current location location type'), 'city')
    expect(screen.getByRole('option', { name: 'Jerusalem, Israel' })).toBeInTheDocument()
  })

  it('preserves the draft after a failed lookup and displays server-resolved data after retry', async () => {
    const { client, profile, onSave, user } = await setup()
    const saved = { ...profile, birthTimeZone: 'America/New_York', birthLocation: { kind: 'zip' as const, id: '10001', label: 'New York, NY', timeZone: 'America/New_York' } }
    onSave.mockRejectedValueOnce(new Error('Location lookup unavailable. Try again.')).mockResolvedValueOnce(saved)
    render(<PersonalizationPage profile={profile} client={client} onSave={onSave} onBack={vi.fn()} />)
    const birth = screen.getByLabelText('Birthplace U.S. ZIP code')
    await user.clear(birth)
    await user.type(birth, '10001')
    await user.click(screen.getByRole('button', { name: 'Save personalization' }))
    expect(await screen.findByText('Location lookup unavailable. Try again.')).toBeVisible()
    expect(birth).toHaveValue('10001')
    await user.click(screen.getByRole('button', { name: 'Save personalization' }))
    expect(await screen.findByText('New York, NY · America/New_York')).toBeVisible()
    expect(screen.queryByText('Location lookup unavailable. Try again.')).not.toBeInTheDocument()
  })
})
