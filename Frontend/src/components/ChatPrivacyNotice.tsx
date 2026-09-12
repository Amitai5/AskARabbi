import { LegalLink } from '../features/legal/LegalLinks.tsx'

export function ChatPrivacyNotice() {
  return (
    <div className="space-y-3 text-sm leading-6 text-muted sm:text-base">
      <p>AskRabbi saves your questions and answers in your account so you can return to your conversations. You can delete saved chats or your account in Settings &amp; Personalization → Your data.</p>
      <p>We and our service providers may retain and review questions and answers to detect abuse, investigate safety issues, and protect the service. Access for these purposes is limited to authorized personnel.</p>
      <p>Deleting saved chats removes them from your account, but records kept for security and abuse prevention may be retained separately.</p>
      <p>Relevant conversation and profile details are processed by our AI providers. Read the <LegalLink document="privacy-policy" /> for data use, service providers, retention, and your choices.</p>
    </div>
  )
}
