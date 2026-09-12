export interface UserSettings {
  showSourceContextByDefault: boolean
  emailProductUpdates: boolean
  enterSendsMessage: boolean
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

export function formatUsageRemainingPercent(usage: UsageSummary): string {
  const remaining = usage.isLimitReached ? 0 : 100 - Math.min(100, Math.max(0, usage.usedPercent))
  return remaining > 0 && remaining < 0.1 ? '<0.1' : (Math.floor(remaining * 10) / 10).toLocaleString(undefined, { maximumFractionDigits: 1 })
}

export function createDefaultUserSettings(): UserSettings {
  return {
    showSourceContextByDefault: false,
    emailProductUpdates: false,
    enterSendsMessage: false,
  }
}
