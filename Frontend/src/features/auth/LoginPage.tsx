import { useState, type FormEvent } from 'react'
import { ArrowRight, LoaderCircle, Mail } from 'lucide-react'
import manuscriptArtwork from '../../assets/library-manuscript.webp'
import { Brand } from '../../components/Brand.tsx'
import { useAuth } from './useAuth.ts'

const EmailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const AuthenticationFeedbackDurationMs = 300

interface LoginPageProps {
  isCheckingSession?: boolean
}

type PendingAuthentication = 'email' | 'google' | null

function AuthenticationSpinner() {
  return <LoaderCircle aria-hidden="true" className="size-5 shrink-0 animate-spin" strokeWidth={2} />
}

function showAuthenticationFeedback() {
  // Authentication replaces the document, so leave enough time for the pending state to paint before navigation.
  return new Promise<void>((resolve) => window.setTimeout(resolve, AuthenticationFeedbackDurationMs))
}

function GoogleMark() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5 shrink-0">
      <path fill="#4285f4" d="M21.6 12.23c0-.71-.06-1.4-.2-2.07H12v3.92h5.38a4.6 4.6 0 0 1-2 3.02v2.55h3.24c1.9-1.75 2.98-4.33 2.98-7.42Z" />
      <path fill="#34a853" d="M12 22c2.7 0 4.96-.9 6.62-2.35l-3.24-2.55c-.9.6-2.05.96-3.38.96-2.61 0-4.82-1.76-5.61-4.13H3.04v2.62A10 10 0 0 0 12 22Z" />
      <path fill="#fbbc05" d="M6.39 13.93A6 6 0 0 1 6.08 12c0-.67.12-1.32.31-1.93V7.45H3.04A10 10 0 0 0 2 12c0 1.62.39 3.15 1.04 4.55l3.35-2.62Z" />
      <path fill="#ea4335" d="M12 5.94c1.47 0 2.79.51 3.83 1.5l2.87-2.88A9.64 9.64 0 0 0 12 2a10 10 0 0 0-8.96 5.45l3.35 2.62C7.18 7.7 9.39 5.94 12 5.94Z" />
    </svg>
  )
}

