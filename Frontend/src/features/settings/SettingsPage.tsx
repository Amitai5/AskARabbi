import { useState, type ReactNode } from 'react'
import { ArrowLeft, Bell, BookOpenText, Database, Gauge, KeyRound, Save, ShieldCheck, Smartphone } from 'lucide-react'
import { UserDataControls } from './UserDataControls.tsx'
import { Toast } from '../../components/Toast.tsx'
import type { AuthenticatedUser } from '../auth/authTypes.ts'
import { formatUsageRemainingPercent, type UsageSummary, type UserSettings } from './settingsTypes.ts'
import { InstallAppPanel } from '../pwa/PwaInstall.tsx'
import { OfflineLearningSettings } from '../pwa/OfflineLearning.tsx'

interface SettingsPageProps {
  user: AuthenticatedUser
  settings: UserSettings
  usage: UsageSummary | null
  usageError: string | null
  isLoadingUsage: boolean
  isDataBusy: boolean
  onDeleteChats(): Promise<void>
  onDeleteAccount(): Promise<void>
  onBack(): void
  onSave(settings: UserSettings): Promise<void>
  onRequestPasswordReset(): Promise<void>
  onRetryUsage(): void
}

type NotificationKind = 'settings' | 'password'

export function SettingsPage({ user, settings, usage, usageError, isLoadingUsage, isDataBusy, onDeleteChats, onDeleteAccount, onBack, onSave, onRequestPasswordReset, onRetryUsage }: SettingsPageProps) {
  const [draft, setDraft] = useState(settings)
  const [notificationKind, setNotificationKind] = useState<NotificationKind | null>(null)
  const [notificationId, setNotificationId] = useState(0)
  const [isRequestingPasswordReset, setIsRequestingPasswordReset] = useState(false)
  const [passwordResetError, setPasswordResetError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  const notification = notificationKind === 'password'
    ? {
        title: 'Password reset requested',
        message: `If ${user.email} uses password sign-in, a secure reset email will arrive shortly.`,
      }
    : {
        title: 'Settings saved',
        message: 'Your conversation defaults were saved to your account.',
      }

  function showNotification(kind: NotificationKind) {
    setNotificationKind(kind)
    setNotificationId((current) => current + 1)
  }

  function updateSetting(field: keyof UserSettings, value: boolean) {
    setDraft((current) => ({ ...current, [field]: value }))
    setNotificationKind(null)
  }

  async function handleSave() {
    setSaveError(null)
    setIsSaving(true)
    try {
      await onSave(draft)
      showNotification('settings')
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'Your settings could not be saved.')
    } finally {
      setIsSaving(false)
    }
  }

  async function handlePasswordReset() {
    setPasswordResetError(null)
    setIsRequestingPasswordReset(true)
    try {
      await onRequestPasswordReset()
      showNotification('password')
    } catch {
      setPasswordResetError('Password reset could not be requested. Please try again.')
    } finally {
      setIsRequestingPasswordReset(false)
    }
  }

  return (
    <section className="min-h-0 flex-1 overflow-y-auto px-4 sm:px-8" aria-labelledby="settings-title">
      {notificationKind !== null ? (
        <Toast notificationId={notificationId} title={notification.title} message={notification.message} onDismiss={() => setNotificationKind(null)} />
      ) : null}

      <div className="enter-softly mx-auto w-full max-w-[54rem] pb-16 pt-7 text-base leading-7 sm:pt-9 sm:text-lg">
        <button type="button" onClick={onBack} className="inline-flex min-h-11 items-center gap-2 rounded-lg pr-3 font-semibold text-ink-soft transition hover:text-pomegranate">
          <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
          Back to conversation
        </button>

        <div className="mt-3 max-w-[46rem]">
          <p className="text-sm font-semibold uppercase tracking-[0.16em] text-pomegranate">Settings</p>
          <h1 id="settings-title" className="mt-2 font-display text-[clamp(2.15rem,4vw,3.1rem)] leading-[1.04] tracking-[-0.04em] text-ink">
            Account and usage.
          </h1>
          <p className="mt-3 max-w-[43rem] text-ink-soft">
            Manage account access, review your allowance, choose conversation defaults, and control your data.
          </p>
        </div>

        <div className="mt-7">
          <SettingsSection icon={<ShieldCheck aria-hidden="true" />} title="Account security" description="Manage the email and password associated with your account.">
            <div className="space-y-6">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <p className="font-semibold text-ink">Account email</p>
                  <p className="mt-1 break-words text-muted">{user.email}</p>
                </div>
                <span className="inline-flex w-fit shrink-0 items-center rounded-full border border-line-strong bg-stone px-3 py-1 text-sm font-semibold leading-5 text-ink-soft sm:text-base">{user.isEmailVerified ? 'Verified email' : 'Email not verified'}</span>
              </div>
              <div>
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="max-w-[28rem]">
                    <p className="font-semibold text-ink">Password</p>
                    <p className="mt-1 text-muted">Request a secure link to choose a new password.</p>
                  </div>
                  <button type="button" disabled={isRequestingPasswordReset} onClick={() => void handlePasswordReset()} className="inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-lg border border-line-strong bg-paper px-4 font-semibold text-ink transition hover:border-ink/35 hover:bg-stone disabled:cursor-wait disabled:opacity-60">
                    <KeyRound aria-hidden="true" className="size-4" strokeWidth={1.8} />
                    {isRequestingPasswordReset ? 'Requesting...' : 'Reset password'}
                  </button>
                </div>
                {passwordResetError ? <p className="mt-3 font-medium text-pomegranate" role="alert">{passwordResetError}</p> : null}
              </div>
            </div>
          </SettingsSection>

          <SettingsSection icon={<Gauge aria-hidden="true" />} title="Usage" description="Your chat allowance resets each month.">
            <UsagePanel usage={usage} error={usageError} isLoading={isLoadingUsage} onRetry={onRetryUsage} />
          </SettingsSection>

          <SettingsSection icon={<BookOpenText aria-hidden="true" />} title="Conversation defaults" description="Choose how new AskRabbi conversations should begin.">
            <div className="space-y-5">
              <PreferenceToggle label="Show source context by default" description="Open the supporting quotations and surrounding text with each answer." icon={<BookOpenText aria-hidden="true" />} isChecked={draft.showSourceContextByDefault} onChange={(value) => updateSetting('showSourceContextByDefault', value)} />
              <PreferenceToggle label="Email me product updates" description="Receive occasional AskRabbi development and feature announcements." icon={<Bell aria-hidden="true" />} isChecked={draft.emailProductUpdates} onChange={(value) => updateSetting('emailProductUpdates', value)} />
            </div>

            <div className="mt-7 flex justify-end">
              {saveError === null ? null : <p className="mr-auto text-pomegranate" role="alert">{saveError}</p>}
              <button type="button" disabled={isSaving} onClick={() => void handleSave()} className="inline-flex h-12 shrink-0 items-center justify-center gap-2 rounded-lg bg-pomegranate px-5 font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-wait disabled:opacity-60">
                <Save aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.8} />
                {isSaving ? 'Saving…' : 'Save settings'}
              </button>
            </div>
          </SettingsSection>

          <SettingsSection icon={<Smartphone aria-hidden="true" />} title="App and offline" description="Keep your learning close, with or without a connection.">
            <InstallAppPanel />
            <div className="mt-7"><OfflineLearningSettings /></div>
          </SettingsSection>

          <SettingsSection icon={<Database aria-hidden="true" />} title="Your data" description="You’re in control of what you keep. Manage your conversations or permanently leave AskRabbi.">
            <UserDataControls isBusy={isDataBusy || isSaving || isRequestingPasswordReset} onDeleteChats={onDeleteChats} onDeleteAccount={onDeleteAccount} />
          </SettingsSection>
        </div>
      </div>
    </section>
  )
}

