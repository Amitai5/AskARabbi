import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import { normalizeDvarTorahText } from './dvarTorahText.ts'
import { WeeklyDvarTorahPage } from './WeeklyDvarTorahPage.tsx'
import type { WeeklyDvarTorahArchiveResponse, WeeklyDvarTorahArticle, WeeklyDvarTorahResponse } from './dvarTorahTypes.ts'

const Publication: WeeklyDvarTorahResponse = {
  currentWeek: {
    weekKey: 'diaspora:2026-09-05',
    shabbatDate: '2026-09-05',
    hebrewDate: '23 Elul, 5786',
    parashah: 'Nitzavim',
    holiday: 'Rosh Hashanah',
    inIsrael: false,
  },
  dvarTorah: {
    week: {
      weekKey: 'diaspora:2026-09-05',
      shabbatDate: '2026-09-05',
      hebrewDate: '23 Elul, 5786',
      parashah: 'Nitzavim',
      holiday: 'Rosh Hashanah',
      inIsrael: false,
    },
    title: 'Nitzavim\u0014Choosing Life',
    body: 'Some matters remain in God\u0019s domain [TA].\n\nExperts called it \u001cclear guidance\u001d\u0014and acted [NV].',
    centralTeaching: 'Choose life.',
    tags: ['nitzavim'],
    sources: [
      {
        sourceId: 'TA',
        kind: 'Torah',
        title: 'Deuteronomy',
        publisher: 'JPS 1917',
        sourceUrl: 'https://example.test/deuteronomy',
        excerpt: 'The revealed matters belong to us and our children.',
        retrievedAtUtc: '2026-09-02T21:24:57Z',
        canonicalReference: 'Deuteronomy 29:28',
        publishedAtUtc: null,
        license: 'Public Domain',
      },
      {
        sourceId: 'NV',
        kind: 'News',
        title: 'Medical groups offer vaccine guidance',
        publisher: 'Example News',
        sourceUrl: 'https://example.test/medical-guidance',
        excerpt: 'Medical groups issued coordinated recommendations.',
        retrievedAtUtc: '2026-09-02T21:24:00Z',
        canonicalReference: null,
        publishedAtUtc: '2026-09-02T18:21:19Z',
        license: 'Public metadata',
      },
    ],
    torahGroundingPercent: 80,
    generatedAtUtc: '2026-09-02T21:28:31Z',
    publishedAtUtc: '2026-09-02T21:28:31Z',
  },
  isCurrentWeek: true,
}

const ArchivedArticle: WeeklyDvarTorahArticle = {
  ...Publication.dvarTorah!,
  week: {
    weekKey: 'diaspora:2026-08-29',
    shabbatDate: '2026-08-29',
    hebrewDate: '16 Elul, 5786',
    parashah: 'Ki Teitzei',
    holiday: null,
    inIsrael: false,
  },
  title: 'Responsibility in the Camp',
  tags: ['responsibility', 'community', 'dignity'],
}

