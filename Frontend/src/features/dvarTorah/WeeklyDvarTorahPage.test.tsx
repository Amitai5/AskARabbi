import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import { createSpeechChunks } from './dvarTorahSpeech.ts'
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
  vi.unstubAllGlobals()
})

describe('WeeklyDvarTorahPage', () => {
  it('breaks long readings into browser-safe speech chunks', () => {
    const chunks = createSpeechChunks('A weekly teaching', `A deliberately long sentence ${'with meaningful words '.repeat(40)}. A short conclusion.`)

    expect(chunks.length).toBeGreaterThan(1)
    expect(chunks.every((chunk) => chunk.length <= 240)).toBe(true)
    expect(chunks.join(' ')).toContain('A short conclusion.')
  })

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

  it('reads the normalized teaching aloud and supports pause, resume, and stop', async () => {
    const user = userEvent.setup()
    const speech = {
      cancel: vi.fn(),
      getVoices: vi.fn(() => [
        createVoice('Microsoft Zira - English (United States)', 'en-US'),
        createVoice('Microsoft David - English (United States)', 'en-US'),
        createVoice('Microsoft Guy Online (Natural) - English (United States)', 'en-US'),
      ]),
      pause: vi.fn(),
      resume: vi.fn(),
      speak: vi.fn(),
    }
    class TestUtterance {
      readonly text: string
      lang = ''
      rate = 1
      voice: SpeechSynthesisVoice | null = null
      onend: (() => void) | null = null
      onerror: ((event: { error: string }) => void) | null = null

      constructor(text: string) {
        this.text = text
      }
    }
    vi.stubGlobal('speechSynthesis', speech)
    vi.stubGlobal('SpeechSynthesisUtterance', TestUtterance)
    render(<WeeklyDvarTorahPage client={createClient(Publication)} />)

    await user.click(await screen.findByRole('button', { name: 'Read this Dvar Torah aloud' }))

    expect(speech.speak).toHaveBeenCalledTimes(1)
    const utterance = speech.speak.mock.calls[0][0] as TestUtterance
    expect(utterance.text).toContain('Nitzavim—Choosing Life. Some matters remain in God’s domain.')
    expect(utterance.text).toContain('Experts called it “clear guidance”—and acted')
    expect(utterance.text).not.toContain('[TA]')
    expect(utterance.lang).toBe('en-US')
    expect(utterance.voice?.name).toBe('Microsoft Guy Online (Natural) - English (United States)')

    await user.click(screen.getByRole('button', { name: 'Pause reading' }))
    expect(speech.pause).toHaveBeenCalledTimes(1)
    await user.click(screen.getByRole('button', { name: 'Resume reading' }))
    expect(speech.resume).toHaveBeenCalledTimes(1)
    await user.click(screen.getByRole('button', { name: 'Stop reading' }))
    expect(speech.cancel).toHaveBeenCalledTimes(2)
    expect(screen.getByRole('button', { name: 'Read this Dvar Torah aloud' })).toBeVisible()
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
      getCurrent: vi.fn().mockResolvedValue(Publication),
      getArchive: vi.fn().mockResolvedValue(Archive),
      getArchived: vi.fn().mockResolvedValue(ArchivedArticle),
    }
    render(<WeeklyDvarTorahPage client={client} />)

    await user.click(await screen.findByRole('button', { name: 'Past teachings' }))
    await user.click(await screen.findByRole('button', { name: 'Open Responsibility in the Camp' }))

    expect(client.getArchived).toHaveBeenCalledWith('diaspora:2026-08-29')
    expect(await screen.findByRole('heading', { name: 'Responsibility in the Camp' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Read aloud unavailable' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Back to past teachings' }))
    expect(await screen.findByRole('heading', { name: 'Explore past Dvar Torahs.' })).toBeVisible()
  })
})

function createVoice(name: string, lang: string) {
  return {
    default: false,
    lang,
    localService: true,
    name,
    voiceURI: name,
  } satisfies SpeechSynthesisVoice
}

function createClient(response: WeeklyDvarTorahResponse, archive: WeeklyDvarTorahArchiveResponse = EmptyArchive): DvarTorahClient {
  return {
    getCurrent: vi.fn().mockResolvedValue(response),
    getArchive: vi.fn().mockResolvedValue(archive),
    getArchived: vi.fn().mockResolvedValue(response.dvarTorah ?? ArchivedArticle),
  }
}
