import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CalendarPage } from './CalendarPage.tsx'
import { calendarOverview, fakeCalendarClient, Jerusalem, RoshHashanah } from './calendarTestData.ts'

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
    expect(screen.getByText('Festival reading · Rosh Hashanah')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Next 90 days' })).toHaveAttribute('aria-pressed', 'true')
    await user.click(screen.getByRole('button', { name: 'View dates & meaning' }))
    expect(screen.getByRole('link', { name: 'Read about Rosh Hashanah' }).closest('details')).toHaveAttribute('open')
    expect(screen.getByText(/— Rosh Hashana II/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Read Dvar Torah' }))
    expect(dvar).toHaveBeenCalledOnce()
    expect(screen.getByText(/publication’s own date and cycle apply/)).toBeVisible()
    expect(screen.getByRole('link', { name: 'Hebcal' })).toHaveAttribute('href', 'https://www.hebcal.com/home/developer-apis')
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
    expect(screen.getByText('Coming next')).toBeVisible()
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

  it('saves a searched city, explicit Israel cycle and timing convention then restores focus', async () => {
    const client = fakeCalendarClient()
    const user = userEvent.setup()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    await screen.findByText('Coming next')
    await user.click(screen.getByRole('button', { name: 'Location & preferences' }))
    expect(screen.getByRole('heading', { name: 'Location & preferences' })).toHaveFocus()
    await user.type(screen.getByLabelText('Search supported cities'), 'Jerusalem')
    expect(screen.queryByRole('option', { name: 'New York, United States' })).not.toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('City'), Jerusalem.id)
    expect(screen.getByLabelText('Holiday and reading schedule')).toHaveValue('diaspora')
    await user.selectOptions(screen.getByLabelText('Holiday and reading schedule'), 'israel')
    await user.selectOptions(screen.getByLabelText('Havdalah calculation'), '72')
    await user.click(screen.getByRole('button', { name: 'Save calendar preferences' }))
    expect(client.updatePreferences).toHaveBeenCalledWith(expect.objectContaining({ location: Jerusalem, inIsrael: true, havdalah: 'fixed', havdalahMinutes: 72, candleLightingMinutes: null }))
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Location & preferences' })).not.toBeInTheDocument())
    expect(screen.getByRole('button', { name: 'Location & preferences' })).toHaveFocus()
    expect(await screen.findByText('Jerusalem, Israel · Israel')).toBeVisible()
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

  it('resolves a ZIP on save and keeps the form editable after a failed save', async () => {
    const client = fakeCalendarClient()
    vi.mocked(client.updatePreferences).mockRejectedValueOnce(new Error('Location lookup unavailable.'))
    const user = userEvent.setup()
    render(<CalendarPage client={client} onOpenDvarTorah={vi.fn()} />)
    await screen.findByText('Coming next')
    await user.click(screen.getByRole('button', { name: 'Location & preferences' }))
    await user.selectOptions(screen.getByLabelText('Location type'), 'zip')
    const form = screen.getByRole('region', { name: 'Location & preferences' })
    await user.type(within(form).getByRole('textbox'), '10001')
    await user.click(screen.getByRole('button', { name: 'Save calendar preferences' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Location lookup unavailable.')
    expect(within(form).getByRole('textbox')).toHaveValue('10001')
    await user.click(screen.getByRole('button', { name: 'Save calendar preferences' }))
    await waitFor(() => expect(client.updatePreferences).toHaveBeenCalledTimes(2))
    expect(client.updatePreferences).toHaveBeenLastCalledWith(expect.objectContaining({ location: expect.objectContaining({ kind: 'zip', id: '10001' }) }))
  })

  it('shows ongoing events, stale provenance and offset-aware local times', async () => {
    const event = { ...RoshHashanah, isOngoing: true }
    const data = calendarOverview({ events: [event], highlight: event, holidays: { isAvailable: true, isStale: true, fetchedAtUtc: '2026-09-10T12:00:00Z', message: 'Showing a saved schedule.' },
      localTimes: [{ title: 'Candle-lighting', date: '2026-09-11', at: '2026-09-11T18:00:00+03:00', timeZone: 'Asia/Jerusalem', location: 'Jerusalem', context: 'Before Shabbat' }] })
    render(<CalendarPage client={fakeCalendarClient(data)} onOpenDvarTorah={vi.fn()} />)
    expect(await screen.findAllByText('Happening now')).toHaveLength(2)
    expect(screen.getByText(/Last updated/)).toBeVisible()
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
    expect(await screen.findByText('Coming next')).toBeVisible()
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
    expect(await screen.findByText('Coming next')).toBeVisible()
  })
})
