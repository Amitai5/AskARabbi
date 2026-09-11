import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CalendarPage } from './CalendarPage.tsx'
import { calendarOverview, fakeCalendarClient, RoshHashanah } from './calendarTestData.ts'

describe('Jewish Calendar', () => {
  beforeEach(() => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() })
  })
  afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers() })

  it('loads 90 days, focuses the heading, isolates Hebrew and expands all holiday days', async () => {
    const client = fakeCalendarClient()
    const user = userEvent.setup()
    const dvar = vi.fn()
    render(<CalendarPage client={client} onOpenDvarTorah={dvar} />)
    expect(screen.getByRole('heading', { name: 'Jewish Calendar' })).toHaveFocus()
    expect(await screen.findByText('Thursday, September 10, 2026')).toBeVisible()
    expect(client.getOverview).toHaveBeenCalledWith(90, expect.any(AbortSignal))
    expect(screen.getByText('כ״ח אלול תשפ״ו')).toHaveAttribute('dir', 'rtl')
    expect(screen.getByText('כ״ח אלול תשפ״ו').tagName).toBe('BDI')
    expect(screen.getByText(/Daytime Hebrew date/)).toBeVisible()
    expect(screen.getByText('Festival reading · Rosh Hashanah')).not.toBeVisible()
    expect(screen.getByRole('button', { name: 'Next 90 days' })).toHaveAttribute('aria-pressed', 'true')
    await user.click(screen.getByRole('button', { name: 'View dates & meaning' }))
    expect(screen.getByRole('heading', { name: 'Rosh Hashanah', level: 3 }).closest('details')).toHaveAttribute('open')
    expect(screen.queryByRole('link', { name: /Read about/ })).not.toBeInTheDocument()
    expect(screen.getByText(/— Rosh Hashana II/)).toBeVisible()
    await user.click(screen.getByText('This Shabbat'))
    expect(screen.getByText('Festival reading · Rosh Hashanah')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Read Dvar Torah' }))
    expect(dvar).toHaveBeenCalledOnce()
    expect(screen.getByText(/publication’s own date and cycle apply/)).toBeVisible()
    expect(screen.getByRole('link', { name: 'Hebcal' })).toHaveAttribute('href', 'https://www.hebcal.com/home/developer-apis')
  })

  it('orders Today, collapsed Shabbat, next holiday, and upcoming holidays without hiding the agenda', async () => {
    const client = fakeCalendarClient(calendarOverview({ localTimes: [{ title: 'Candle-lighting', date: '2026-09-11', at: '2026-09-11T18:00:00+03:00', timeZone: 'Asia/Jerusalem', location: 'Jerusalem', context: 'Before Shabbat' }] }))
    const user = userEvent.setup()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    const today = await screen.findByRole('heading', { name: 'Today' })
    const shabbat = screen.getByText('This Shabbat')
    const nextHoliday = screen.getByText('Next holiday')
    const upcoming = screen.getByRole('heading', { name: 'Upcoming holidays' })
    const disclosure = shabbat.closest('details')

    expect(today.compareDocumentPosition(shabbat) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(shabbat.compareDocumentPosition(nextHoliday) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(nextHoliday.compareDocumentPosition(upcoming) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(disclosure).not.toHaveAttribute('open')
    expect(within(shabbat.closest('summary')!).getByText('September 12, 2026')).toBeVisible()
    expect(screen.getByText('Local times')).not.toBeVisible()
    expect(screen.getByText('6:00 PM')).not.toBeVisible()
    expect(upcoming).toBeVisible()

    await user.click(shabbat)
    expect(disclosure).toHaveAttribute('open')
    expect(screen.getByRole('heading', { name: 'Local times' })).toBeVisible()
    expect(screen.getByText('6:00 PM')).toBeVisible()
    await user.click(shabbat)
    expect(disclosure).not.toHaveAttribute('open')
    expect(screen.getByText('6:00 PM')).not.toBeVisible()
    expect(nextHoliday).toBeVisible()
    expect(upcoming).toBeVisible()
    expect(client.getOverview).toHaveBeenCalledOnce()
  })

  it('switches ranges and persists optional filters before refreshing', async () => {
    const client = fakeCalendarClient()
    const user = userEvent.setup()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    await user.click(await screen.findByRole('button', { name: '30 days' }))
    await waitFor(() => expect(client.getOverview).toHaveBeenLastCalledWith(30, expect.any(AbortSignal)))
    await user.click(await screen.findByRole('checkbox', { name: 'Modern observances' }))
    expect(client.updatePreferences).toHaveBeenCalledWith(expect.objectContaining({ modernObservances: true, majorHolidays: true, fastDays: true }))
    await waitFor(() => expect(screen.getByRole('checkbox', { name: 'Modern observances' })).toBeChecked())
    await user.click(screen.getByRole('button', { name: '365 days' }))
    await waitFor(() => expect(client.getOverview).toHaveBeenLastCalledWith(365, expect.any(AbortSignal)))
  })

  it('keeps the loaded agenda visible when its already selected range is clicked', async () => {
    const client = fakeCalendarClient()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    fireEvent.click(await screen.findByRole('button', { name: 'Next 90 days' }))
    expect(screen.getByText('Next holiday')).toBeVisible()
    expect(screen.queryByText('Loading calendar…')).not.toBeInTheDocument()
    expect(client.getOverview).toHaveBeenCalledOnce()
  })

  it('identifies cached solar data even when the holiday schedule is fresh', async () => {
    const data = calendarOverview()
    data.today.solarData = { isAvailable: true, isStale: true, fetchedAtUtc: '2026-09-10T12:00:00Z', message: 'Using saved solar times for this location and date.' }
    render(<CalendarPage client={fakeCalendarClient(data)} onOpenDvarTorah={vi.fn()} />)
    expect(await screen.findByText('Using saved solar times for this location and date.')).toBeVisible()
    expect(screen.getByText(/Last updated/)).toBeVisible()
  })

  it('opens Personalization instead of exposing a separate calendar settings form', async () => {
    const client = fakeCalendarClient()
    const user = userEvent.setup()
    const openPersonalization = vi.fn()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} onOpenPersonalization={openPersonalization} />)
    await screen.findByText('Next holiday')
    await user.click(screen.getByRole('button', { name: 'Edit location in Personalization' }))
    expect(openPersonalization).toHaveBeenCalledOnce()
    expect(screen.queryByRole('button', { name: 'Location & preferences' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Havdalah calculation')).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/Show local candle/)).not.toBeInTheDocument()
    expect(client.getPreferences).not.toHaveBeenCalled()
    expect(client.updatePreferences).not.toHaveBeenCalled()
  })

  it('rolls back an optimistic category toggle when saving fails', async () => {
    const client = fakeCalendarClient()
    vi.mocked(client.updatePreferences).mockRejectedValue(new Error('Filters could not be saved'))
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Special Shabbatot' }))
    expect(screen.getByRole('checkbox', { name: 'Special Shabbatot' })).toBeChecked()
    expect(await screen.findByRole('alert')).toHaveTextContent('Filters could not be saved')
    expect(screen.getByRole('checkbox', { name: 'Special Shabbatot' })).not.toBeChecked()
  })

  it('directs an account with no location to Personalization from local times', async () => {
    const client = fakeCalendarClient()
    const user = userEvent.setup()
    const openPersonalization = vi.fn()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} onOpenPersonalization={openPersonalization} />)
    await screen.findByText('Next holiday')
    await user.click(screen.getByText('This Shabbat'))
    await user.click(screen.getByRole('button', { name: 'Set location in Personalization' }))
    expect(openPersonalization).toHaveBeenCalledOnce()
    expect(client.updatePreferences).not.toHaveBeenCalled()
  })

  it('shows ongoing events, stale provenance and offset-aware local times', async () => {
    const event = { ...RoshHashanah, isOngoing: true }
    const data = calendarOverview({ events: [event], highlight: event, holidays: { isAvailable: true, isStale: true, fetchedAtUtc: '2026-09-10T12:00:00Z', message: 'Showing a saved schedule.' },
      localTimes: [{ title: 'Candle-lighting', date: '2026-09-11', at: '2026-09-11T18:00:00+03:00', timeZone: 'Asia/Jerusalem', location: 'Jerusalem', context: 'Before Shabbat' }] })
    render(<CalendarPage client={fakeCalendarClient(data)} onOpenDvarTorah={vi.fn()} />)
    expect(await screen.findAllByText('Happening now')).toHaveLength(2)
    expect(screen.getByText(/Last updated/)).toBeVisible()
    fireEvent.click(screen.getByText('This Shabbat'))
    expect(screen.getByText('6:00 PM')).toHaveAttribute('datetime', '2026-09-11T18:00:00+03:00')
    expect(screen.getByText(/September 11, 2026 · Jerusalem/)).toBeVisible()
  })

  it('distinguishes a partial provider failure from an empty filtered agenda', async () => {
    const client = fakeCalendarClient(calendarOverview({ events: [], highlight: null, holidays: { isAvailable: false, isStale: false, fetchedAtUtc: null, message: 'Some holiday dates are temporarily unavailable.' } }))
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    expect(await screen.findByText('Some holiday dates are temporarily unavailable.')).toBeVisible()
    expect(screen.getByText('Thursday, September 10, 2026')).toBeVisible()
    expect(screen.queryByText(/No events in this range/)).not.toBeInTheDocument()
    vi.mocked(client.getOverview).mockResolvedValue(calendarOverview({ events: [], highlight: null }))
    fireEvent.click(screen.getByRole('button', { name: 'Refresh data' }))
    expect(await screen.findByText(/No events in this range/)).toBeVisible()
  })

  it('recovers from an initial error and aborts work when unmounted', async () => {
    const client = fakeCalendarClient()
    vi.mocked(client.getOverview).mockRejectedValueOnce(new Error('Calendar unavailable'))
    const { unmount } = render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Calendar unavailable')
    fireEvent.click(screen.getByRole('button', { name: 'Retry calendar' }))
    expect(await screen.findByText('Next holiday')).toBeVisible()
    const signal = vi.mocked(client.getOverview).mock.calls.at(-1)?.[1]
    unmount()
    expect(signal?.aborted).toBe(true)
  })

  it('refreshes once at a boundary and again on focus only after the next boundary expires', async () => {
    vi.useFakeTimers()
    vi.setSystemTime('2026-09-10T12:00:00Z')
    const client = fakeCalendarClient(calendarOverview({ nextRefreshAtUtc: '2026-09-10T12:00:10Z' }))
    vi.mocked(client.getOverview).mockResolvedValueOnce(calendarOverview({ nextRefreshAtUtc: '2026-09-10T12:00:10Z' })).mockResolvedValue(calendarOverview({ nextRefreshAtUtc: '2026-09-10T13:00:00Z' }))
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    await act(async () => { await Promise.resolve() })
    fireEvent.focus(window)
    expect(client.getOverview).toHaveBeenCalledTimes(1)
    await act(async () => { await vi.advanceTimersByTimeAsync(10_100) })
    expect(client.getOverview).toHaveBeenCalledTimes(2)
    vi.setSystemTime('2026-09-10T13:01:00Z')
    await act(async () => { fireEvent.focus(window) })
    expect(client.getOverview).toHaveBeenCalledTimes(3)
  })

  it('makes no request offline and refreshes when connectivity returns', async () => {
    const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    const client = fakeCalendarClient()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    expect(screen.getByText(/Reconnect for current calendar/)).toBeVisible()
    expect(client.getOverview).not.toHaveBeenCalled()
    online.mockReturnValue(true)
    fireEvent.online(window)
    expect(await screen.findByText('Next holiday')).toBeVisible()
  })
})
