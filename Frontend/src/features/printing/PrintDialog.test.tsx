import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AllCalendarFilters, type CalendarRange } from '../calendar/calendarTypes.ts'
import { calendarOverview, RoshHashanah } from '../calendar/calendarTestData.ts'
import { PrintAction } from './PrintAction.tsx'
import { PrintDialog } from './PrintDialog.tsx'
import { PrintAnswers, PrintTeaching } from './printTestData.ts'

afterEach(() => vi.restoreAllMocks())

async function loadPreview() {
  const frame = screen.getByTitle('Study copy preview') as HTMLIFrameElement
  fireEvent.load(frame)
  await waitFor(() => expect(frame.contentDocument?.querySelector('.print-document')).not.toBeNull())
  await waitFor(() => expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeEnabled())
  return frame
}

describe('print selection and dialog', () => {
  it('starts at the clicked answer, updates the isolated preview, and prints only the preview frame', async () => {
    const user = userEvent.setup()
    const pagePrint = vi.spyOn(window, 'print').mockImplementation(() => {})
    render(<><p>Private account detail</p><PrintDialog request={{ ...PrintAnswers, initialAnswerId: 'a2' }} onClose={vi.fn()} /></>)
    expect(screen.getByRole('dialog', { name: 'Print a study copy' })).toHaveAttribute('open')
    const frame = await loadPreview()
    expect(frame.srcdoc).toContain('Content-Security-Policy')
    expect(frame.srcdoc).toContain("default-src 'none'")
    const framePrint = vi.spyOn(frame.contentWindow!, 'print').mockImplementation(() => {})
    vi.spyOn(frame.contentWindow!, 'focus').mockImplementation(() => {})
    expect(frame.contentDocument!.body).toHaveTextContent('Make room for kindness')
    expect(frame.contentDocument!.body).not.toHaveTextContent('Private account detail')
    expect(frame.contentDocument!.body).not.toHaveTextContent('Our choices matter')
    await user.click(screen.getByRole('checkbox', { name: /What does choosing life mean/ }))
    expect(frame.contentDocument!.body).toHaveTextContent('Our choices matter')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Paper size' }), 'a4')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Print text' }), 'large')
    expect(frame.contentDocument!.head).toHaveTextContent('size: A4')
    expect(frame.contentDocument!.querySelector('.print-size-large')).not.toBeNull()
    await user.click(screen.getByRole('button', { name: 'Print / Save PDF' }))
    expect(framePrint).toHaveBeenCalledOnce()
    expect(pagePrint).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Clear selection' }))
    expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Select all' }))
    expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeEnabled()
  })

  it('opens on demand, preserves the original page, and returns focus to its trigger on cancel', async () => {
    const user = userEvent.setup()
    render(<PrintAction label="Print teaching" getRequest={() => ({ kind: 'teaching', article: PrintTeaching })} />)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    const trigger = screen.getByRole('button', { name: 'Print teaching' })
    await user.click(trigger)
    expect(await screen.findByRole('dialog')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Close print preview' })).toHaveFocus()
    fireEvent(screen.getByRole('dialog'), new Event('cancel', { cancelable: true }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
  })

  it('closes with Escape inside the preview and removes its listener on unmount', async () => {
    const onClose = vi.fn()
    const { unmount } = render(<PrintDialog request={PrintAnswers} onClose={onClose} />)
    const frame = await loadPreview()
    const frameDocument = frame.contentDocument!
    fireEvent.keyDown(frameDocument, { key: 'Escape', isComposing: true })
    fireEvent.keyDown(frameDocument, { key: 'Enter' })
    expect(onClose).not.toHaveBeenCalled()
    fireEvent.keyDown(frameDocument, { key: 'Escape' })
    expect(onClose).toHaveBeenCalledOnce()
    unmount()
    fireEvent.keyDown(frameDocument, { key: 'Escape' })
    expect(onClose).toHaveBeenCalledOnce()
  })

  it('keeps source credits when excerpts are disabled and provides keyboard-operable mobile tabs', async () => {
    const user = userEvent.setup()
    render(<PrintDialog request={{ kind: 'teaching', article: PrintTeaching }} onClose={vi.fn()} />)
    const frame = await loadPreview()
    expect(screen.getByRole('checkbox', { name: 'Source excerpts' })).toBeChecked()
    await user.click(screen.getByRole('checkbox', { name: 'Source excerpts' }))
    expect(frame.contentDocument!.body).not.toHaveTextContent('Choose life, that thou mayest live.')
    expect(frame.contentDocument!.body).toHaveTextContent('Public Domain')
    screen.getByRole('tab', { name: 'Options' }).focus()
    await user.keyboard('{ArrowRight}')
    expect(screen.getByRole('tab', { name: 'Preview' })).toHaveFocus()
    expect(screen.getByRole('tab', { name: 'Preview' })).toHaveAttribute('aria-selected', 'true')
  })

  it('restores the supplied print trigger even if loading moved focus elsewhere', () => {
    const trigger = document.createElement('button')
    document.body.append(trigger)
    const { unmount } = render(<PrintDialog request={PrintAnswers} returnFocusTo={trigger} onClose={vi.fn()} />)
    expect(screen.getByRole('button', { name: 'Close print preview' })).toHaveFocus()
    unmount()
    expect(trigger).toHaveFocus()
    trigger.remove()
  })

  it('honors calendar search and range, then allows other holidays without loading an API', async () => {
    const user = userEvent.setup()
    const spring = { ...RoshHashanah, id: 'spring', title: 'Spring holiday', startDate: '2027-03-25', beginningDate: '2027-03-24', endDate: '2027-03-26' }
    render(<PrintDialog request={{ kind: 'calendar', calendar: { startDate: '2026-09-10', days: 90, events: [RoshHashanah, spring], filters: AllCalendarFilters, search: 'spring', inIsrael: false, savedNote: 'Saved schedule; reconnect for local times.' } }} onClose={vi.fn()} />)
    const frame = await loadPreview()
    expect(screen.getByRole('combobox', { name: 'Calendar range' })).toHaveValue('360')
    expect(frame.contentDocument!.body).toHaveTextContent('Spring holiday')
    expect(frame.contentDocument!.body).not.toHaveTextContent('Rosh Hashanah')
    expect(frame.contentDocument!.body).toHaveTextContent('Saved schedule; reconnect for local times.')
    await user.click(screen.getByRole('button', { name: 'Include other holidays' }))
    expect(screen.getByRole('combobox', { name: 'Calendar range' })).toHaveValue('90')
    expect(frame.contentDocument!.body).toHaveTextContent('Rosh Hashanah')
    expect(frame.contentDocument!.body).not.toHaveTextContent('Spring holiday')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Calendar range' }), '360')
    expect(frame.contentDocument!.body).toHaveTextContent('Spring holiday')
  })

  it('reports print-dialog errors without closing or discarding the selection', async () => {
    const user = userEvent.setup()
    const close = vi.fn()
    render(<PrintDialog request={PrintAnswers} onClose={close} />)
    const frame = await loadPreview()
    vi.spyOn(frame.contentWindow!, 'focus').mockImplementation(() => {})
    vi.spyOn(frame.contentWindow!, 'print').mockImplementation(() => { throw new Error('Unavailable') })
    await user.click(screen.getByRole('button', { name: 'Print / Save PDF' }))
    expect(screen.getByRole('alert')).toHaveTextContent('The browser could not open its print dialog')
    expect(screen.getByRole('checkbox', { name: /What does choosing life mean/ })).toBeChecked()
    expect(close).not.toHaveBeenCalled()
  })

  it('prints all holidays in each range or none without individual holiday checkboxes', async () => {
    const user = userEvent.setup()
    const events = [RoshHashanah, { ...RoshHashanah, id: 'winter', title: 'Winter holiday', startDate: '2027-01-20', beginningDate: '2027-01-19', endDate: '2027-01-20', occurrences: [] }, { ...RoshHashanah, id: 'spring', title: 'Spring holiday', startDate: '2027-03-25', beginningDate: '2027-03-24', endDate: '2027-03-25', occurrences: [] }]
    render(<PrintDialog request={{ kind: 'calendar', calendar: { startDate: '2026-09-10', days: 90, events, filters: AllCalendarFilters, search: '', inIsrael: false } }} onClose={vi.fn()} />)
    const frame = await loadPreview()
    expect(screen.queryByRole('checkbox', { name: /Rosh Hashanah|Winter holiday|Spring holiday/ })).not.toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'All holidays in this range (1)' })).toBeChecked()

    for (const [range, count] of [[90, 1], [180, 2], [360, 3]] as [CalendarRange, number][]) {
      await user.selectOptions(screen.getByRole('combobox', { name: 'Calendar range' }), String(range))
      expect(screen.getByRole('radio', { name: `All holidays in this range (${count})` })).toBeChecked()
      expect(frame.contentDocument!.querySelectorAll('.print-holiday')).toHaveLength(count)
    }
    await user.click(screen.getByRole('radio', { name: 'None' }))
    expect(frame.contentDocument!.querySelectorAll('.print-holiday')).toHaveLength(0)
    expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeDisabled()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Calendar range' }), '90')
    expect(screen.getByRole('radio', { name: 'None' })).toBeChecked()
    expect(frame.contentDocument!.querySelectorAll('.print-holiday')).toHaveLength(0)
    await user.click(screen.getByRole('radio', { name: 'All holidays in this range (1)' }))
    expect(frame.contentDocument!.querySelectorAll('.print-holiday')).toHaveLength(1)
    expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeEnabled()
  })

  it('allows summary-only calendar printing and handles an empty filtered range', async () => {
    const user = userEvent.setup()
    render(<PrintDialog request={{ kind: 'calendar', calendar: { startDate: '2026-09-10', days: 90, events: [RoshHashanah], filters: { ...AllCalendarFilters, majorHolidays: false }, search: '', inIsrael: false, overview: calendarOverview() } }} onClose={vi.fn()} />)
    const frame = await loadPreview()
    expect(screen.getByRole('radio', { name: 'All holidays in this range (0)' })).toBeChecked()
    expect(screen.getByText('No holidays match this range and your filters.')).toBeVisible()
    await user.click(screen.getByRole('radio', { name: 'None' }))
    expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeEnabled()
    expect(frame.contentDocument!.querySelector('.print-calendar-summary')).not.toBeNull()
    expect(frame.contentDocument!.querySelector('.print-holiday')).toBeNull()
    await user.click(screen.getByRole('checkbox', { name: 'Today and this Shabbat' }))
    await user.click(screen.getByRole('checkbox', { name: 'Local times and location' }))
    expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeDisabled()
  })

  it('toggles teaching references and their appendix while retaining the text and restoring excerpts', async () => {
    const user = userEvent.setup()
    render(<PrintDialog request={{ kind: 'teaching', article: PrintTeaching }} onClose={vi.fn()} />)
    const frame = await loadPreview()
    expect(screen.getByRole('checkbox', { name: 'Source references' })).toBeChecked()
    expect(frame.contentDocument!.querySelectorAll('.print-citation')).toHaveLength(2)

    await user.click(screen.getByRole('checkbox', { name: 'Source references' }))
    expect(frame.contentDocument!.querySelector('.print-citation')).toBeNull()
    expect(frame.contentDocument!.querySelector('.print-sources')).toBeNull()
    expect(frame.contentDocument!.body).toHaveTextContent('God’s invitation to choose life.')
    expect(screen.getByRole('checkbox', { name: 'Source excerpts' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Print / Save PDF' })).toBeEnabled()

    await user.click(screen.getByRole('checkbox', { name: 'Source references' }))
    expect(frame.contentDocument!.querySelectorAll('.print-citation')).toHaveLength(2)
    expect(frame.contentDocument!.querySelector('.print-sources')).not.toBeNull()
    expect(screen.getByRole('checkbox', { name: 'Source excerpts' })).toBeEnabled()
    expect(screen.getByRole('checkbox', { name: 'Source excerpts' })).toBeChecked()
  })
})
