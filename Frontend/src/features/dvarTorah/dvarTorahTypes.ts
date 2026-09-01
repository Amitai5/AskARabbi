export interface DvarTorahWeek {
  weekKey: string
  shabbatDate: string
  hebrewDate: string
  parashah: string | null
  holiday: string | null
  inIsrael: boolean
}

export interface WeeklyDvarTorahArticle {
  week: DvarTorahWeek
  title: string
  body: string
  generatedAtUtc: string
  publishedAtUtc: string
}

export interface WeeklyDvarTorahResponse {
  currentWeek: DvarTorahWeek
  dvarTorah: WeeklyDvarTorahArticle | null
  isCurrentWeek: boolean
}
