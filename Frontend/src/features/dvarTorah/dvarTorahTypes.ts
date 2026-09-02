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
}

export interface WeeklyDvarTorahResponse {
  currentWeek: DvarTorahWeek
  dvarTorah: WeeklyDvarTorahArticle | null
  isCurrentWeek: boolean
}