const Archive: WeeklyDvarTorahArchiveResponse = {
  items: [
    {
      week: ArchivedArticle.week,
      title: ArchivedArticle.title,
      tags: ['responsibility', 'community', 'dignity', 'hidden fourth tag'],
      publishedAtUtc: '2026-08-24T18:00:00Z',
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 11,
  totalPages: 2,
}

const EmptyArchive: WeeklyDvarTorahArchiveResponse = {
  items: [],
  page: 1,
  pageSize: 10,
  totalCount: 0,
  totalPages: 0,
}

afterEach(() => {
  cleanup()
  vi.useRealTimers()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('WeeklyDvarTorahPage', () => {
  it('uses the teaching as the only main heading with its metadata and actions underneath', async () => {
    render(<WeeklyDvarTorahPage client={createClient(Publication)} />)
    const heading = await screen.findByRole('heading', { name: 'Nitzavim—Choosing Life', level: 1 })
    const metadata = screen.getByRole('group', { name: 'Teaching details' })
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(screen.queryByText('A teaching for the week.')).not.toBeInTheDocument()
    expect(screen.queryByText(/A new reflection follows the upcoming Shabbat/)).not.toBeInTheDocument()
    expect(metadata).toHaveTextContent('Parashat Nitzavim')
    expect(metadata).toHaveTextContent('23 Elul, 5786')
    expect(metadata).toHaveTextContent('Diaspora')
    expect(heading.compareDocumentPosition(metadata) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(metadata.compareDocumentPosition(screen.getByRole('button', { name: 'Print teaching' })) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    const progress = screen.getByRole('group', { name: 'Teaching reading progress' })
    expect(within(screen.getByRole('group', { name: 'Teaching actions' })).queryByRole('button', { name: /Mark as/ })).not.toBeInTheDocument()
    expect(screen.getByText(/“clear guidance”—and acted/).compareDocumentPosition(progress) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(within(progress).getByRole('button', { name: 'Mark as read: Nitzavim—Choosing Life' })).toBeInTheDocument()
  })

  it('identifies a festival reading once without a redundant holiday badge or generic reading label', async () => {
    render(<WeeklyDvarTorahPage client={createClient({ ...Publication, dvarTorah: { ...Publication.dvarTorah!, week: { ...Publication.currentWeek, parashah: null } } })} />)
    await screen.findByRole('heading', { level: 1, name: 'Nitzavim—Choosing Life' })
    expect(screen.getAllByText('Rosh Hashanah')).toHaveLength(1)
    expect(screen.queryByText('Holiday reading')).not.toBeInTheDocument()
  })

  it('renders normalized typography, the holiday, and chat-style source references', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication)
    render(<WeeklyDvarTorahPage client={client} />)

    expect(await screen.findByRole('heading', { name: 'Nitzavim—Choosing Life' })).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Back to conversation' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Open saved teaching' })).not.toBeInTheDocument()
    expect(screen.queryByText(/Saved on this device:|Preparing learning for offline use/)).not.toBeInTheDocument()
    expect(screen.getByText(/God’s domain/)).toBeVisible()
    expect(screen.getByText(/“clear guidance”—and acted/)).toBeVisible()
    expect(screen.getByText('Rosh Hashanah')).toBeVisible()
    expect(screen.getByLabelText('Estimated reading time')).toHaveTextContent('About 1 min read')
    expect(screen.getByLabelText('Estimated reading time')).toHaveTextContent('Based on text length')
    expect(document.body).not.toHaveTextContent('\u0019')

    const torahReference = screen.getByRole('button', { name: 'View source 1' })
    const newsReference = screen.getByRole('button', { name: 'View source 2' })
    expect(torahReference).toHaveTextContent('[1]')
    expect(newsReference).toHaveTextContent('[2]')

    await user.click(newsReference)

    expect(newsReference).toHaveAttribute('aria-expanded', 'true')
    const sourceReader = screen.getByRole('dialog', { name: 'Source reader' })
    const sourceLink = within(sourceReader).getByRole('link', { name: /Medical groups offer vaccine guidance.*Open original source/ })
    expect(sourceLink).toHaveAttribute('href', 'https://example.test/medical-guidance')
    expect(within(sourceReader).getByText(/Medical groups issued coordinated recommendations/)).toBeVisible()

    await user.click(within(sourceReader).getByRole('button', { name: 'Close source reader' }))

    expect(screen.queryByRole('dialog', { name: 'Source reader' })).not.toBeInTheDocument()
    expect(newsReference).toHaveFocus()
  })

  it('streams the recording, highlights normalized text, preserves sources, and supports pause, seek, and speed', async () => {
    const user = userEvent.setup()
    const play = vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(function (this: HTMLMediaElement) {
      this.dispatchEvent(new Event('playing'))
      return Promise.resolve()
    })
    const pause = vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {})
    const article = Publication.dvarTorah!
    const body = normalizeDvarTorahText(article.body)
    const client = createClient({ ...Publication, dvarTorah: { ...article, audio: { version: 'v1', voice: 'Andrew', durationMs: 20_000, audioUrl: '', timingsUrl: '' } } })
    client.getAudioTimings = vi.fn().mockResolvedValue({ schemaVersion: 1, version: 'v1', title: normalizeDvarTorahText(article.title), body, durationMs: 20_000, words: [
      { section: 'body', text: 'God’s', textOffset: body.indexOf('God’s'), textLength: 5, audioOffsetMs: 1000, durationMs: 700 },
      { section: 'body', text: 'Experts', textOffset: body.indexOf('Experts'), textLength: 7, audioOffsetMs: 2000, durationMs: 700 },
    ] })
    const { unmount } = render(<WeeklyDvarTorahPage client={client} />)

    const listen = await screen.findByRole('button', { name: 'Listen to this teaching' })
    expect(screen.getByLabelText('Estimated reading time')).toHaveTextContent('About 1 min read')
    expect(screen.getByLabelText('Estimated reading time')).toHaveTextContent('Based on audio at 1×')
    const player = screen.getByRole('region', { name: 'Dvar Torah audio player' })
    const readingArea = screen.getByRole('region', { name: 'Weekly Dvar Torah' })
    expect(readingArea).not.toContainElement(player)
    expect(screen.getByRole('button', { name: 'Follow text' })).toHaveAttribute('aria-pressed', 'true')
    fireEvent.wheel(readingArea)
    expect(screen.getByRole('button', { name: 'Follow text' })).toHaveAttribute('aria-pressed', 'false')
    await user.click(screen.getByRole('button', { name: 'Follow text' }))
    const audio = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    expect(audio).toHaveAttribute('preload', 'auto')
    expect(audio).toHaveAttribute('crossorigin', 'use-credentials')
    expect(audio.src).toContain('/audio?version=v1')
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    expect(play).not.toHaveBeenCalled()
    expect(document.querySelector('[data-narration-word]')).toBeNull()
    await user.click(listen)

    expect(play).toHaveBeenCalledTimes(1)
    expect(audio.src).toContain('/audio?version=v1')
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    act(() => {
      audio.currentTime = 1.2
      fireEvent.timeUpdate(audio)
    })
    await waitFor(() => expect(document.querySelector('[data-narration-word]')).toHaveTextContent('God’s'))
    await user.click(screen.getByRole('button', { name: 'Pause recording' }))
    expect(pause).toHaveBeenCalledTimes(1)
    fireEvent.change(screen.getByRole('slider', { name: 'Recording position' }), { target: { value: '2.2' } })
    expect(document.querySelector('[data-narration-word]')).toHaveTextContent('Experts')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Playback speed' }), '1.5')
    expect(audio.playbackRate).toBe(1.5)
    expect(screen.getByLabelText('Estimated reading time')).toHaveTextContent('About 1 min read')
    await user.click(screen.getByRole('button', { name: 'View source 1' }))
    expect(screen.getByRole('dialog', { name: 'Source reader' })).toBeVisible()
    await user.click(within(screen.getByRole('dialog', { name: 'Source reader' })).getByRole('button', { name: 'Close source reader' }))
    await user.click(screen.getByRole('button', { name: 'Resume recording' }))
    expect(play).toHaveBeenCalledTimes(2)
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    unmount()
    expect(pause).toHaveBeenCalledTimes(2)
    expect(audio).not.toHaveAttribute('src')
  })

  it('starts at a clicked title or body word and keeps seeking available during playback and pause', async () => {
    const user = userEvent.setup()
    const play = vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(function (this: HTMLMediaElement) {
      if (play.mock.calls.length !== 2) {
        this.dispatchEvent(new Event('playing'))
      }
      return Promise.resolve()
    })
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {})
    const article = Publication.dvarTorah!
    const title = normalizeDvarTorahText(article.title)
    const body = normalizeDvarTorahText(article.body)
    const client = createClient({ ...Publication, dvarTorah: { ...article, audio: { version: 'v1', voice: 'Andrew', durationMs: 20_000, audioUrl: '', timingsUrl: '' } } })
    client.getAudioTimings = vi.fn().mockResolvedValue({ schemaVersion: 1, version: 'v1', title, body, durationMs: 20_000, words: [
      { section: 'title', text: 'Life', textOffset: title.indexOf('Life'), textLength: 4, audioOffsetMs: 200, durationMs: 300 },
      { section: 'body', text: 'God’s', textOffset: body.indexOf('God’s'), textLength: 5, audioOffsetMs: 1000, durationMs: 700 },
      { section: 'body', text: 'Experts', textOffset: body.indexOf('Experts'), textLength: 7, audioOffsetMs: 2000, durationMs: 700 },
    ] })
    render(<WeeklyDvarTorahPage client={client} />)
    const firstWord = await screen.findByRole('button', { name: 'God’s' })
    const audio = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    Object.defineProperty(audio, 'readyState', { configurable: true, value: HTMLMediaElement.HAVE_METADATA })
    expect(play).not.toHaveBeenCalled()

    await user.click(firstWord)
    expect(audio.currentTime).toBe(1)
    expect(document.querySelector('[data-narration-word]')).toHaveTextContent('God’s')
    await user.click(screen.getByRole('button', { name: 'Experts' }))
    expect(audio.currentTime).toBe(2)
    expect(screen.queryByText('Loading audio…')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Pause recording' }))
    await user.click(screen.getByRole('button', { name: 'Life' }))
    expect(audio.currentTime).toBe(0.2)
    expect(document.querySelector('[data-narration-word]')).toHaveTextContent('Life')
    expect(screen.getByRole('button', { name: 'Pause recording' })).toBeVisible()
    expect(play).toHaveBeenCalledTimes(3)
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
  })

  it('removes the bottom player when browsing the archive', async () => {
    const article = Publication.dvarTorah!
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {})
    const client = createClient({ ...Publication, dvarTorah: { ...article, audio: { version: 'v1', voice: 'Andrew', durationMs: 20_000, audioUrl: '', timingsUrl: '' } } })
    render(<WeeklyDvarTorahPage client={client} />)
    await screen.findByRole('region', { name: 'Dvar Torah audio player' })

    fireEvent.click(screen.getByRole('button', { name: 'Past teachings' }))

    expect(screen.queryByRole('region', { name: 'Dvar Torah audio player' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Dvar Torah recording')).not.toBeInTheDocument()
  })

  it('shows the archived teaching’s own audio-based reading time above its paragraphs', async () => {
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {})
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {})
    const client = createClient(Publication)
    client.getArchive = vi.fn().mockResolvedValue(Archive)
    client.getArchived = vi.fn().mockResolvedValue({ ...ArchivedArticle, audio: { version: 'archived', voice: 'Andrew', durationMs: 403_012.5, audioUrl: '', timingsUrl: '' } })
    render(<WeeklyDvarTorahPage client={client} />)
    await screen.findByRole('heading', { name: 'Nitzavim—Choosing Life' })

    fireEvent.click(screen.getByRole('button', { name: 'Past teachings' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Open Responsibility in the Camp' }))

    const duration = await screen.findByLabelText('Estimated reading time')
    expect(duration).toHaveTextContent('About 7 min read')
    expect(duration).toHaveAttribute('title', 'Based on 6:43 of audio at 1× speed, rounded up to the next minute.')
    expect(duration.compareDocumentPosition(screen.getByText(/God’s domain/)) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    await waitFor(() => expect(client.getAudioTimings).toHaveBeenCalledWith('diaspora:2026-08-29', 'archived', expect.any(AbortSignal)))
  })

  it('loads the newest ten archive records, shows their metadata, searches, and pages', async () => {
    const user = userEvent.setup()
    const secondPage: WeeklyDvarTorahArchiveResponse = {
      ...Archive,
      items: [{ ...Archive.items[0], week: { ...Archive.items[0].week, weekKey: 'diaspora:2026-08-22', shabbatDate: '2026-08-22' }, title: 'A Second Page Teaching' }],
      page: 2,
    }
    const getArchive = vi.fn(async ({ page = 1, search }: { page?: number; search?: string } = {}) => {
      if (search === 'community') {
        return { ...Archive, totalCount: 1, totalPages: 1 }
      }
      return page === 2 ? secondPage : Archive
    })
    const client: DvarTorahClient = {
      ...createClient(Publication),
      getCurrent: vi.fn().mockResolvedValue(Publication),
      getArchive,
      getArchived: vi.fn().mockResolvedValue(ArchivedArticle),
    }
    render(<WeeklyDvarTorahPage client={client} />)

    await screen.findByRole('heading', { name: 'Nitzavim—Choosing Life' })
    expect(getArchive).toHaveBeenCalledWith({ page: 1, pageSize: 10, search: undefined })
    await user.click(screen.getByRole('button', { name: 'Past teachings' }))

    expect(await screen.findByRole('heading', { name: 'Explore past Dvar Torahs.' })).toBeVisible()
    expect(screen.getByText('Responsibility in the Camp')).toBeVisible()
    expect(screen.getByText(/August 29, 2026/)).toBeVisible()
    expect(screen.getByText('16 Elul, 5786')).toBeVisible()
    expect(screen.getByText('Parashat Ki Teitzei')).toBeVisible()
    expect(screen.getByText('responsibility')).toBeVisible()
    expect(screen.getByText('community')).toBeVisible()
    expect(screen.getByText('dignity')).toBeVisible()
    expect(screen.queryByText('hidden fourth tag')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(await screen.findByText('A Second Page Teaching')).toBeVisible()
    expect(getArchive).toHaveBeenLastCalledWith({ page: 2, pageSize: 10, search: undefined })

    await user.type(screen.getByRole('searchbox', { name: 'Search past teachings' }), 'community')
    await user.click(screen.getByRole('button', { name: 'Search' }))
    expect(await screen.findByText('Showing results for “community”')).toBeVisible()
    expect(getArchive).toHaveBeenLastCalledWith({ page: 1, pageSize: 10, search: 'community' })
  })

  it('opens a selected past teaching and returns to the archive', async () => {
    const user = userEvent.setup()
    const client: DvarTorahClient = {
      ...createClient(Publication),
      getCurrent: vi.fn().mockResolvedValue(Publication),
      getArchive: vi.fn().mockResolvedValue(Archive),
      getArchived: vi.fn().mockResolvedValue(ArchivedArticle),
    }
    render(<WeeklyDvarTorahPage client={client} />)

    await user.click(await screen.findByRole('button', { name: 'Past teachings' }))
    await user.click(await screen.findByRole('button', { name: 'Open Responsibility in the Camp' }))

    expect(client.getArchived).toHaveBeenCalledWith('diaspora:2026-08-29')
    expect(await screen.findByRole('heading', { name: 'Responsibility in the Camp' })).toBeVisible()
    expect(screen.getByText('Audio is not available for this teaching yet.')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Back to past teachings' }))
    expect(await screen.findByRole('heading', { name: 'Explore past Dvar Torahs.' })).toBeVisible()
  })
})

describe('teaching reading progress', () => {
  it('marks the current teaching, reloads its account progress, and lets the reader undo it', async () => {
    const user = userEvent.setup()
    const read = new Set<string>()
    const client = createClient(Publication)
    client.getReadState = vi.fn(async () => ({ readWeekKeys: [...read] }))
    client.setReadState = vi.fn(async (key, isRead) => { if (isRead) { read.add(key) } else { read.delete(key) } })
    const { unmount } = render(<WeeklyDvarTorahPage client={client} />)
    await user.click(await screen.findByRole('button', { name: 'Mark as read: Nitzavim—Choosing Life' }))
    expect(await screen.findByRole('button', { name: 'Mark as unread: Nitzavim—Choosing Life' })).toHaveAttribute('aria-pressed', 'true')
    expect(client.setReadState).toHaveBeenCalledWith('diaspora:2026-09-05', true)
    unmount()

    render(<WeeklyDvarTorahPage client={client} />)
    await user.click(await screen.findByRole('button', { name: 'Mark as unread: Nitzavim—Choosing Life' }))
    expect(await screen.findByRole('button', { name: 'Mark as read: Nitzavim—Choosing Life' })).toHaveAttribute('aria-pressed', 'false')
    expect(client.setReadState).toHaveBeenLastCalledWith('diaspora:2026-09-05', false)
  })

  it('combines read filters with search, resets pagination, and preserves the filter when opening an article', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication, { ...Archive, page: 2 })
    vi.mocked(client.getArchived).mockResolvedValue(ArchivedArticle)
    const navigate = vi.fn()
    render(<WeeklyDvarTorahPage client={client} initialRoute={{ archive: true, page: 2, search: 'community' }} onNavigate={navigate} />)
    await screen.findByRole('button', { name: 'Open Responsibility in the Camp' })
    await user.click(screen.getByRole('button', { name: 'Unread' }))
    await waitFor(() => expect(client.getArchive).toHaveBeenLastCalledWith({ page: 1, pageSize: 10, search: 'community', readStatus: 'unread' }))
    expect(navigate).toHaveBeenLastCalledWith({ archive: true, page: 1, search: 'community', readStatus: 'unread' })
    await user.click(screen.getByRole('button', { name: 'Open Responsibility in the Camp' }))
    await screen.findByRole('heading', { name: ArchivedArticle.title })
    expect(navigate).toHaveBeenLastCalledWith({ weekKey: ArchivedArticle.week.weekKey, page: 1, search: 'community', readStatus: 'unread' })
    await user.click(screen.getByRole('button', { name: 'Back to past teachings' }))
    expect(screen.getByRole('button', { name: 'Unread' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows only status in the archive and refreshes its filter after marking an opened teaching read', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication, { ...Archive, totalCount: 1, totalPages: 1 })
    vi.mocked(client.getArchived).mockResolvedValue(ArchivedArticle)
    render(<WeeklyDvarTorahPage client={client} initialRoute={{ archive: true, readStatus: 'unread' }} />)
    const open = await screen.findByRole('button', { name: `Open ${ArchivedArticle.title}` })
    expect(open).toHaveAccessibleDescription('Unread')
    expect(within(open).getByText('Unread')).toBeVisible()
    expect(screen.queryByRole('button', { name: /Mark as/ })).not.toBeInTheDocument()
    await user.click(open)
    const mark = within(await screen.findByRole('group', { name: 'Teaching reading progress' })).getByRole('button', { name: `Mark as read: ${ArchivedArticle.title}` })
    vi.mocked(client.getArchive).mockResolvedValue(EmptyArchive)
    await user.click(mark)
    await user.click(screen.getByRole('button', { name: 'Back to past teachings' }))
    expect(await screen.findByText('No unread teachings found.')).toBeVisible()
    expect(client.setReadState).toHaveBeenCalledWith(ArchivedArticle.week.weekKey, true)
    expect(client.getArchived).toHaveBeenCalledWith(ArchivedArticle.week.weekKey)
  })

  it('marks a text-only teaching after more than 33% of reading time and preserves a manual undo', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'performance'] })
    const client = createClient(Publication, Archive)
    await act(async () => { render(<WeeklyDvarTorahPage client={client} />) })
    const progress = screen.getByRole('group', { name: 'Teaching reading progress' })
    expect(within(progress).getByRole('status')).toHaveTextContent('Finished reading?')

    await act(async () => { await vi.advanceTimersByTimeAsync(19_800) })
    expect(client.setReadState).not.toHaveBeenCalled()
    await act(async () => { await vi.advanceTimersByTimeAsync(1) })
    expect(client.setReadState).toHaveBeenCalledExactlyOnceWith(Publication.currentWeek.weekKey, true)
    expect(within(progress).getByRole('status')).toHaveTextContent('Marked as read')

    await act(async () => { fireEvent.click(within(progress).getByRole('button', { name: /Mark as unread:/ })) })
    expect(client.setReadState).toHaveBeenLastCalledWith(Publication.currentWeek.weekKey, false)
    await act(async () => { await vi.advanceTimersByTimeAsync(120_000) })
    expect(client.setReadState).toHaveBeenCalledTimes(2)
    expect(within(progress).getByRole('status')).toHaveTextContent('Finished reading?')
  })

  it('does not retry a failed automatic save repeatedly and allows a manual retry', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'performance'] })
    const client = createClient(Publication)
    vi.mocked(client.setReadState).mockRejectedValueOnce(new Error('Unavailable'))
    await act(async () => { render(<WeeklyDvarTorahPage client={client} />) })
    await act(async () => { await vi.advanceTimersByTimeAsync(19_801) })
    expect(screen.getByRole('alert')).toHaveTextContent('could not be saved')
    expect(screen.getByRole('alert').closest('footer')).toContainElement(screen.getByRole('button', { name: /Mark as read:/ }))
    expect(screen.getByRole('button', { name: /Mark as read:/ })).toHaveAttribute('aria-pressed', 'false')
    await act(async () => { await vi.advanceTimersByTimeAsync(120_000) })
    expect(client.setReadState).toHaveBeenCalledTimes(1)

    await act(async () => { fireEvent.click(screen.getByRole('button', { name: /Mark as read:/ })) })
    expect(screen.getByRole('button', { name: /Mark as unread:/ })).toBeEnabled()
    expect(client.setReadState).toHaveBeenCalledTimes(2)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows a read badge without marking other teachings while browsing the archive', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'performance'] })
    const client = createClient(Publication, Archive)
    client.getReadState = vi.fn().mockResolvedValue({ readWeekKeys: [ArchivedArticle.week.weekKey] })
    await act(async () => { render(<WeeklyDvarTorahPage client={client} initialRoute={{ archive: true }} />) })
    const open = screen.getByRole('button', { name: `Open ${ArchivedArticle.title}` })
    expect(open).toHaveAccessibleDescription('Read')
    expect(within(open).getByText('Read')).toBeVisible()
    expect(screen.queryByRole('button', { name: /Mark as/ })).not.toBeInTheDocument()
    await act(async () => { await vi.advanceTimersByTimeAsync(120_000) })
    expect(client.setReadState).not.toHaveBeenCalled()
  })

  it('keeps the original state on a failed save and allows another attempt', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication)
    vi.mocked(client.setReadState).mockRejectedValueOnce(new Error('Unavailable'))
    render(<WeeklyDvarTorahPage client={client} />)
    const button = await screen.findByRole('button', { name: 'Mark as read: Nitzavim—Choosing Life' })
    await user.click(button)
    expect(await screen.findByRole('alert')).toHaveTextContent('could not be saved')
    expect(button).toHaveAttribute('aria-pressed', 'false')
    await user.click(button)
    expect(await screen.findByRole('button', { name: 'Mark as unread: Nitzavim—Choosing Life' })).toBeEnabled()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('does not overwrite unknown progress after a failed load and offers a retry', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication)
    client.getReadState = vi.fn().mockRejectedValueOnce(new Error('Unavailable')).mockResolvedValue({ readWeekKeys: ['diaspora:2026-09-05'] })
    render(<WeeklyDvarTorahPage client={client} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('could not be loaded')
    expect(screen.getByRole('button', { name: 'Mark as read: Nitzavim—Choosing Life' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Retry reading progress' }))
    expect(await screen.findByRole('button', { name: 'Mark as unread: Nitzavim—Choosing Life' })).toBeEnabled()
    expect(client.setReadState).not.toHaveBeenCalled()
  })

  it('disables account progress for a saved offline teaching without making progress requests', async () => {
    const client = createClient(Publication)
    client.getReadState = vi.fn()
    render(<WeeklyDvarTorahPage client={client} offlineSavedAt="2026-09-05T12:00:00Z" />)
    expect(await screen.findByRole('button', { name: 'Mark as read: Nitzavim—Choosing Life' })).toBeDisabled()
    expect(screen.getByText('Connect to update your reading progress.')).toBeVisible()
    expect(client.getReadState).not.toHaveBeenCalled()
  })

  it('prevents duplicate updates while a save is pending', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication)
    let finish!: () => void
    client.setReadState = vi.fn(() => new Promise<void>(resolve => { finish = resolve }))
    render(<WeeklyDvarTorahPage client={client} />)
    const button = await screen.findByRole('button', { name: 'Mark as read: Nitzavim—Choosing Life' })
    await user.dblClick(button)
    expect(client.setReadState).toHaveBeenCalledTimes(1)
    expect(button).toBeDisabled()
    await act(async () => finish())
    expect(screen.getByRole('button', { name: 'Mark as unread: Nitzavim—Choosing Life' })).toBeEnabled()
  })
})

function createClient(response: WeeklyDvarTorahResponse, archive: WeeklyDvarTorahArchiveResponse = EmptyArchive): DvarTorahClient {
  return {
    getReadState: async () => ({ readWeekKeys: [] }),
    setReadState: vi.fn().mockResolvedValue(undefined),
    getCurrent: vi.fn().mockResolvedValue(response),
    getArchive: vi.fn().mockResolvedValue(archive),
    getArchived: vi.fn().mockResolvedValue(response.dvarTorah ?? ArchivedArticle),
    getAudioUrl: vi.fn((_weekKey: string, version: string) => `https://api.example.test/audio?version=${version}`),
    getAudioTimings: vi.fn().mockResolvedValue(null),
  }
}
