import { useState, type FormEvent, type ReactNode } from 'react'
import { ArrowLeft, Clock3, Languages, Save, UserRound, UsersRound } from 'lucide-react'
import { Toast } from '../../components/Toast.tsx'
import { LanguageOptions } from './languageOptions.ts'
import { JewishHeritageOptions, ReligiousMovementOptions } from './personalizationOptions.ts'
import type { PersonalizationProfile } from './personalizationTypes.ts'
import { normalizePersonalizationProfile, validatePersonalizationProfile, type PersonalizationErrors } from './personalizationValidation.ts'
import { UsTimeZoneOptions } from './usTimeZoneOptions.ts'

interface PersonalizationPageProps {
  profile: PersonalizationProfile
  onBack(): void
  onSave(profile: PersonalizationProfile): Promise<void>
}

const InputClassName = 'mt-2 h-12 w-full rounded-lg border border-line-strong bg-paper px-3.5 text-[0.95rem] text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15'

export function PersonalizationPage({ profile, onBack, onSave }: PersonalizationPageProps) {
  const [draft, setDraft] = useState(profile)
  const [errors, setErrors] = useState<PersonalizationErrors>({})
  const [saveNotificationId, setSaveNotificationId] = useState(0)
  const [isSaving, setIsSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  function updateField(field: keyof PersonalizationProfile, value: string) {
    setDraft((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
    setSaveNotificationId(0)
    setSaveError(null)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const nextErrors = validatePersonalizationProfile(draft)
    setErrors(nextErrors)

    if (Object.keys(nextErrors).length > 0) {
      return
    }

    const normalized = normalizePersonalizationProfile(draft)
    setDraft(normalized)
    setIsSaving(true)
    try {
      await onSave(normalized)
      setSaveNotificationId((current) => current + 1)
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'Your personalization could not be saved.')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <section className="min-h-0 flex-1 overflow-y-auto px-4 sm:px-8" aria-labelledby="personalization-title">
      {saveNotificationId > 0 ? (
        <Toast notificationId={saveNotificationId} title="Saved to your account" message="AskRabbi will use these preferences in future conversations." onDismiss={() => setSaveNotificationId(0)} />
      ) : null}

      <div className="enter-softly mx-auto w-full max-w-[54rem] pb-16 pt-7 sm:pt-9">
        <button type="button" onClick={onBack} className="inline-flex min-h-11 items-center gap-2 rounded-lg pr-3 text-sm font-semibold text-ink-soft transition hover:text-pomegranate">
          <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
          Back to conversation
        </button>

        <div className="mt-3 max-w-[46rem]">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-pomegranate">Personalization</p>
          <h1 id="personalization-title" className="mt-2 font-display text-[clamp(2.15rem,4vw,3.1rem)] leading-[1.04] tracking-[-0.04em] text-ink">
            Make AskRabbi yours.
          </h1>
          <p className="mt-3 max-w-[43rem] text-sm leading-6 text-ink-soft sm:text-base">
            A few details help us tailor explanations without defining what you believe or practice.
          </p>
        </div>

        <form className="mt-7" onSubmit={handleSubmit} noValidate>
          <FormSection icon={<UserRound aria-hidden="true" />} title="About you" description="For your name, age, and future Hebrew-birthday calculation.">
            <div className="grid gap-6 sm:grid-cols-2">
              <FormField label="Full name" htmlFor="full-name" error={errors.fullName}>
                <input id="full-name" name="fullName" type="text" autoComplete="name" maxLength={120} required value={draft.fullName} onChange={(event) => updateField('fullName', event.target.value)} className={InputClassName} aria-invalid={errors.fullName !== undefined} aria-describedby={errors.fullName ? 'full-name-error' : undefined} />
              </FormField>

              <FormField label="Birth date and time" htmlFor="birth-date-time" error={errors.birthDateTime} hint="Use the local time at your birthplace.">
                <input id="birth-date-time" name="birthDateTime" type="datetime-local" required value={draft.birthDateTime} onInput={(event) => updateField('birthDateTime', event.currentTarget.value)} className={InputClassName} aria-invalid={errors.birthDateTime !== undefined} aria-describedby={errors.birthDateTime ? 'birth-date-time-error birth-date-time-hint' : 'birth-date-time-hint'} />
              </FormField>

              <div className="sm:col-span-2">
                <FormField label="Birth time zone" htmlFor="birth-time-zone" error={errors.birthTimeZone} hint="Choose the time zone that applied where you were born.">
                  <div className="relative">
                    <Clock3 aria-hidden="true" className="pointer-events-none absolute left-3.5 top-[1.1rem] size-[1.1rem] text-muted" strokeWidth={1.7} />
                    <select id="birth-time-zone" name="birthTimeZone" required value={draft.birthTimeZone} onChange={(event) => updateField('birthTimeZone', event.target.value)} className={`${InputClassName} pl-10`} aria-invalid={errors.birthTimeZone !== undefined} aria-describedby={errors.birthTimeZone ? 'birth-time-zone-error birth-time-zone-hint' : 'birth-time-zone-hint'}>
                      <option value="">Select a U.S. time zone</option>
                      {UsTimeZoneOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                    </select>
                  </div>
                </FormField>
              </div>
            </div>

            <div className="mt-6 border-l-2 border-brass bg-stone/55 px-4 py-3 text-sm leading-6 text-ink-soft">
              A time zone gives us the regional date context. If your birth was near sunset, an exact Hebrew-date calculation may still ask for your birthplace later.
            </div>
          </FormSection>

          <FormSection icon={<Languages aria-hidden="true" />} title="Language" description="Choose how AskRabbi speaks and quotes Jewish texts.">
            <div className="grid gap-6 sm:grid-cols-2">
              <FormField label="Conversation language" htmlFor="conversation-language" error={errors.conversationLanguage} hint="AskRabbi will answer in this language.">
                <select id="conversation-language" name="conversationLanguage" required value={draft.conversationLanguage} onChange={(event) => updateField('conversationLanguage', event.target.value)} className={InputClassName} aria-invalid={errors.conversationLanguage !== undefined} aria-describedby={errors.conversationLanguage ? 'conversation-language-error conversation-language-hint' : 'conversation-language-hint'}>
                  {LanguageOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              </FormField>

              <FormField label="Torah and source quotations" htmlFor="quotation-language" error={errors.quotationLanguage} hint="Prefer quotations in this language when an approved edition is available.">
                <select id="quotation-language" name="quotationLanguage" required value={draft.quotationLanguage} onChange={(event) => updateField('quotationLanguage', event.target.value)} className={InputClassName} aria-invalid={errors.quotationLanguage !== undefined} aria-describedby={errors.quotationLanguage ? 'quotation-language-error quotation-language-hint' : 'quotation-language-hint'}>
                  {LanguageOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              </FormField>
            </div>

            <div className="mt-6 border-l-2 border-brass bg-stone/55 px-4 py-3 text-sm leading-6 text-ink-soft">
              When the preferred quotation language is unavailable, AskRabbi should say so and use the closest approved text rather than inventing a translation.
            </div>
          </FormSection>

          <FormSection icon={<UsersRound aria-hidden="true" />} title="Jewish background" description="Helps surface customs that may matter to you without defining your identity.">
            <div className="grid gap-6 sm:grid-cols-2">
              <FormField label="Religious movement or practice" htmlFor="religious-movement" error={errors.religiousMovement}>
                <select id="religious-movement" name="religiousMovement" required value={draft.religiousMovement} onChange={(event) => updateField('religiousMovement', event.target.value)} className={InputClassName} aria-invalid={errors.religiousMovement !== undefined} aria-describedby={errors.religiousMovement ? 'religious-movement-error' : undefined}>
                  <option value="">Select an option</option>
                  {ReligiousMovementOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              </FormField>

              <FormField label="Heritage or community" htmlFor="jewish-heritage" error={errors.jewishHeritage}>
                <select id="jewish-heritage" name="jewishHeritage" required value={draft.jewishHeritage} onChange={(event) => updateField('jewishHeritage', event.target.value)} className={InputClassName} aria-invalid={errors.jewishHeritage !== undefined} aria-describedby={errors.jewishHeritage ? 'jewish-heritage-error' : undefined}>
                  <option value="">Select an option</option>
                  {JewishHeritageOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
              </FormField>
            </div>
          </FormSection>

          <FormSection icon={<UserRound aria-hidden="true" />} title="Anything else?" description="Optional context for a more useful conversation.">
            <FormField label="Additional information" htmlFor="additional-context" error={errors.additionalContext} hint="Optional. For example: what you do, what you are studying, family customs, accessibility needs, or topics you want explained differently.">
              <textarea id="additional-context" name="additionalContext" maxLength={2_000} rows={7} value={draft.additionalContext} onChange={(event) => updateField('additionalContext', event.target.value)} className="mt-2 w-full resize-y rounded-lg border border-line-strong bg-paper px-3.5 py-3 text-[0.95rem] leading-6 text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15" aria-invalid={errors.additionalContext !== undefined} aria-describedby={errors.additionalContext ? 'additional-context-error additional-context-hint additional-context-count' : 'additional-context-hint additional-context-count'} />
              <p id="additional-context-count" className="mt-2 text-right text-xs text-muted">{draft.additionalContext.length.toLocaleString()} / 2,000</p>
            </FormField>
          </FormSection>

          <div className="flex justify-end border-t border-line pt-7">
            <button type="submit" disabled={isSaving} className="inline-flex h-12 shrink-0 items-center justify-center gap-2 rounded-lg bg-pomegranate px-5 text-sm font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-wait disabled:opacity-60">
              <Save aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.8} />
              {isSaving ? 'Saving…' : 'Save personalization'}
            </button>
          </div>
          {saveError === null ? null : <p className="mt-4 text-right text-sm font-medium text-pomegranate" role="alert">{saveError}</p>}
        </form>
      </div>
    </section>
  )
}

interface FormSectionProps {
  icon: ReactNode
  title: string
  description: string
  children: ReactNode
}

function FormSection({ icon, title, description, children }: FormSectionProps) {
  return (
    <section className="border-t border-line py-7 sm:py-8">
      <div className="grid gap-6 md:grid-cols-[12rem_1fr] md:gap-10">
        <div>
          <div className="flex items-center gap-2.5 text-ink [&_svg]:size-[1.15rem] [&_svg]:text-pomegranate [&_svg]:stroke-[1.7]">
            {icon}
            <h2 className="font-display text-xl">{title}</h2>
          </div>
          <p className="mt-2 text-sm leading-6 text-muted">{description}</p>
        </div>
        <div>{children}</div>
      </div>
    </section>
  )
}

interface FormFieldProps {
  label: string
  htmlFor: string
  error?: string
  hint?: string
  children: ReactNode
}

function FormField({ label, htmlFor, error, hint, children }: FormFieldProps) {
  return (
    <div>
      <label htmlFor={htmlFor} className="text-sm font-semibold text-ink">{label}</label>
      {children}
      {hint ? <p id={`${htmlFor}-hint`} className="mt-2 text-xs leading-5 text-muted">{hint}</p> : null}
      {error ? <p id={`${htmlFor}-error`} className="mt-2 text-sm font-medium text-pomegranate" role="alert">{error}</p> : null}
    </div>
  )
}
