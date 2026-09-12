import type { CalendarRange } from '../calendar/calendarTypes.ts'
import type { TeachingReadStatus } from '../dvarTorah/dvarTorahTypes.ts'
import { readSettingsRoute } from '../settings/settingsRegistry.ts'

export type ActiveView = 'conversation' | 'dvarTorah' | 'calendar' | 'settings'
export interface TeachingRoute { weekKey?: string; archive?: boolean; page?: number; search?: string; readStatus?: TeachingReadStatus }
export interface PageRoute {
  view: ActiveView
  conversationId?: string
  isNew?: boolean
  teaching?: TeachingRoute
  days?: CalendarRange
  search?: string
}

export function readPageRoute(): PageRoute {
  const { pathname, search } = window.location
  const query = new URLSearchParams(search)
  const readStatus = query.get('readStatus')
  const page = Number(query.get('page'))
  const progress = readStatus === 'read' || readStatus === 'unread' ? { readStatus } as const : {}
  if (readSettingsRoute(pathname)) { return { view: 'settings' } }
  if (pathname === '/calendar') {
    const days = Number(query.get('days'))
    return { view: 'calendar', days: days === 180 || days === 360 ? days : 90, search: query.get('search')?.slice(0, 150) ?? '' }
  }
  if (pathname === '/teachings/archive') {
    return { view: 'dvarTorah', teaching: { archive: true, page: Number.isInteger(page) && page > 0 ? page : 1, search: query.get('search')?.slice(0, 120) ?? '', ...progress } }
  }
  if (pathname === '/teachings') { return { view: 'dvarTorah', teaching: {} } }
  const teaching = decodeSegment(pathname, '/teachings/')
  if (teaching) { return { view: 'dvarTorah', teaching: { weekKey: teaching, ...progress, ...(query.has('search') ? { search: query.get('search')?.slice(0, 120) } : {}), ...(Number.isInteger(page) && page > 1 ? { page } : {}) } } }
  if (pathname === '/conversations/new') { return { view: 'conversation', isNew: true } }
  const conversationId = decodeSegment(pathname, '/conversations/')
  return { view: 'conversation', conversationId: conversationId ?? undefined }
}

export function conversationPath(id: string | null) {
  return id && !id.startsWith('pending:') ? `/conversations/${encodeURIComponent(id)}` : '/conversations/new'
}

export function teachingPath(route: TeachingRoute = {}) {
  if (!route.archive && !route.weekKey) { return '/teachings' }
  const query = new URLSearchParams()
  if (route.page && route.page > 1) { query.set('page', String(route.page)) }
  if (route.search) { query.set('search', route.search) }
  if (route.readStatus && route.readStatus !== 'all') { query.set('readStatus', route.readStatus) }
  return `${route.weekKey ? `/teachings/${encodeURIComponent(route.weekKey)}` : '/teachings/archive'}${query.size ? `?${query}` : ''}`
}

export function calendarPath(days: CalendarRange = 90, search = '') {
  const query = new URLSearchParams()
  if (days !== 90) { query.set('days', String(days)) }
  if (search) { query.set('search', search) }
  return `/calendar${query.size ? `?${query}` : ''}`
}

export function writePageUrl(path: string, replace = false) {
  if (`${window.location.pathname}${window.location.search}${window.location.hash}` === path && !window.history.state?.askarabbiPendingId) { return }
  const state = { ...window.history.state }
  delete state.askarabbiPendingId
  if (replace) { window.history.replaceState(state, '', path) }
  else { window.history.pushState(state, '', path) }
}

function decodeSegment(path: string, prefix: string) {
  if (!path.startsWith(prefix)) { return null }
  try {
    const value = decodeURIComponent(path.slice(prefix.length))
    return value && value.length <= 128 && !/[\s/\\?#]/.test(value) && !value.startsWith('pending:') ? value : null
  } catch { return null }
}
