const PolicyLinkClassName = 'rounded-sm font-medium text-pomegranate underline underline-offset-4 hover:text-pomegranate-dark focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-pomegranate'

export function ChatPrivacyNotice() {
  return (
    <p className="text-sm leading-6 text-muted sm:text-base">
      For details about using AskRabbi and how we handle your data, read our{' '}
      <a href="https://askarabbi.ai/terms-of-service" target="_blank" rel="noopener noreferrer" aria-label="Terms of Use (opens in a new tab)" className={PolicyLinkClassName}>Terms of Use</a>
      {' '}and{' '}
      <a href="https://askarabbi.ai/privacy-policy" target="_blank" rel="noopener noreferrer" aria-label="Privacy Policy (opens in a new tab)" className={PolicyLinkClassName}>Privacy Policy</a>.
    </p>
  )
}
