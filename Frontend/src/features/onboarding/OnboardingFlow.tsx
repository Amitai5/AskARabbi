import { useState, type FormEvent, type ReactNode } from 'react'
import { ArrowLeft, ArrowRight, Check, LockKeyhole, LogOut } from 'lucide-react'
import { Brand } from '../../components/Brand.tsx'
import { LanguageOptions } from '../personalization/languageOptions.ts'
import { JewishHeritageOptions, ReligiousMovementOptions } from '../personalization/personalizationOptions.ts'
import type { PersonalizationProfile } from '../personalization/personalizationTypes.ts'
import { normalizePersonalizationProfile, validatePersonalizationProfile, type PersonalizationErrors } from '../personalization/personalizationValidation.ts'
import { UsTimeZoneOptions } from '../personalization/usTimeZoneOptions.ts'

interface OnboardingFlowProps {
  profile: PersonalizationProfile
  onComplete(profile: PersonalizationProfile): Promise<void>
  onLogout(): Promise<void>
}

type OnboardingStep = 0 | 1 | 2

interface StepDefinition {
  label: string
  fields: readonly (keyof PersonalizationProfile)[]
}

const Steps: readonly StepDefinition[] = [
  { label: 'About you', fields: ['fullName', 'birthDateTime', 'birthTimeZone'] },
  { label: 'Language', fields: ['conversationLanguage', 'quotationLanguage'] },
  { label: 'Jewish background', fields: ['religiousMovement', 'jewishHeritage', 'additionalContext'] },
]

const InputClassName = 'mt-2 h-14 w-full rounded-lg border border-line-strong bg-paper px-4 text-base text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15'