export function LoginPage({ isCheckingSession = false }: LoginPageProps) {
  const { authenticationError, clearAuthenticationError, isAuthenticating, requestPasswordReset, signInWithEmail, signInWithSocialProvider, signUp } = useAuth()
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isRecoveringPassword, setIsRecoveringPassword] = useState(false)
  const [resetRequested, setResetRequested] = useState(false)
  const [pendingAuthentication, setPendingAuthentication] = useState<PendingAuthentication>(null)
  const isAuthenticationPending = pendingAuthentication !== null || isAuthenticating
  const isEmailAuthenticationPending = pendingAuthentication === 'email'
  const isGoogleAuthenticationPending = pendingAuthentication === 'google'

  async function handleEmailSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalizedEmail = email.trim()

    if (!EmailPattern.test(normalizedEmail)) {
      setError('Enter a valid email address to continue.')
      return
    }

    setError(null)
    clearAuthenticationError()
    if (isRecoveringPassword) {
      try {
        await requestPasswordReset(normalizedEmail)
        setResetRequested(true)
      } catch {
        setError('The password reset request could not be sent. Please try again.')
      }
      return
    }

    setPendingAuthentication('email')
    try {
      await showAuthenticationFeedback()
      await signInWithEmail(normalizedEmail)
    } catch {
      setPendingAuthentication(null)
    }
  }

  async function handleGoogleLogin() {
    setError(null)
    clearAuthenticationError()
    setPendingAuthentication('google')
    try {
      await showAuthenticationFeedback()
      await signInWithSocialProvider('google')
    } catch {
      setPendingAuthentication(null)
    }
  }

  async function handleSignUp() {
    setError(null)
    clearAuthenticationError()
    try {
      await signUp()
    } catch {
      return
    }
  }

  return (
    <main className="grid min-h-dvh bg-parchment lg:grid-cols-[minmax(0,1.05fr)_minmax(28rem,0.95fr)]" aria-busy={isCheckingSession || isAuthenticationPending}>
      {isCheckingSession ? <span className="sr-only" role="status">Checking for an existing session.</span> : null}
      {pendingAuthentication === null ? null : <span className="sr-only" role="status">{pendingAuthentication === 'email' ? 'Continuing with email.' : 'Continuing with Google.'}</span>}
      <section className="flex min-h-dvh flex-col px-6 py-7 sm:px-10 lg:px-16 lg:py-10 xl:px-20">
        <Brand />

        <div className="flex flex-1 items-center py-12 lg:py-8">
          <div className="enter-softly w-full max-w-[39rem]">
            <h1 className="font-display text-[clamp(3rem,5vw,4.9rem)] leading-[0.98] tracking-[-0.045em] text-ink">
              {isRecoveringPassword ? 'Reset your password' : 'Welcome back'}
            </h1>
            <p className="mt-6 max-w-[36rem] text-[1.05rem] leading-7 text-ink-soft sm:text-lg">
              {isRecoveringPassword ? 'Enter your account email and WorkOS will send a secure reset link.' : 'Continue your conversation with Jewish texts and traditions.'}
            </p>

            <form className="mt-10 sm:mt-12" onSubmit={handleEmailSubmit} noValidate>
              <label htmlFor="email" className="text-[0.95rem] font-semibold text-ink">
                Email address
              </label>
              <div className="mt-2.5 flex h-14 items-center rounded-lg border border-line-strong bg-paper px-4 transition focus-within:border-pomegranate focus-within:ring-3 focus-within:ring-pomegranate/10 sm:h-16">
                <Mail aria-hidden="true" className="size-5 shrink-0 text-muted" strokeWidth={1.75} />
                <input
                  id="email"
                  name="email"
                  type="email"
                  autoComplete="email"
                  value={email}
                  disabled={isAuthenticationPending || resetRequested}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="you@example.com"
                  aria-describedby={error === null ? undefined : 'email-error'}
                  aria-invalid={error !== null}
                  className="h-full min-w-0 flex-1 bg-transparent px-3 text-base text-ink outline-none placeholder:text-muted/75"
                />
              </div>
              <div className="min-h-7 pt-1.5">
                {error === null ? null : (
                  <p id="email-error" role="alert" className="text-sm text-pomegranate">
                    {error}
                  </p>
                )}
              </div>

              <button
                type="submit"
                disabled={isAuthenticationPending || resetRequested}
                aria-busy={isEmailAuthenticationPending}
                className="group flex h-14 w-full items-center justify-center gap-3 rounded-lg bg-pomegranate px-5 text-[0.95rem] font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-not-allowed disabled:opacity-60 sm:h-16 sm:text-base"
              >
                <span>{resetRequested ? 'Reset email requested' : isEmailAuthenticationPending ? 'Continuing with email…' : isRecoveringPassword ? 'Send reset link' : 'Continue with email'}</span>
                {isEmailAuthenticationPending ? <AuthenticationSpinner /> : <ArrowRight aria-hidden="true" className="size-5 transition-transform group-hover:translate-x-0.5" strokeWidth={1.75} />}
              </button>
            </form>

            {isRecoveringPassword ? (
              <button type="button" disabled={isAuthenticationPending} onClick={() => { setIsRecoveringPassword(false); setResetRequested(false); setError(null) }} className="mt-5 text-sm font-semibold text-ink-soft transition hover:text-pomegranate disabled:cursor-not-allowed disabled:opacity-60">
                Back to sign in
              </button>
            ) : (
              <button type="button" disabled={isAuthenticationPending} onClick={() => { setIsRecoveringPassword(true); setError(null); clearAuthenticationError() }} className="mt-5 text-sm font-semibold text-pomegranate transition hover:text-pomegranate-dark disabled:cursor-not-allowed disabled:opacity-60">
                Forgot your password?
              </button>
            )}

            {!isRecoveringPassword ? <><div className="my-7 flex items-center gap-4 text-sm text-muted" aria-hidden="true">
              <span className="h-px flex-1 bg-line" />
              <span>or</span>
              <span className="h-px flex-1 bg-line" />
            </div>

            <button
              type="button"
              disabled={isAuthenticationPending}
              aria-busy={isGoogleAuthenticationPending}
              onClick={() => void handleGoogleLogin()}
              className="group grid h-14 w-full grid-cols-[1.25rem_1fr_1.25rem] items-center rounded-lg border border-ink bg-transparent px-5 text-[0.95rem] font-semibold text-ink transition hover:bg-stone disabled:cursor-not-allowed disabled:opacity-60 sm:h-16 sm:text-base"
            >
              <GoogleMark />
              <span>{isGoogleAuthenticationPending ? 'Continuing with Google…' : 'Continue with Google'}</span>
              {isGoogleAuthenticationPending ? <AuthenticationSpinner /> : <ArrowRight aria-hidden="true" className="size-5 transition-transform group-hover:translate-x-0.5" strokeWidth={1.75} />}
            </button>

            <p className="mt-8 text-[0.95rem] text-ink-soft">
              New to AskRabbi?{' '}
              <button type="button" disabled={isAuthenticationPending} onClick={() => void handleSignUp()} className="font-semibold text-pomegranate hover:text-pomegranate-dark disabled:cursor-not-allowed disabled:opacity-60">
                Create an account
              </button>
            </p></> : null}
            {authenticationError === null ? null : <p role="alert" className="mt-5 text-sm leading-6 text-pomegranate">{authenticationError}</p>}
            <p className="mt-8 text-sm leading-6 text-muted">
              AskRabbi is a study companion, not a source of binding psak.
            </p>
          </div>
        </div>
      </section>

      <aside className="relative hidden min-h-dvh overflow-hidden border-l border-line bg-parchment lg:block" aria-hidden="true">
        <img src={manuscriptArtwork} alt="" loading="lazy" decoding="async" fetchPriority="low" className="absolute inset-0 h-full w-full object-cover object-center" />
      </aside>
    </main>
  )
}
