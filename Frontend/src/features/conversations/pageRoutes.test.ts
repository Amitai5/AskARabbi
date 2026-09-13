import { afterEach, describe, expect, it } from 'vitest'
import { calendarPath, conversationPath, readPageRoute, teachingPath, writePageUrl } from './pageRoutes.ts'

afterEach(() => window.history.replaceState({}, '', '/'))
describe('Page routes', () => {
  it.each(['/conversations/%ZZ', '/conversations/pending%3Aid', '/conversations/%2Fprivate', '/conversations/%3Fx'])('rejects malformed or temporary IDs in %s', path => {
    window.history.replaceState({}, '', path)
    expect(readPageRoute()).toEqual({ view: 'conversation', isNew: true })
  })
  it.each(['/', '/conversations', '/conversations/', '/conversations/new'])('defaults %s to a new conversation', path => {
    window.history.replaceState({}, '', path)
    expect(readPageRoute()).toEqual({ view: 'conversation', isNew: true })
  })
  it('keeps explicitly linked conversations selected', () => {
    writePageUrl(conversationPath('saved-id'))
    expect(readPageRoute()).toEqual({ view: 'conversation', conversationId: 'saved-id' })
  })
  it('round trips exact teaching keys and archive queries', () => {
    writePageUrl(teachingPath({ weekKey: 'israel:2026-09-05' }))
    expect(readPageRoute()).toEqual({ view: 'dvarTorah', teaching: { weekKey: 'israel:2026-09-05' } })
    writePageUrl(teachingPath({ archive: true, page: 3, search: 'love & responsibility' }))
    expect(readPageRoute()).toEqual({ view: 'dvarTorah', teaching: { archive: true, page: 3, search: 'love & responsibility' } })
  })
  it('round trips calendar range and search and keeps drafts out of the URL', () => {
    writePageUrl(calendarPath(360, 'Rosh Chodesh'))
    expect(readPageRoute()).toEqual({ view: 'calendar', days: 360, search: 'Rosh Chodesh' })
    expect(conversationPath('pending:test')).toBe('/conversations/new')
    expect(conversationPath('saved-id')).toBe('/conversations/saved-id')
  })
  it('normalizes unsupported ranges and invalid page numbers', () => {
    writePageUrl('/calendar?days=500')
    expect(readPageRoute().days).toBe(90)
    writePageUrl('/teachings/archive?page=-3')
    expect(readPageRoute().teaching?.page).toBe(1)
  })
  it('round trips reading filters with archive and article return context', () => {
    writePageUrl(teachingPath({ archive: true, readStatus: 'unread', search: 'community', page: 2 }))
    expect(readPageRoute().teaching).toEqual({ archive: true, readStatus: 'unread', search: 'community', page: 2 })
    writePageUrl(teachingPath({ weekKey: 'diaspora:2026-09-05', readStatus: 'read', search: 'community', page: 2 }))
    expect(readPageRoute().teaching).toEqual({ weekKey: 'diaspora:2026-09-05', readStatus: 'read', search: 'community', page: 2 })
    writePageUrl('/teachings/archive?readStatus=invalid')
    expect(readPageRoute().teaching?.readStatus).toBeUndefined()
    writePageUrl('/teachings/diaspora%3A2026-09-05?page=Infinity')
    expect(readPageRoute().teaching?.page).toBeUndefined()
  })
})