export function OnboardingFlow({ profile, onComplete, onLogout }: OnboardingFlowProps) {
  const [draft, setDraft] = useState(profile)
  const [currentStep, setCurrentStep] = useState<OnboardingStep>(0)
  const [errors, setErrors] = useState<PersonalizationErrors>({})
  const [isLoggingOut, setIsLoggingOut] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  function updateField(field: keyof PersonalizationProfile, value: string) {
    setDraft((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
  }

  function moveToStep(step: OnboardingStep) {
    setErrors({})
    setCurrentStep(step)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaveError(null)
    const stepErrors = getStepErrors(draft, currentStep)
    setErrors(stepErrors)

    if (Object.keys(stepErrors).length > 0) {
      return
    }

    if (currentStep < 2) {
      moveToStep((currentStep + 1) as OnboardingStep)
      return
    }

    setIsSaving(true)
    try {
      await onComplete(normalizePersonalizationProfile(draft))
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'Your personalization could not be saved.')
    } finally {
      setIsSaving(false)
    }
  }

  async function handleLogout() {
    setIsLoggingOut(true)
    try {
      await onLogout()
    } finally {
      setIsLoggingOut(false)
    }
  }

  return (
    <main className="min-h-dvh bg-parchment lg:grid lg:h-dvh lg:grid-cols-[21rem_minmax(0,1fr)] lg:overflow-hidden">
      <aside className="hidden h-dvh flex-col border-r border-line bg-stone px-9 py-9 lg:flex" aria-label="Onboarding progress">
        <Brand />
        <DesktopProgress currentStep={currentStep} onSelectStep={moveToStep} />
        <div className="mt-auto border-t border-line-strong pt-8">
          <p className="flex gap-3 text-sm leading-6 text-ink-soft">
            <LockKeyhole aria-hidden="true" className="mt-0.5 size-[1.15rem] shrink-0 text-brass" strokeWidth={1.7} />
            <span>Your details are used only to personalize your experience.</span>
          </p>
        </div>
      </aside>

      <section className="flex min-h-dvh min-w-0 flex-col lg:h-dvh lg:min-h-0 lg:overflow-y-auto">
        <header className="flex h-[4.75rem] shrink-0 items-center justify-between border-b border-line px-5 sm:px-8 lg:h-20 lg:justify-end lg:border-b-0 lg:px-12">
          <div className="lg:hidden"><Brand compact /></div>
          <LogoutButton isLoggingOut={isLoggingOut} onLogout={handleLogout} />
        </header>

        <MobileProgress currentStep={currentStep} />

        <div className="flex flex-1 px-5 pb-8 pt-8 sm:px-8 sm:pb-12 sm:pt-10 lg:items-center lg:px-14 lg:pb-11 lg:pt-2 xl:px-20">
          <form className="enter-softly mx-auto w-full max-w-[50rem]" key={currentStep} onSubmit={handleSubmit} noValidate>
            <StepHeader currentStep={currentStep} fullName={draft.fullName} />

            <div className="mt-8 sm:mt-10">
              {currentStep === 0 ? <AboutYouStep draft={draft} errors={errors} onChange={updateField} /> : null}
              {currentStep === 1 ? <LanguageStep draft={draft} errors={errors} onChange={updateField} /> : null}
              {currentStep === 2 ? <JewishBackgroundStep draft={draft} errors={errors} onChange={updateField} /> : null}
            </div>

            <div className="mt-8 flex items-center justify-between gap-4 border-t border-line pt-7 sm:mt-10">
              {currentStep === 0 ? (
                <p className="text-sm text-muted">All fields are required.</p>
              ) : (
                <button type="button" onClick={() => moveToStep((currentStep - 1) as OnboardingStep)} className="inline-flex h-12 items-center gap-2 rounded-lg px-2 text-sm font-semibold text-ink transition hover:text-pomegranate">
                  <ArrowLeft aria-hidden="true" className="size-4" strokeWidth={1.8} />
                  Back
                </button>
              )}
              <button type="submit" disabled={isSaving} className="group inline-flex h-14 items-center justify-center gap-3 rounded-lg bg-pomegranate px-6 text-sm font-semibold text-white transition hover:bg-pomegranate-dark disabled:cursor-wait disabled:opacity-60 sm:min-w-48">
                {isSaving ? 'Saving…' : currentStep === 2 ? 'Start a conversation' : 'Continue'}
                <ArrowRight aria-hidden="true" className="size-[1.1rem] transition-transform group-hover:translate-x-0.5" strokeWidth={1.8} />
              </button>
            </div>
            {saveError === null ? null : <p className="mt-4 text-right text-sm font-medium text-pomegranate" role="alert">{saveError}</p>}
          </form>
        </div>
      </section>
    </main>
  )
}

function DesktopProgress({ currentStep, onSelectStep }: { currentStep: OnboardingStep; onSelectStep(step: OnboardingStep): void }) {
  return (
    <ol className="relative mt-20 space-y-10 before:absolute before:bottom-7 before:left-[1.15rem] before:top-7 before:w-px before:bg-line-strong">
      {Steps.map((step, index) => {
        const stepIndex = index as OnboardingStep
        const isComplete = stepIndex < currentStep
        const isCurrent = stepIndex === currentStep
        return (
          <li key={step.label} className="relative">
            <button type="button" disabled={stepIndex >= currentStep} onClick={() => onSelectStep(stepIndex)} aria-current={isCurrent ? 'step' : undefined} className="group flex min-h-11 w-full items-center gap-4 text-left disabled:cursor-default">
              <span className={`relative z-10 flex size-9 shrink-0 items-center justify-center rounded-full border text-sm font-semibold transition ${isCurrent ? 'border-pomegranate bg-pomegranate text-white' : isComplete ? 'border-ink bg-ink text-white group-hover:border-pomegranate group-hover:bg-pomegranate' : 'border-line-strong bg-stone text-muted'}`}>
                {isComplete ? <Check aria-hidden="true" className="size-4" strokeWidth={2} /> : index + 1}
              </span>
              <span className={`text-[0.95rem] ${isCurrent ? 'font-semibold text-ink' : isComplete ? 'font-medium text-ink-soft group-hover:text-pomegranate' : 'text-muted'}`}>{step.label}</span>
            </button>
          </li>
        )
      })}
    </ol>
  )
}

function MobileProgress({ currentStep }: { currentStep: OnboardingStep }) {
  return (
    <nav className="border-b border-line px-6 py-5 lg:hidden" aria-label="Onboarding progress">
      <ol className="mx-auto flex max-w-sm items-center">
        {Steps.map((step, index) => {
          const isComplete = index < currentStep
          const isCurrent = index === currentStep
          return (
            <li key={step.label} className={`flex items-center ${index < Steps.length - 1 ? 'flex-1' : ''}`} aria-current={isCurrent ? 'step' : undefined}>
              <span className={`flex size-8 shrink-0 items-center justify-center rounded-full border text-xs font-semibold ${isCurrent ? 'border-pomegranate bg-pomegranate text-white' : isComplete ? 'border-ink bg-ink text-white' : 'border-line-strong bg-parchment text-muted'}`}>
                {isComplete ? <Check aria-hidden="true" className="size-3.5" strokeWidth={2.2} /> : index + 1}
              </span>
              <span className="sr-only">{step.label}{isComplete ? ', complete' : isCurrent ? ', current step' : ''}</span>
              {index < Steps.length - 1 ? <span className={`mx-2 h-px flex-1 ${index < currentStep ? 'bg-ink' : 'bg-line-strong'}`} aria-hidden="true" /> : null}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}

function LogoutButton({ isLoggingOut, onLogout }: { isLoggingOut: boolean; onLogout(): Promise<void> }) {
  return (
    <button type="button" disabled={isLoggingOut} onClick={() => void onLogout()} className="inline-flex min-h-11 items-center gap-2 rounded-lg px-2 text-sm font-semibold text-ink-soft transition hover:text-pomegranate disabled:cursor-wait disabled:opacity-60">
      <LogOut aria-hidden="true" className="size-4 lg:hidden" strokeWidth={1.8} />
      {isLoggingOut ? 'Logging out…' : 'Log out'}
    </button>
  )
}

function StepHeader({ currentStep, fullName }: { currentStep: OnboardingStep; fullName: string }) {
  const firstName = fullName.trim().split(/\s+/)[0] || 'there'
  const content = currentStep === 0
    ? { title: <><span className="block">Welcome, {firstName}.</span><span className="block">Let’s make AskRabbi yours.</span></>, description: 'A few details help tailor explanations and traditions to you. You can update these anytime.' }
    : currentStep === 1
      ? { title: 'Choose your languages.', description: 'Choose how AskRabbi speaks with you and quotes Jewish texts.' }
      : { title: 'Your Jewish background.', description: 'These details help surface customs that may matter to you, without defining your identity.' }

  return (
    <div>
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-brass" aria-live="polite">Step {currentStep + 1} of {Steps.length}</p>
      <h1 className="mt-3 font-display text-[clamp(2.35rem,5vw,4rem)] leading-[1.02] tracking-[-0.045em] text-ink">
        {content.title}
      </h1>
      <p className="mt-5 max-w-[42rem] text-base leading-7 text-ink-soft sm:text-lg">{content.description}</p>
    </div>
  )
}

interface StepProps {
  draft: PersonalizationProfile
  errors: PersonalizationErrors
  onChange(field: keyof PersonalizationProfile, value: string): void
}

function AboutYouStep({ draft, errors, onChange }: StepProps) {
  return (
    <div className="space-y-6">
      <FormField label="Full name" htmlFor="onboarding-full-name" error={errors.fullName} isRequired>
        <input id="onboarding-full-name" name="fullName" type="text" autoComplete="name" maxLength={120} required value={draft.fullName} onChange={(event) => onChange('fullName', event.target.value)} className={InputClassName} aria-invalid={errors.fullName !== undefined} aria-describedby={errors.fullName ? 'onboarding-full-name-error' : undefined} />
      </FormField>

      <FormField label="Birth date and time" htmlFor="onboarding-birth-date-time" error={errors.birthDateTime} hint="Use the local time at your birthplace." isRequired>
        <input id="onboarding-birth-date-time" name="birthDateTime" type="datetime-local" required value={draft.birthDateTime} onInput={(event) => onChange('birthDateTime', event.currentTarget.value)} className={InputClassName} aria-invalid={errors.birthDateTime !== undefined} aria-describedby={errors.birthDateTime ? 'onboarding-birth-date-time-error onboarding-birth-date-time-hint' : 'onboarding-birth-date-time-hint'} />
      </FormField>

      <FormField label="Birth time zone" htmlFor="onboarding-birth-time-zone" error={errors.birthTimeZone} hint="Choose the time zone that applied where you were born." isRequired>
        <select id="onboarding-birth-time-zone" name="birthTimeZone" required value={draft.birthTimeZone} onChange={(event) => onChange('birthTimeZone', event.target.value)} className={InputClassName} aria-invalid={errors.birthTimeZone !== undefined} aria-describedby={errors.birthTimeZone ? 'onboarding-birth-time-zone-error onboarding-birth-time-zone-hint' : 'onboarding-birth-time-zone-hint'}>
          <option value="">Select a U.S. time zone</option>
          {UsTimeZoneOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
        </select>
      </FormField>
    </div>
  )
}

function LanguageStep({ draft, errors, onChange }: StepProps) {
  return (
    <div className="grid gap-6 sm:grid-cols-2">
      <FormField label="Conversation language" htmlFor="onboarding-conversation-language" error={errors.conversationLanguage} hint="AskRabbi will answer in this language." isRequired>
        <select id="onboarding-conversation-language" name="conversationLanguage" required value={draft.conversationLanguage} onChange={(event) => onChange('conversationLanguage', event.target.value)} className={InputClassName} aria-invalid={errors.conversationLanguage !== undefined} aria-describedby={errors.conversationLanguage ? 'onboarding-conversation-language-error onboarding-conversation-language-hint' : 'onboarding-conversation-language-hint'}>
          {LanguageOptions.map((option) => <option key={option} value={option}>{option}</option>)}
        </select>
      </FormField>

      <FormField label="Torah and source quotations" htmlFor="onboarding-quotation-language" error={errors.quotationLanguage} hint="Prefer quotations in this language when an approved edition is available." isRequired>
        <select id="onboarding-quotation-language" name="quotationLanguage" required value={draft.quotationLanguage} onChange={(event) => onChange('quotationLanguage', event.target.value)} className={InputClassName} aria-invalid={errors.quotationLanguage !== undefined} aria-describedby={errors.quotationLanguage ? 'onboarding-quotation-language-error onboarding-quotation-language-hint' : 'onboarding-quotation-language-hint'}>
          {LanguageOptions.map((option) => <option key={option} value={option}>{option}</option>)}
        </select>
      </FormField>
    </div>
  )
}

function JewishBackgroundStep({ draft, errors, onChange }: StepProps) {
  return (
    <div className="space-y-6">
      <div className="grid gap-6 sm:grid-cols-2">
        <FormField label="Religious movement or practice" htmlFor="onboarding-religious-movement" error={errors.religiousMovement} isRequired>
          <select id="onboarding-religious-movement" name="religiousMovement" required value={draft.religiousMovement} onChange={(event) => onChange('religiousMovement', event.target.value)} className={InputClassName} aria-invalid={errors.religiousMovement !== undefined} aria-describedby={errors.religiousMovement ? 'onboarding-religious-movement-error' : undefined}>
            <option value="">Select an option</option>
            {ReligiousMovementOptions.map((option) => <option key={option} value={option}>{option}</option>)}
          </select>
        </FormField>

        <FormField label="Heritage or community" htmlFor="onboarding-jewish-heritage" error={errors.jewishHeritage} isRequired>
          <select id="onboarding-jewish-heritage" name="jewishHeritage" required value={draft.jewishHeritage} onChange={(event) => onChange('jewishHeritage', event.target.value)} className={InputClassName} aria-invalid={errors.jewishHeritage !== undefined} aria-describedby={errors.jewishHeritage ? 'onboarding-jewish-heritage-error' : undefined}>
            <option value="">Select an option</option>
            {JewishHeritageOptions.map((option) => <option key={option} value={option}>{option}</option>)}
          </select>
        </FormField>
      </div>

      <FormField label="Anything else?" htmlFor="onboarding-additional-context" error={errors.additionalContext}>
        <textarea id="onboarding-additional-context" name="additionalContext" maxLength={2_000} rows={5} value={draft.additionalContext} onChange={(event) => onChange('additionalContext', event.target.value)} placeholder="Optional — share family customs, accessibility needs, or topics you want explained differently." className="mt-2 w-full resize-y rounded-lg border border-line-strong bg-paper px-4 py-3 text-base leading-6 text-ink shadow-sm transition placeholder:text-muted/70 hover:border-ink/35 focus:border-pomegranate focus:outline-none focus:ring-2 focus:ring-pomegranate/15" aria-invalid={errors.additionalContext !== undefined} aria-describedby={errors.additionalContext ? 'onboarding-additional-context-error' : undefined} />
      </FormField>
    </div>
  )
}

interface FormFieldProps {
  label: string
  htmlFor: string
  error?: string
  hint?: string
  isRequired?: boolean
  children: ReactNode
}

function FormField({ label, htmlFor, error, hint, isRequired = false, children }: FormFieldProps) {
  return (
    <div>
      <label htmlFor={htmlFor} className="text-sm font-semibold text-ink">
        {label}{isRequired ? <span className="ml-1 text-pomegranate" aria-hidden="true">*</span> : null}
        {isRequired ? <span className="sr-only"> (required)</span> : null}
      </label>
      {children}
      {hint ? <p id={`${htmlFor}-hint`} className="mt-2 text-xs leading-5 text-muted">{hint}</p> : null}
      {error ? <p id={`${htmlFor}-error`} className="mt-2 text-sm font-medium text-pomegranate" role="alert">{error}</p> : null}
    </div>
  )
}

function getStepErrors(profile: PersonalizationProfile, step: OnboardingStep) {
  const profileErrors = validatePersonalizationProfile(profile)
  const stepErrors: PersonalizationErrors = {}

  for (const field of Steps[step].fields) {
    const error = profileErrors[field]
    if (error !== undefined) {
      stepErrors[field] = error
    }
  }

  return stepErrors
}
