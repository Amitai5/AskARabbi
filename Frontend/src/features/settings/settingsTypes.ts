export interface UserSettings {
  showSourceContextByDefault: boolean
  emailProductUpdates: boolean
}

export interface UsageSummary {
  periodStartUtc: string
  periodEndUtc: string
  answersUsed: number
  answerLimit: number
  answersRemaining: number
}

export function createDefaultUserSettings(): UserSettings {
  return {
    showSourceContextByDefault: false,
    emailProductUpdates: false,
  }
}
