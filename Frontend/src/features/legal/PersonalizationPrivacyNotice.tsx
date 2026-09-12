import { LegalLink } from './LegalLinks.tsx'

export function PersonalizationPrivacyNotice() {
  return <p className="text-sm leading-6 text-muted">
    Your profile is saved to your account. Relevant details are processed by our AI and calendar providers to personalize replies and dates. Religion, heritage, and additional context can be sensitive; choose “Prefer not to say” or leave optional context blank. See the <LegalLink document="privacy-policy" section="sensitive-information" /> and <LegalLink document="terms-of-service" />.
  </p>
}
