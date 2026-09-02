import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { DvarTorahClient } from './dvarTorahClient.ts'
import { WeeklyDvarTorahPage } from './WeeklyDvarTorahPage.tsx'
import type { WeeklyDvarTorahResponse } from './dvarTorahTypes.ts'

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

describe('WeeklyDvarTorahPage', () => {
  it('renders normalized typography, the holiday, and chat-style source references', async () => {
    const user = userEvent.setup()
    const client = createClient(Publication)
    render(<WeeklyDvarTorahPage client={client} onBack={vi.fn()} />)

    expect(await screen.findByRole('heading', { name: 'Nitzavim—Choosing Life' })).toBeVisible()
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
})

function createClient(response: WeeklyDvarTorahResponse): DvarTorahClient {
  return {
    getCurrent: vi.fn().mockResolvedValue(response),
  }
}
