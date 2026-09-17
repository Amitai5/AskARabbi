import { AppLink } from './AppLink.tsx'
import { Brand } from './Brand.tsx'
import { SiteLinks } from '../siteLinks.ts'

export function Invitation() {
  return (
    <section aria-labelledby="invitation-heading" className="site-container invitation">
      <h2 id="invitation-heading" className="section-heading">There is always another question.</h2>
      <p className="mt-5 text-[1.0625rem] leading-relaxed text-ink-soft">Begin with what you have been wondering about.</p>
      <AppLink className="mt-8" />
    </section>
  )
}

export function SiteFooter() {
  return (
    <footer className="site-container">
      <div className="site-footer">
        <a href="#top" aria-label="AskRabbi home" className="w-fit rounded-sm"><Brand compact /></a>
        <p className="footer-description">An independent project for thoughtful Jewish learning.</p>
        <nav aria-label="Footer navigation" className="flex flex-wrap items-center gap-7 text-sm text-ink-soft">
          <a href={SiteLinks.privacy} className="footer-link">Privacy</a>
          <a href={SiteLinks.terms} className="footer-link">Terms</a>
          <a href={SiteLinks.contact} className="footer-link">Contact</a>
        </nav>
      </div>
    </footer>
  )
}
