import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { OfflineHolidayCalendar } from './OfflineHolidayCalendar.tsx'
import { OfflineLibraryChanged, readOfflineLibrary } from './offlineLibrary.ts'
import { RoshHashanah } from '../calendar/calendarTestData.ts'

vi.mock('./offlineLibrary.ts', async original => ({ ...await original<typeof import('./offlineLibrary.ts')>(), readOfflineLibrary: vi.fn() }))

describe('saved holiday calendar', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime('2026-09-10T12:00:00Z')
    vi.mocked(readOfflineLibrary).mockResolvedValue({ audioEnabled: false, revision: 1, teaching: null, holidays: { version: 1, startDate: '2026-09-10', endDate: '2027-09-04', savedAt: '2026-09-10T12:00:00Z', inIsrael: false, events: [RoshHashanah, { ...RoshHashanah, id: 'spring', title: 'Spring holiday', startDate: '2027-03-25', beginningDate: '2027-03-24', endDate: '2027-03-26' }] } })
  })
  afterEach(() => { vi.clearAllMocks(); vi.useRealTimers() })

  it('shows saved dates and offline filters without any network requests', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch')
    const user = userEvent.setup()
    render(<OfflineHolidayCalendar />)
    expect(await screen.findByText(/Available through September 4, 2027/)).toBeVisible()
    expect(screen.getByRole('heading', { name: 'Rosh Hashanah' })).toBeVisible()
    expect(screen.queryByText('Spring holiday')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: '360 days' }))
    expect(screen.getByText('Spring holiday')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Filter holidays' }))
    await user.click(screen.getByRole('checkbox', { name: 'Major holidays' }))
    expect(screen.queryByRole('heading', { name: 'Rosh Hashanah' })).not.toBeInTheDocument()
    expect(fetch).not.toHaveBeenCalled()
    fetch.mockRestore()
  })

  it('clears the visible list after logout and explains an expired schedule', async () => {
    vi.setSystemTime('2027-09-05T12:00:00Z')
    render(<OfflineHolidayCalendar />)
    expect(await screen.findByText(/does not cover today/)).toBeVisible()
    vi.mocked(readOfflineLibrary).mockResolvedValue({ audioEnabled: false, revision: 2, teaching: null, holidays: null })
    await act(async () => fireEvent(window, new Event(OfflineLibraryChanged)))
    expect(await screen.findByText(/No holidays saved/)).toBeVisible()
    expect(screen.queryByRole('heading', { name: 'Upcoming holidays' })).not.toBeInTheDocument()
  })

  it('searches the saved 360-day schedule and expands the range without a network connection', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch')
    const user = userEvent.setup()
    render(<OfflineHolidayCalendar />)
    await user.type(await screen.findByRole('searchbox', { name: 'Search upcoming holidays' }), 'spring')
    expect(screen.getByRole('heading', { name: 'Spring holiday' })).toBeVisible()
    expect(screen.getByRole('button', { name: '360 days' })).toHaveAttribute('aria-pressed', 'true')
    expect(fetch).not.toHaveBeenCalled()
    fetch.mockRestore()
  })

  it('handles unavailable storage as an empty offline library', async () => {
    vi.mocked(readOfflineLibrary).mockRejectedValue(new Error('unavailable'))
    render(<OfflineHolidayCalendar />)
    expect(await screen.findByText(/No holidays saved/)).toBeVisible()
  })
})
