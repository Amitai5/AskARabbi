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
  // Avoid losing a hundredth to floating-point subtraction at an exact boundary.
  return remaining > 0 && remaining < 0.01 ? '<0.01' : (Math.floor(remaining * 100 + 1e-9) / 100).toLocaleString(undefined, { maximumFractionDigits: 2 })
}

export function createDefaultUserSettings(): UserSettings {
  return {
    showSourceContextByDefault: false,
    emailProductUpdates: false,
    enterSendsMessage: false,
  }
}
