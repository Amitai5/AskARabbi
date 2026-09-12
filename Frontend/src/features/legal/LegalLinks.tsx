import type { ReactNode } from 'react'

export function LegalLink({ document, section, children }: { document: 'privacy-policy' | 'terms-of-service'; section?: string; children?: ReactNode }) {
  return <a href={`/${document}${section ? `#${section}` : ''}`} target="_blank" rel="noopener noreferrer" className="text-pomegranate underline decoration-pomegranate/50 underline-offset-4 hover:decoration-pomegranate focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-pomegranate">{children ?? (document === 'privacy-policy' ? 'Privacy Policy' : 'Terms of Service')}<span className="sr-only"> (opens in a new tab)</span></a>
}

export function LegalLinks() {
  return <span><LegalLink document="privacy-policy" /> · <LegalLink document="terms-of-service" /></span>
}
