import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AssistantMessage } from '../conversations/AssistantMessage.tsx'
import { WeeklyDvarTorahPage } from '../dvarTorah/WeeklyDvarTorahPage.tsx'
import type { DvarTorahClient } from '../dvarTorah/dvarTorahClient.ts'
import type { WeeklyDvarTorahArticle } from '../dvarTorah/dvarTorahTypes.ts'
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

afterEach(() => { cleanup(); vi.restoreAllMocks() })

function FocusLayout({ children }: { children: ReactNode }) {
  const { target } = useFocusedReading()
  return <main className={target ? 'focused-reading' : ''}><FocusedReadingToolbar /><section data-reading-scroll>{children}</section></main>
}

describe('focused reading', () => {
  it('does not show the removed per-answer focus control for long answers', () => {
    render(<FocusedReadingProvider><FocusLayout><AssistantMessage message={message} selectedSourceNumber={null} onSelectSource={vi.fn()} /></FocusLayout></FocusedReadingProvider>)
    expect(screen.queryByRole('button', { name: 'Focus answer' })).not.toBeInTheDocument()
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
    await userEvent.keyboard('{Escape}')
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

  it.each([
    ['current', false], ['current', true],
    ['archived', false], ['archived', true],
    ['offline', false], ['offline', true],
  ] as const)('keeps %s teachings out of focused reading when automatic focus is %s', async (view, focusLongContent) => {
    const week = { weekKey: 'diaspora:2026-09-12', shabbatDate: '2026-09-12', hebrewDate: '1 Tishrei 5787', parashah: null, holiday: 'Rosh Hashanah', inIsrael: false }
    const article: WeeklyDvarTorahArticle = { title: 'A thoughtful question', body: longText, week, tags: [], sources: [], centralTeaching: 'Careful study', torahGroundingPercent: 100, generatedAtUtc: '2026-09-10T12:00:00Z', publishedAtUtc: '2026-09-10T12:00:00Z', audio: { version: 'v1', voice: 'Andrew', audioUrl: '', timingsUrl: '', durationMs: 240000 } }
    const preferences = { ...DefaultReadingPreferences, focusLongContent }
    cacheReadingPreferences('reader', preferences, false)
    const settingsClient = createDemoApplicationClients().conversationSettingsClient
    settingsClient.getReadingPreferences = vi.fn().mockResolvedValue(preferences)
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {})
    const pause = vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
    const client: DvarTorahClient = {
      getReadState: async () => ({ readWeekKeys: [] }),
      setReadState: vi.fn().mockResolvedValue(undefined),
      getCurrent: async () => ({ currentWeek: week, isCurrentWeek: true, dvarTorah: article }),
      getArchive: async () => ({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }),
      getArchived: vi.fn().mockResolvedValue(article),
      getAudioUrl: () => 'https://example.test/audio.mp3',
      getAudioTimings: vi.fn().mockResolvedValue(null),
    }
    const onAskTeaching = vi.fn()
    const { unmount } = render(<ReadingPreferencesProvider userId="reader" client={settingsClient}><FocusedReadingProvider><FocusLayout><WeeklyDvarTorahPage client={client} initialRoute={view === 'archived' ? { weekKey: week.weekKey } : undefined} offlineSavedAt={view === 'offline' ? '2026-09-10T12:00:00Z' : undefined} onAskTeaching={view === 'offline' ? undefined : onAskTeaching} /></FocusLayout></FocusedReadingProvider></ReadingPreferencesProvider>)
    expect(await screen.findByRole('heading', { name: article.title })).toBeVisible()
    await waitFor(() => expect(client.getAudioTimings).toHaveBeenCalledTimes(1))
    expect(settingsClient.getReadingPreferences).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('button', { name: /focus/i })).not.toBeInTheDocument()
    expect(screen.getByRole('main')).not.toHaveClass('focused-reading')
    expect(screen.getByRole('article')).not.toHaveAttribute('data-reading-target')
    expect(screen.getByRole('article')).not.toHaveAttribute('data-reading-focused')
    expect(screen.getByRole('button', { name: 'Print teaching' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Listen to this teaching' })).toBeVisible()
    const audio = screen.getByLabelText('Dvar Torah recording')
    if (view !== 'offline') {
      expect(screen.getByRole('navigation', { name: 'Weekly learning' })).toBeVisible()
      expect(screen.getByRole('button', { name: `Mark as read: ${article.title}` })).toBeEnabled()
      fireEvent.click(screen.getByRole('button', { name: 'Ask about this teaching' }))
      expect(onAskTeaching).toHaveBeenCalledWith({ weekKey: week.weekKey, title: article.title, selectedText: null })
    }
    if (view === 'archived') {
      expect(client.getArchived).toHaveBeenCalledWith(week.weekKey)
      expect(screen.getByRole('button', { name: 'Back to past teachings' })).toBeVisible()
    }
    expect(screen.getByLabelText('Dvar Torah recording')).toBe(audio)
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    expect(pause).not.toHaveBeenCalled()
    unmount()
  })
})
