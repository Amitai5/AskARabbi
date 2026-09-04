export interface DvarTorahWeek {
  weekKey: string
  shabbatDate: string
  hebrewDate: string
  parashah: string | null
  holiday: string | null
  inIsrael: boolean
}

export type WeeklyDvarTorahSourceKind = 'Torah' | 'News' | 'Other'

export interface WeeklyDvarTorahSource {
  sourceId: string
  kind: WeeklyDvarTorahSourceKind
  title: string
  publisher: string
  sourceUrl: string
  excerpt: string
  retrievedAtUtc: string
  canonicalReference: string | null
  publishedAtUtc: string | null
  license: string | null
}

export interface WeeklyDvarTorahArticle {
  week: DvarTorahWeek
  title: string
  body: string
  centralTeaching: string | null
  tags: string[]
  sources: WeeklyDvarTorahSource[]
  torahGroundingPercent: number | null
  generatedAtUtc: string
  publishedAtUtc: string
  audio?: WeeklyDvarTorahAudio | null
}

export interface WeeklyDvarTorahAudio {
  version: string
  voice: string
  durationMs: number
  audioUrl: string
  timingsUrl: string
}

export interface DvarTorahAudioWord {
  section: 'title' | 'body'
  text: string
  textOffset: number
  textLength: number
  audioOffsetMs: number
  durationMs: number
}

export interface DvarTorahAudioTimings {
  schemaVersion: 1
  version: string
  title: string
  body: string
  durationMs: number
  words: DvarTorahAudioWord[]
}

export interface WeeklyDvarTorahResponse {
  currentWeek: DvarTorahWeek
  dvarTorah: WeeklyDvarTorahArticle | null
  isCurrentWeek: boolean
}

export interface WeeklyDvarTorahArchiveItem {
  week: DvarTorahWeek
  title: string
  tags: string[]
  publishedAtUtc: string
}

export interface WeeklyDvarTorahArchiveResponse {
  items: WeeklyDvarTorahArchiveItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface WeeklyDvarTorahArchiveQuery {
  search?: string
  page?: number
  pageSize?: number
}
