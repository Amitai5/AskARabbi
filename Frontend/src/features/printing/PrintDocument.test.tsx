import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AllCalendarFilters } from '../calendar/calendarTypes.ts'
import { calendarOverview, Jerusalem, RoshHashanah } from '../calendar/calendarTestData.ts'
import { PrintDocument } from './PrintDocument.tsx'
import { DefaultPrintOptions, type PrintOptions, type PrintRequest } from './printTypes.ts'
import { PrintAnswers, PrintTeaching } from './printTestData.ts'

function documentFor(request: PrintRequest, selectedIds = new Set(['a1', 'a2']), options: Partial<PrintOptions> = {}) {
  return <PrintDocument request={request} options={{ ...DefaultPrintOptions, ...options }} selectedIds={selectedIds} events={request.kind === 'calendar' ? request.calendar.events : []} days={90} />
}

describe('study copy content', () => {
  it('prints only selected answers with their questions, citations, excerpts, and a linked book mark', () => {
    const { container } = render(documentFor(PrintAnswers, new Set(['a2'])))
    expect(screen.getByText('How can we put this into practice?')).toBeVisible()
    expect(screen.queryByText(PrintAnswers.answers[0].question!)).not.toBeInTheDocument()
    expect(screen.queryByText(/Our choices matter/)).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Source 1' })).toHaveAttribute('href', '#answer-1-source-1')
    expect(container.querySelector('#answer-1-source-1')).toHaveTextContent('Choose life, that thou mayest live.')
    expect(screen.getByText('דברים')).toHaveAttribute('dir', 'rtl')
    expect(screen.getByText(/Public Domain/)).toBeVisible()
    expect(screen.getByRole('link', { name: 'askarabbi.ai/terms-of-service' })).toHaveAttribute('href', 'https://askarabbi.ai/terms-of-service')
    expect(screen.getByRole('link', { name: 'askarabbi.ai/privacy-policy' })).toHaveAttribute('href', 'https://askarabbi.ai/privacy-policy')
    const watermark = container.querySelector('.print-watermark')!
    expect(watermark.closest('tfoot')).not.toBeNull()
    expect(container.querySelector('a')).toHaveTextContent('AskRabbi')
    expect(within(watermark as HTMLElement).getByRole('link', { name: 'AskARabbi.ai' })).toHaveAttribute('href', 'https://askarabbi.ai')
    expect(watermark.querySelector('svg')).not.toBeNull()
  })

  it('keeps repeated source numbers scoped to each answer in conversation order', () => {
    const { container } = render(documentFor(PrintAnswers, new Set(['a2', 'a1']), { separateAnswers: true }))
    expect(screen.getAllByRole('link', { name: 'Source 1' }).map(link => link.getAttribute('href'))).toEqual(['#answer-1-source-1', '#answer-2-source-1'])
    expect(container.querySelectorAll('.print-new-page')).toHaveLength(1)
    expect(container.querySelectorAll('.print-answer')[0]).toHaveTextContent('Our choices matter')
  })

  it('keeps attribution when questions and excerpts are omitted, and offers notes and context independently', () => {
    const { rerender } = render(documentFor(PrintAnswers, new Set(['a1']), { includeQuestions: false, includeExcerpts: false, includeContext: true, includeNotes: true }))
    expect(screen.queryByText(PrintAnswers.answers[0].question!)).not.toBeInTheDocument()
    expect(screen.queryByText('Choose life, that thou mayest live.')).not.toBeInTheDocument()
    expect(screen.queryByText('Surrounding passage for further study.')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Sources' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'https://example.test/edition' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'https://example.test/edition' })).toHaveAttribute('target', '_blank')
    expect(screen.getByRole('link', { name: 'https://example.test/edition' })).toHaveAttribute('rel', 'noopener noreferrer')
    expect(screen.getByRole('heading', { name: 'Notes for study & discussion' })).toBeVisible()
    rerender(documentFor(PrintAnswers, new Set(['a1']), { includeContext: true }))
    expect(screen.getByText('Surrounding passage for further study.')).toBeVisible()
  })

  it('normalizes teaching typography and renders teaching citations, dates, reflection, and simple formatting', () => {
    const { container } = render(documentFor({ kind: 'teaching', article: PrintTeaching }))
    expect(screen.getByRole('heading', { name: 'Choosing Life', level: 1 })).toBeVisible()
    expect(screen.getByText(/God’s invitation/)).toBeVisible()
    expect(screen.getByText(/23 Elul, 5786/)).toHaveTextContent('Parashat Nitzavim')
    expect(screen.getAllByRole('link', { name: 'Source 1' })).toHaveLength(2)
    expect(screen.getByRole('heading', { name: 'For reflection' })).toBeVisible()
    expect(container.querySelector('strong')).toHaveTextContent('Study together.')
    expect(screen.getByText('Listen').tagName).toBe('LI')
    expect(container).not.toHaveTextContent('[TA]')
  })

  it('treats markup-like source content as text and removes dangerous source links', () => {
    const article = { ...PrintTeaching, body: '<img src=x onerror=alert(1)> [MISSING]', sources: [{ ...PrintTeaching.sources[0], sourceUrl: 'javascript:alert(1)' }] }
    const { container } = render(documentFor({ kind: 'teaching', article }))
    expect(container.querySelector('img')).toBeNull()
    expect(screen.getByText('<img src=x onerror=alert(1)> [MISSING]')).toBeVisible()
    expect(container.querySelector('a[href^="javascript:"]')).toBeNull()
  })

  it('omits only known teaching references, retaining quotations, ordinary brackets, and formatting', () => {
    const article = { ...PrintTeaching, body: 'The Torah says, “Choose life” [TA] [TA].\n\n**Study [TA] together.**\n\nKeep this [explanation] and the year [2026].\n\n- Listen [TA]\n- Reflect', centralTeaching: 'Choose life [TA].' }
    const { container } = render(documentFor({ kind: 'teaching', article }, new Set(), { includeSourceReferences: false }))
    expect(screen.getByText('The Torah says, “Choose life”.')).toBeVisible()
    expect(screen.getByText('Keep this [explanation] and the year [2026].')).toBeVisible()
    expect(screen.getByText('Study together.').tagName).toBe('STRONG')
    expect(screen.getByText('Listen').tagName).toBe('LI')
    expect(container.querySelector('.print-takeaway')).toHaveTextContent('Choose life.')
    expect(container.querySelector('.print-citation')).toBeNull()
    expect(container.querySelector('.print-sources')).toBeNull()
    expect(container).not.toHaveTextContent('[TA]')
    expect(container.querySelector('.print-watermark')).toHaveTextContent('AskARabbi.ai')
  })

  it('keeps conversation source references even when the teaching-only option is disabled', () => {
    render(documentFor(PrintAnswers, new Set(['a1']), { includeSourceReferences: false }))
    expect(screen.getByRole('link', { name: 'Source 1' })).toBeVisible()
    expect(screen.getByRole('heading', { name: 'Sources & excerpts' })).toBeVisible()
  })

  it('prints calendar dates, only chosen holidays, location-aware times, warnings, and calendar credits', () => {
    const overview = calendarOverview({ localTimes: [{ title: 'Candle-lighting', date: '2026-09-11', at: '2026-09-11T18:00:00+03:00', timeZone: 'Asia/Jerusalem', location: 'Jerusalem', context: 'Before Shabbat' }], timing: { isAvailable: true, isStale: true, message: 'Cached local times.', fetchedAtUtc: '2026-09-10T12:00:00Z' } })
    overview.preferences.location = Jerusalem
    const request: PrintRequest = { kind: 'calendar', calendar: { startDate: '2026-09-10', days: 90, search: '', filters: AllCalendarFilters, inIsrael: false, events: [RoshHashanah, { ...RoshHashanah, id: 'not-selected', title: 'Do not print this holiday' }], overview } }
    const { rerender } = render(documentFor(request, new Set([RoshHashanah.id])))
    expect(screen.getByRole('heading', { name: 'Today' })).toBeVisible()
    expect(screen.getByText('6:00 PM')).toBeVisible()
    expect(screen.getByText(/Cached local times/)).toBeVisible()
    expect(screen.queryByText('Do not print this holiday')).not.toBeInTheDocument()
    expect(screen.getByText(/Rosh Hashana II/)).toBeVisible()
    expect(screen.getByRole('link', { name: 'CC BY 4.0' })).toBeVisible()
    rerender(documentFor(request, new Set(), { includeCalendarSummary: false, includeLocalTimes: false }))
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Today' })).not.toBeInTheDocument()
    expect(screen.getByText('No holidays selected for this copy.')).toBeVisible()
  })
})
