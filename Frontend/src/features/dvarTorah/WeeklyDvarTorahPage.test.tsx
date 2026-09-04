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
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('WeeklyDvarTorahPage', () => {
  it('renders normalized typography, the holiday, and chat-style source references', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication)
    render(<WeeklyDvarTorahPage client={client} />)

    expect(await screen.findByRole('heading', { name: 'Nitzavim—Choosing Life' })).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Back to conversation' })).not.toBeInTheDocument()
    expect(screen.getByText(/God’s domain/)).toBeVisible()
    expect(screen.getByText(/“clear guidance”—and acted/)).toBeVisible()
    expect(screen.getByText('Rosh Hashanah')).toBeVisible()
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
    const player = screen.getByRole('region', { name: 'Dvar Torah audio player' })
    const readingArea = screen.getByRole('region', { name: 'A teaching for the week.' })
    expect(readingArea).not.toContainElement(player)
    expect(screen.getByRole('button', { name: 'Follow text' })).toHaveAttribute('aria-pressed', 'true')
    fireEvent.wheel(readingArea)
    expect(screen.getByRole('button', { name: 'Follow text' })).toHaveAttribute('aria-pressed', 'false')
    await user.click(screen.getByRole('button', { name: 'Follow text' }))
    const audio = screen.getByLabelText('Dvar Torah recording') as HTMLAudioElement
    expect(audio).toHaveAttribute('preload', 'none')
    expect(audio).toHaveAttribute('crossorigin', 'use-credentials')
    expect(audio).not.toHaveAttribute('src')
    expect(client.getAudioTimings).not.toHaveBeenCalled()
    await user.click(listen)

    expect(play).toHaveBeenCalledTimes(1)
    expect(audio.src).toContain('/audio?version=v1')
    expect(client.getAudioTimings).toHaveBeenCalledTimes(1)
    act(() => {
      audio.currentTime = 1.2
      fireEvent.timeUpdate(audio)
    })
    await waitFor(() => expect(document.querySelector('mark')).toHaveTextContent('God’s'))
    await user.click(screen.getByRole('button', { name: 'Pause recording' }))
    expect(pause).toHaveBeenCalledTimes(1)
    fireEvent.change(screen.getByRole('slider', { name: 'Recording position' }), { target: { value: '2.2' } })
    expect(document.querySelector('mark')).toHaveTextContent('Experts')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Playback speed' }), '1.5')
    expect(audio.playbackRate).toBe(1.5)
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

function createClient(response: WeeklyDvarTorahResponse, archive: WeeklyDvarTorahArchiveResponse = EmptyArchive): DvarTorahClient {
  return {
    getCurrent: vi.fn().mockResolvedValue(response),
    getArchive: vi.fn().mockResolvedValue(archive),
    getArchived: vi.fn().mockResolvedValue(response.dvarTorah ?? ArchivedArticle),
    getAudioUrl: vi.fn((_weekKey: string, version: string) => `https://api.example.test/audio?version=${version}`),
    getAudioTimings: vi.fn().mockResolvedValue(null),
  }
}
