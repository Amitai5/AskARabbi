import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { HolidayAgenda } from './HolidayAgenda.tsx'
import { AllCalendarFilters, type CalendarRange } from './calendarTypes.ts'
import { addCivilDays } from './calendarAgenda.ts'
import { RoshHashanah } from './calendarTestData.ts'

const Today = '2026-09-10'
const Events = [RoshHashanah, ...[100, 250].map(offset => ({ ...RoshHashanah, id: `future-${offset}`, title: offset === 100 ? 'Winter holiday' : 'Spring holiday', beginningDate: addCivilDays(Today, offset), startDate: addCivilDays(Today, offset), endDate: addCivilDays(Today, offset), occurrences: [] }))]

function Agenda() {
  const [days, setDays] = useState<CalendarRange>(90)
  const [filters, setFilters] = useState(AllCalendarFilters)
  return <HolidayAgenda days={days} onRange={setDays} filters={filters} onFilters={setFilters} events={Events} startDate={Today} />
}

describe('holiday search controls', () => {
  it('adjusts the visible range as the search changes and restores the chosen range on clear', async () => {
    const user = userEvent.setup()
    render(<Agenda />)
    const search = screen.getByRole('searchbox', { name: 'Search upcoming holidays' })
    await user.type(search, 'winter')
    expect(screen.getByRole('button', { name: '180 days' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Next 90 days' })).toBeDisabled()
    expect(screen.getByRole('heading', { name: 'Winter holiday' })).toBeVisible()
    await user.clear(search)
    await user.type(search, 'spring')
    expect(screen.getByRole('button', { name: '360 days' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('status')).toHaveTextContent('1 holiday found in the next 360 days')
    await user.clear(search)
    await user.type(search, 'rosh')
    expect(screen.getByRole('button', { name: 'Next 90 days' })).toHaveAttribute('aria-pressed', 'true')
    await user.click(screen.getByRole('button', { name: '180 days' }))
    await user.clear(search)
    await user.type(search, 'spring')
    await user.keyboard('{Escape}')
    expect(search).toHaveValue('')
    expect(search).toHaveFocus()
    expect(screen.getByRole('button', { name: '180 days' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('explains missing matches and retains filters rather than silently enabling categories', async () => {
    const user = userEvent.setup()
    render(<Agenda />)
    const search = screen.getByRole('searchbox', { name: 'Search upcoming holidays' })
    await user.type(search, 'spring')
    await user.click(screen.getByRole('button', { name: 'Filter holidays' }))
    await user.click(screen.getByRole('checkbox', { name: 'Major holidays' }))
    expect(screen.getByRole('status')).toHaveTextContent('No matching holidays in the next 360 days with the selected filters')
    expect(screen.queryByRole('heading', { name: 'Spring holiday' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Show everything' }))
    expect(screen.getByRole('heading', { name: 'Spring holiday' })).toBeVisible()
    await user.clear(search)
    await user.type(search, 'not a holiday name')
    expect(screen.getByRole('status')).toHaveTextContent('No matching holidays')
  })
})
