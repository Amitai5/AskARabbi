import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AssistantMessage } from '../conversations/AssistantMessage.tsx'
import { WeeklyDvarTorahPage } from '../dvarTorah/WeeklyDvarTorahPage.tsx'
import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'
import { createDemoApplicationClients } from '../../test/demoApplicationClients.ts'
import { cacheReadingPreferences, DefaultReadingPreferences } from './readingPreferences.ts'
import { ReadingPreferencesProvider } from './ReadingPreferencesProvider.tsx'
import { FocusedReadingProvider, FocusedReadingToolbar } from './FocusedReading.tsx'
import { useFocusedReading } from './focusedReadingContext.ts'
import { StrictMode, type ReactNode } from 'react'

const longText = 'A teaching invites us to study carefully and ask a thoughtful question. '.repeat(20)
const message = { id: 'answer-1', role: 'Assistant' as const, content: longText, createdAtUtc: '2026-09-11T12:00:00Z' }

beforeEach(() => {
  localStorage.clear()
  Object.defineProperty(HTMLElement.prototype, 'scrollTo', { configurable: true, value: vi.fn() })
})

function FocusLayout({ children }: { children: ReactNode }) {
  const { target } = useFocusedReading()
  return <main className={target ? 'focused-reading' : ''}><FocusedReadingToolbar /><section data-reading-scroll>{children}</section></main>
}

describe('focused reading', () => {
  it('focuses a long answer on demand and restores keyboard focus on exit', async () => {
    render(<FocusedReadingProvider><FocusLayout><AssistantMessage message={message} selectedSourceNumber={null} onSelectSource={vi.fn()} /></FocusLayout></FocusedReadingProvider>)
    const trigger = screen.getByRole('button', { name: 'Focus answer' })
    fireEvent.click(trigger)
    expect(screen.getByRole('main')).toHaveClass('focused-reading')
    expect(screen.getByRole('button', { name: 'Exit focused reading' })).toHaveFocus()
    expect(document.querySelector('[data-reading-target]')).toHaveAttribute('data-reading-focused', 'true')
    fireEvent.click(screen.getByRole('button', { name: 'Exit focused reading' }))
    await waitFor(() => expect(trigger).toHaveFocus())
    expect(screen.getByRole('main')).not.toHaveClass('focused-reading')
  })

  it('enters by default only for the latest eligible long answer and lets Escape exit', async () => {
    const client = createDemoApplicationClients().conversationSettingsClient
    client.getReadingPreferences = async () => ({ ...DefaultReadingPreferences, focusLongContent: true })
    render(<ReadingPreferencesProvider userId="reader" client={client}><FocusedReadingProvider><FocusLayout>
      <AssistantMessage message={{ ...message, id: 'older' }} selectedSourceNumber={null} onSelectSource={vi.fn()} />
      <AssistantMessage message={message} autoFocusEligible selectedSourceNumber={null} onSelectSource={vi.fn()} />
    </FocusLayout></FocusedReadingProvider></ReadingPreferencesProvider>)
    await screen.findByRole('button', { name: 'Exit focused reading' })
    expect(document.querySelector('[data-reading-focused="true"]')).toHaveAttribute('data-reading-target', 'answer:answer-1')
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(screen.queryByRole('button', { name: 'Exit focused reading' })).not.toBeInTheDocument()
    expect(screen.getByRole('main')).not.toHaveClass('focused-reading')
  })

  it('does not offer focus for short answers', () => {
    render(<FocusedReadingProvider><AssistantMessage message={{ ...message, content: 'A short reply.' }} selectedSourceNumber={null} onSelectSource={vi.fn()} /></FocusedReadingProvider>)
    expect(screen.queryByRole('button', { name: 'Focus answer' })).not.toBeInTheDocument()
  })

  it('honors cached automatic focus through Strict Mode effect replay', async () => {
    const preferences = { ...DefaultReadingPreferences, focusLongContent: true }
    cacheReadingPreferences('strict-reader', preferences, false)
    const client = createDemoApplicationClients().conversationSettingsClient
    client.getReadingPreferences = async () => preferences
    render(<StrictMode><ReadingPreferencesProvider userId="strict-reader" client={client}><FocusedReadingProvider><FocusLayout><AssistantMessage message={message} autoFocusEligible selectedSourceNumber={null} onSelectSource={vi.fn()} /></FocusLayout></FocusedReadingProvider></ReadingPreferencesProvider></StrictMode>)

    expect(await screen.findByRole('button', { name: 'Exit focused reading' })).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Exit focused reading' }))
    expect(screen.queryByRole('button', { name: 'Exit focused reading' })).not.toBeInTheDocument()
  })

  it('keeps the same teaching audio element and timing request when entering and exiting focus', async () => {
    const week = { weekKey: 'diaspora:2026-09-12', shabbatDate: '2026-09-12', hebrewDate: '1 Tishrei 5787', parashah: null, holiday: 'Rosh Hashanah', inIsrael: false }
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {})
    const pause = vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
    const client: DvarTorahClient = {
      getCurrent: async () => ({ currentWeek: week, isCurrentWeek: true, dvarTorah: { title: 'A thoughtful question', body: longText, week, tags: [], sources: [], centralTeaching: 'Careful study', torahGroundingPercent: 100, generatedAtUtc: '2026-09-10T12:00:00Z', publishedAtUtc: '2026-09-10T12:00:00Z', audio: { version: 'v1', voice: 'Andrew', audioUrl: '', timingsUrl: '', durationMs: 240000 } } }),
      getArchive: async () => ({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
      getArchived: vi.fn(),
      getAudioUrl: () => 'https://example.test/audio.mp3',
      getAudioTimings: vi.fn().mockResolvedValue(null),
    }
    const { unmount } = render(<FocusedReadingProvider><FocusLayout><WeeklyDvarTorahPage client={client} /></FocusLayout></FocusedReadingProvider>)
    const trigger = await screen.findByRole('button', { name: 'Focus teaching' })
    const audio = screen.getByLabelText('Dvar Torah recording')
    const calls = client.getAudioTimings as ReturnType<typeof vi.fn>
    await waitFor(() => expect(calls).toHaveBeenCalledTimes(1))
    fireEvent.click(trigger)
    expect(screen.getByLabelText('Dvar Torah recording')).toBe(audio)
    fireEvent.click(screen.getByRole('button', { name: 'Exit focused reading' }))
    expect(screen.getByLabelText('Dvar Torah recording')).toBe(audio)
    expect(calls).toHaveBeenCalledTimes(1)
    expect(pause).not.toHaveBeenCalled()
    unmount()
    vi.restoreAllMocks()
  })
})
