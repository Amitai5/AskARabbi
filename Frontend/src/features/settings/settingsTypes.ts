export interface UserSettings {
  showSourceContextByDefault: boolean
  emailProductUpdates: boolean
}

export interface UsageSummary {
  periodStartUtc: string
  periodEndUtc: string
  tokensUsed: number
  tokenLimit: number
  tokensRemaining: number
  usedPercent: number
  isLimitReached: boolean
}

export function formatUsagePercent(usage: UsageSummary): string {
  return (Math.floor(Math.min(100, Math.max(0, usage.usedPercent)) * 10) / 10).toLocaleString(undefined, { maximumFractionDigits: 1 })
}

export function createDefaultUserSettings(): UserSettings {
  return {
    showSourceContextByDefault: false,
    emailProductUpdates: false,
  }
}
