import { useState, type FormEvent, type ReactNode } from 'react'
import { ArrowLeft, CheckCircle2, Clock3, MapPin, Save, UserRound, UsersRound } from 'lucide-react'
import { JewishHeritageOptions, ReligiousMovementOptions } from './personalizationOptions.ts'
import type { PersonalizationProfile } from './personalizationTypes.ts'
import { normalizePersonalizationProfile, validatePersonalizationProfile, type PersonalizationErrors } from './personalizationValidation.ts'

interface PersonalizationPageProps {
  profile: PersonalizationProfile
  onBack(): void
  onSave(profile: PersonalizationProfile): void
}

const InputClassName = 'mt-2 h-12 w-full rounded-lg border border-line-strong bg-paper px-3.5 text-[0.95rem] text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15'

export function PersonalizationPage({ profile, onBack, onSave }: PersonalizationPageProps) {
  const [draft, setDraft] = useState(profile)
  const [errors, setErrors] = useState<PersonalizationErrors>({})
  const [isSaved, setIsSaved] = useState(false)

  function updateField(field: keyof PersonalizationProfile, value: string) {
    setDraft((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
    setIsSaved(false)
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const nextErrors = validatePersonalizationProfile(draft)
    setErrors(nextErrors)

    if (Object.keys(nextErrors).length > 0) {
      return
    }

    const normalized = normalizePersonalizationProfile(draft)
    setDraft(normalized)
    onSave(normalized)
    setIsSaved(true)
  }

  return (
    <section className="min-h-0 flex-1 overflow-y-auto px-4 sm:px-8" aria-labelledby="personalization-title">
      <div className="enter-softly mx-auto w-full max-w-[54rem] pb-16 pt-7 sm:pt-11">
        <button type="button" onClick={onBack} className="inline-flex min-h-11 items-center gap-2 rounded-lg pr-3 text-sm font-semibold text-ink-soft transition hover:text-pomegranate">
          <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
          Back to conversation
        </button>

        <div className="mt-5 max-w-[46rem]">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-pomegranate">Personalization</p>
          <h1 id="personalization-title" className="mt-3 font-display text-[clamp(2.4rem,5vw,3.8rem)] leading-[1.03] tracking-[-0.04em] text-ink">
            Help us meet you where you are.
          </h1>
          <p className="mt-5 max-w-[43rem] text-base leading-7 text-ink-soft">
            This context helps AskRabbi explain customs and sources more clearly. It never counts as religious evidence or assumes what you believe or practice.
          </p>
        </div>

        <form className="mt-10" onSubmit={handleSubmit} noValidate>
          <FormSection icon={<UserRound aria-hidden="true" />} title="About you" description="Basic details let the conversation use your name and age-appropriate language.">
            <div className="grid gap-6 sm:grid-cols-2">
              <FormField label="Full name" htmlFor="full-name" error={errors.fullName}>
                <input id="full-name" name="fullName" type="text" autoComplete="name" maxLength={120} required value={draft.fullName} onChange={(event) => updateField('fullName', event.target.value)} className={InputClassName} aria-invalid={errors.fullName !== undefined} aria-describedby={errors.fullName ? 'full-name-error' : undefined} />
              </FormField>

              <FormField label="Birth date and time" htmlFor="birth-date-time" error={errors.birthDateTime} hint="Use the local time at your birthplace.">
                <input id="birth-date-time" name="birthDateTime" type="datetime-local" required value={draft.birthDateTime} onChange={(event) => updateField('birthDateTime', event.target.value)} className={InputClassName} aria-invalid={errors.birthDateTime !== undefined} aria-describedby={errors.birthDateTime ? 'birth-date-time-error birth-date-time-hint' : 'birth-date-time-hint'} />
              </FormField>

              <FormField label="Birthplace" htmlFor="birth-place" error={errors.birthPlace} hint="City and country, such as Los Angeles, United States.">
                <div className="relative">
                  <MapPin aria-hidden="true" className="pointer-events-none absolute left-3.5 top-[1.1rem] size-[1.1rem] text-muted" strokeWidth={1.7} />
                  <input id="birth-place" name="birthPlace" type="text" autoComplete="off" maxLength={160} required value={draft.birthPlace} onChange={(event) => updateField('birthPlace', event.target.value)} placeholder="City, country" className={`${InputClassName} pl-10`} aria-invalid={errors.birthPlace !== undefined} aria-describedby={errors.birthPlace ? 'birth-place-error birth-place-hint' : 'birth-place-hint'} />
                </div>
              </FormField>

              <FormField label="Birth time zone" htmlFor="birth-time-zone" error={errors.birthTimeZone} hint="Use an IANA zone. We detect your current zone as a starting point.">
                <div className="relative">
                  <Clock3 aria-hidden="true" className="pointer-events-none absolute left-3.5 top-[1.1rem] size-[1.1rem] text-muted" strokeWidth={1.7} />
                  <input id="birth-time-zone" name="birthTimeZone" type="text" autoComplete="off" maxLength={100} required value={draft.birthTimeZone} onChange={(event) => updateField('birthTimeZone', event.target.value)} placeholder="America/Los_Angeles" className={`${InputClassName} pl-10`} aria-invalid={errors.birthTimeZone !== undefined} aria-describedby={errors.birthTimeZone ? 'birth-time-zone-error birth-time-zone-hint' : 'birth-time-zone-hint'} />
                </div>
              </FormField>
            </div>

            <div className="mt-6 border-l-2 border-brass bg-stone/55 px-4 py-3 text-sm leading-6 text-ink-soft">
              Birthplace and time zone matter because the Hebrew date changes at sunset. The production backend will calculate and verify your Hebrew birthday; this demo only collects the details it will need.
            </div>
          </FormSection>

          <FormSection icon={<UsersRound aria-hidden="true" />} title="Jewish background" description="Choose the language that best fits you. These labels personalize framing; they do not define your identity.">
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

          <FormSection icon={<UserRound aria-hidden="true" />} title="Anything else?" description="Share only what would help a study partner understand your questions and perspective.">
            <FormField label="Additional information" htmlFor="additional-context" error={errors.additionalContext} hint="Optional. For example: what you do, what you are studying, family customs, accessibility needs, or topics you want explained differently.">
              <textarea id="additional-context" name="additionalContext" maxLength={2_000} rows={7} value={draft.additionalContext} onChange={(event) => updateField('additionalContext', event.target.value)} className="mt-2 w-full resize-y rounded-lg border border-line-strong bg-paper px-3.5 py-3 text-[0.95rem] leading-6 text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15" aria-invalid={errors.additionalContext !== undefined} aria-describedby={errors.additionalContext ? 'additional-context-error additional-context-hint additional-context-count' : 'additional-context-hint additional-context-count'} />
              <p id="additional-context-count" className="mt-2 text-right text-xs text-muted">{draft.additionalContext.length.toLocaleString()} / 2,000</p>
            </FormField>
          </FormSection>

          <div className="flex flex-col-reverse gap-4 border-t border-line pt-7 sm:flex-row sm:items-center sm:justify-between">
            <p className="max-w-[31rem] text-sm leading-6 text-muted">
              For this demo, Save keeps the profile only in this browser tab until you log out or refresh.
            </p>
            <div className="flex items-center gap-4">
              {isSaved ? (
                <p className="flex items-center gap-2 text-sm font-semibold text-ink-soft" role="status">
                  <CheckCircle2 aria-hidden="true" className="size-4 text-pomegranate" />
                  Saved for this session
                </p>
              ) : null}
              <button type="submit" className="inline-flex h-12 shrink-0 items-center justify-center gap-2 rounded-lg bg-pomegranate px-5 text-sm font-semibold text-white transition hover:bg-pomegranate-dark">
                <Save aria-hidden="true" className="size-[1.1rem]" strokeWidth={1.8} />
                Save personalization
              </button>
            </div>
          </div>
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
    <section className="border-t border-line py-8 sm:py-10">
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