function UsagePanel({ usage, error, isLoading, onRetry }: { usage: UsageSummary | null; error: string | null; isLoading: boolean; onRetry(): void }) {
  if (isLoading) {
    return <div className="text-muted" role="status">Loading current usage…</div>
  }
  if (error !== null) {
    return (
      <div>
        <p className="font-medium text-pomegranate" role="alert">{error}</p>
        <button type="button" onClick={onRetry} className="mt-3 font-semibold text-ink transition hover:text-pomegranate">Try again</button>
      </div>
    )
  }
  if (usage === null) {
    return <div className="text-muted">Usage is not available.</div>
  }

  const percentage = usage.isLimitReached ? 0 : 100 - Math.min(100, Math.max(0, usage.usedPercent))
  return (
    <div>
      <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <p className="font-semibold text-ink">Monthly usage limit</p>
          <p className="mt-1 text-sm leading-6 text-muted">Resets {formatResetDate(usage.periodEndUtc)}</p>
        </div>
        <div className="flex items-center gap-3 sm:shrink-0">
          <div className="h-2 flex-1 overflow-hidden rounded-full bg-stone-deep sm:w-28 sm:flex-none" role="progressbar" aria-label="Monthly chat allowance remaining" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percentage} aria-valuetext={`${formatUsageRemainingPercent(usage)}% left`}>
            <div className="h-full rounded-full bg-pomegranate" style={{ width: `${percentage}%` }} />
          </div>
          <p className="min-w-18 text-right text-sm font-semibold tabular-nums text-ink-soft">{formatUsageRemainingPercent(usage)}% left</p>
        </div>
      </div>
      {usage.isLimitReached ? <p className="mt-3 font-medium text-pomegranate" role="status">Your chat allowance is used up until the next reset. Dvar Torah reading and audio remain available.</p> : null}
    </div>
  )
}

function formatResetDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit', timeZoneName: 'short' }).format(new Date(value))
}

interface SettingsSectionProps {
  icon: ReactNode
  title: string
  description: string
  children: ReactNode
}

function SettingsSection({ icon, title, description, children }: SettingsSectionProps) {
  return (
    <section className="py-6 sm:py-7" aria-label={title}>
      <div className="grid gap-6 md:grid-cols-[12rem_1fr] md:gap-10">
        <div>
          <div className="flex items-center gap-2.5 text-ink [&_svg]:size-[1.15rem] [&_svg]:text-pomegranate [&_svg]:stroke-[1.7]">
            {icon}
            <h2 className="font-display text-2xl">{title}</h2>
          </div>
          <p className="mt-2 text-muted">{description}</p>
        </div>
        <div className="min-w-0 rounded-2xl bg-stone/65 p-5 sm:p-6">{children}</div>
      </div>
    </section>
  )
}

interface PreferenceToggleProps {
  label: string
  description: string
  icon: ReactNode
  isChecked: boolean
  onChange(value: boolean): void
}

function PreferenceToggle({ label, description, icon, isChecked, onChange }: PreferenceToggleProps) {
  return (
    <div className="flex items-start gap-4">
      <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-full bg-stone text-ink [&_svg]:size-4 [&_svg]:stroke-[1.7]">{icon}</span>
      <div className="min-w-0 flex-1">
        <p className="font-semibold text-ink">{label}</p>
        <p className="mt-1 text-muted">{description}</p>
      </div>
      <button type="button" role="switch" aria-checked={isChecked} aria-label={label} onClick={() => onChange(!isChecked)} className={`relative mt-1 h-7 w-12 shrink-0 rounded-full transition ${isChecked ? 'bg-pomegranate' : 'bg-stone-deep'}`}>
        <span className={`absolute left-0 top-1 size-5 rounded-full bg-paper shadow-sm transition-transform ${isChecked ? 'translate-x-6' : 'translate-x-1'}`} />
      </button>
    </div>
  )
}
