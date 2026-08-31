import { useEffect, useState } from 'react'
import { createApiClient } from './api/apiClient.ts'
import { Brand } from './components/Brand.tsx'
import { AuthProvider } from './features/auth/AuthProvider.tsx'
import { createBackendAuthClient } from './features/auth/backendAuthClient.ts'
import { LoginPage } from './features/auth/LoginPage.tsx'
import { PasswordResetPage } from './features/auth/PasswordResetPage.tsx'
import type { AuthClient, AuthenticatedUser } from './features/auth/authTypes.ts'
import { useAuth } from './features/auth/useAuth.ts'
import { ConversationDashboard } from './features/conversations/ConversationDashboard.tsx'
import { createBackendConversationClient, type ConversationClient } from './features/conversations/conversationClient.ts'
import { OnboardingFlow } from './features/onboarding/OnboardingFlow.tsx'
import { createBackendConversationSettingsClient, type ConversationSettingsClient } from './features/personalization/conversationSettingsClient.ts'
import { createDefaultPersonalizationProfile, type PersonalizationProfile } from './features/personalization/personalizationTypes.ts'
import type { UserSettings } from './features/settings/settingsTypes.ts'

const DefaultApiClient = createApiClient()
const DefaultAuthClient = createBackendAuthClient({ apiClient: DefaultApiClient })
const DefaultConversationClient = createBackendConversationClient(DefaultApiClient)
const DefaultConversationSettingsClient = createBackendConversationSettingsClient(DefaultApiClient)

interface AppProps {
  authClient?: AuthClient
  conversationClient?: ConversationClient
  conversationSettingsClient?: ConversationSettingsClient
}

export function App({ authClient = DefaultAuthClient, conversationClient = DefaultConversationClient, conversationSettingsClient = DefaultConversationSettingsClient }: AppProps) {
  return (
    <AuthProvider client={authClient}>
      <AuthenticatedApplication conversationClient={conversationClient} conversationSettingsClient={conversationSettingsClient} />
    </AuthProvider>
  )
}

interface AuthenticatedApplicationProps {
  conversationClient: ConversationClient
  conversationSettingsClient: ConversationSettingsClient
}

function AuthenticatedApplication({ conversationClient, conversationSettingsClient }: AuthenticatedApplicationProps) {
  const { isInitializing, signOut, user } = useAuth()
  const resetToken = getPasswordResetToken()

  if (resetToken !== null) {
    return <PasswordResetPage token={resetToken} onReturnToLogin={returnToLogin} />
  }
  if (user === null) {
    return <LoginPage isCheckingSession={isInitializing} />
  }

  return <SignedInApplication user={user} conversationClient={conversationClient} conversationSettingsClient={conversationSettingsClient} onLogout={signOut} />
}

function getPasswordResetToken() {
  if (window.location.pathname !== '/reset-password') {
    return null
  }
  const token = new URLSearchParams(window.location.search).get('token')?.trim()
  return token && token.length > 0 ? token : null
}

function returnToLogin() {
  window.location.assign('/')
}

interface SignedInApplicationProps {
  user: AuthenticatedUser
  conversationClient: ConversationClient
  conversationSettingsClient: ConversationSettingsClient
  onLogout(): Promise<void>
}

function SignedInApplication({ user, conversationClient, conversationSettingsClient, onLogout }: SignedInApplicationProps) {
  const [profile, setProfile] = useState<PersonalizationProfile | null>(null)
  const [isConfigured, setIsConfigured] = useState(false)
  const [userSettings, setUserSettings] = useState<UserSettings | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let isCurrent = true

    void Promise.all([conversationSettingsClient.getPersonalization(), conversationSettingsClient.getPreferences()])
      .then(([envelope, preferences]) => {
        if (!isCurrent) {
          return
        }

        setIsConfigured(envelope.isConfigured)
        const defaultProfile = createDefaultPersonalizationProfile(user)
        setProfile(envelope.personalization ?? { ...defaultProfile, birthTimeZone: '' })
        setUserSettings(preferences)
      })
      .catch((error: unknown) => {
        if (isCurrent) {
          setLoadError(getErrorMessage(error, 'Your account settings could not be loaded.'))
        }
      })
      .finally(() => {
        if (isCurrent) {
          setIsLoading(false)
        }
      })

    return () => {
      isCurrent = false
    }
  }, [conversationSettingsClient, reloadKey, user])

  async function savePersonalization(nextProfile: PersonalizationProfile) {
    const saved = await conversationSettingsClient.updatePersonalization(nextProfile)
    setProfile(saved)
    setIsConfigured(true)
  }

  async function saveUserSettings(nextSettings: UserSettings) {
    const saved = await conversationSettingsClient.updatePreferences(nextSettings)
    setUserSettings(saved)
    return saved
  }

  function retryLoad() {
    setIsLoading(true)
    setLoadError(null)
    setReloadKey((current) => current + 1)
  }

  if (isLoading) {
    return <LoadingScreen message="Loading your account…" />
  }
  if (loadError !== null || profile === null || userSettings === null) {
    return <AccountLoadError message={loadError ?? 'Your personalization response was incomplete.'} onRetry={retryLoad} onLogout={onLogout} />
  }
  if (!isConfigured) {
    return <OnboardingFlow profile={profile} onComplete={savePersonalization} onLogout={onLogout} />
  }

  return <ConversationDashboard user={user} initialPersonalizationProfile={profile} initialUserSettings={userSettings} conversationClient={conversationClient} conversationSettingsClient={conversationSettingsClient} onSavePersonalization={savePersonalization} onSaveSettings={saveUserSettings} />
}

function LoadingScreen({ message }: { message: string }) {
  return (
    <main className="flex min-h-dvh items-center justify-center bg-parchment px-6">
      <div className="text-center">
        <div className="flex justify-center"><Brand /></div>
        <p className="mt-6 text-sm text-muted" role="status">{message}</p>
      </div>
    </main>
  )
}

function AccountLoadError({ message, onRetry, onLogout }: { message: string; onRetry(): void; onLogout(): Promise<void> }) {
  return (
    <main className="flex min-h-dvh items-center justify-center bg-parchment px-6">
      <div className="w-full max-w-lg text-center">
        <div className="flex justify-center"><Brand /></div>
        <h1 className="mt-8 font-display text-3xl text-ink">We couldn’t load your account.</h1>
        <p className="mt-4 text-sm leading-6 text-ink-soft" role="alert">{message}</p>
        <div className="mt-7 flex justify-center gap-3">
          <button type="button" onClick={onRetry} className="h-11 rounded-lg bg-pomegranate px-5 text-sm font-semibold text-white transition hover:bg-pomegranate-dark">Try again</button>
          <button type="button" onClick={() => void onLogout()} className="h-11 rounded-lg border border-line-strong px-5 text-sm font-semibold text-ink transition hover:bg-stone">Log out</button>
        </div>
      </div>
    </main>
  )
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error && error.message.trim().length > 0 ? error.message : fallback
}

export default App
