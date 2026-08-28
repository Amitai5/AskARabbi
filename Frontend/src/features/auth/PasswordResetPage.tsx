import { useState, type FormEvent } from 'react'
import { ArrowLeft, KeyRound } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import { useAuth } from './useAuth.ts'

interface PasswordResetPageProps {
  token: string
  onReturnToLogin(): void
}

export function PasswordResetPage({ token, onReturnToLogin }: PasswordResetPageProps) {
  const { confirmPasswordReset } = useAuth()
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isComplete, setIsComplete] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (password.length < 12) {
      setError('Use at least 12 characters for your new password.')
      return
    }
    if (password !== confirmation) {
      setError('The passwords do not match.')
      return
    }

    setError(null)
    setIsSubmitting(true)
    try {
      await confirmPasswordReset(token, password)
      setIsComplete(true)
      setPassword('')
      setConfirmation('')
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'The password reset link could not be used.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="flex min-h-dvh items-center justify-center bg-parchment px-6 py-10">
      <section className="w-full max-w-[32rem]">
        <Brand />
        <div className="mt-10 border-t border-line pt-8">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-pomegranate">Account security</p>
          <h1 className="mt-2 font-display text-4xl tracking-[-0.035em] text-ink">Choose a new password.</h1>
          {isComplete ? (
            <div className="mt-6">
              <p className="text-base leading-7 text-ink-soft" role="status">Your password was updated. Sign in again to continue.</p>
              <button type="button" onClick={onReturnToLogin} className="mt-6 inline-flex h-12 items-center justify-center gap-2 rounded-lg bg-pomegranate px-5 text-sm font-semibold text-white transition hover:bg-pomegranate-dark">
                Return to sign in
              </button>
            </div>
          ) : (
            <form className="mt-7 space-y-5" onSubmit={handleSubmit}>
              <PasswordField id="new-password" label="New password" value={password} onChange={setPassword} />
              <PasswordField id="confirm-password" label="Confirm new password" value={confirmation} onChange={setConfirmation} />
              {error === null ? null : <p className="text-sm leading-6 text-pomegranate" role="alert">{error}</p>}
              <button type="submit" disabled={isSubmitting} className="inline-flex h-12 w-full items-center justify-center gap-2 rounded-lg bg-pomegranate px-5 text-sm font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-wait disabled:opacity-60">
                <KeyRound aria-hidden="true" className="size-4" strokeWidth={1.8} />
                {isSubmitting ? 'Updating…' : 'Update password'}
              </button>
            </form>
          )}
          {!isComplete ? (
            <button type="button" onClick={onReturnToLogin} className="mt-6 inline-flex min-h-11 items-center gap-2 text-sm font-semibold text-ink-soft transition hover:text-pomegranate">
              <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
              Back to sign in
            </button>
          ) : null}
        </div>
      </section>
    </main>
  )
}

function PasswordField({ id, label, value, onChange }: { id: string; label: string; value: string; onChange(value: string): void }) {
  return (
    <label htmlFor={id} className="block text-sm font-semibold text-ink">
      {label}
      <input id={id} type="password" autoComplete="new-password" required value={value} onChange={(event) => onChange(event.target.value)} className="mt-2 h-12 w-full rounded-lg border border-line-strong bg-paper px-4 text-base text-ink outline-none transition focus:border-pomegranate focus:ring-3 focus:ring-pomegranate/10" />
    </label>
  )
}
