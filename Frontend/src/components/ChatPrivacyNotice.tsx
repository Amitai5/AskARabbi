import { LegalLink } from '../features/legal/LegalLinks.tsx'

export function ChatPrivacyNotice() {
  return (
    <p className="text-sm leading-6 text-muted sm:text-base">
      For details about using AskRabbi and how we handle your data, read our{' '}
      <LegalLink document="terms-of-service">Terms of Use</LegalLink>
      {' '}and{' '}
      <LegalLink document="privacy-policy" />.
    </p>
  )
}
