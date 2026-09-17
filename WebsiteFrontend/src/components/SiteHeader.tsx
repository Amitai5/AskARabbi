import { AppLink } from './AppLink.tsx'
import { Brand } from './Brand.tsx'

export function SiteHeader() {
  return (
    <header className="border-b border-line/75">
      <div className="site-container site-header">
        <a href="#top" aria-label="AskRabbi home" className="site-brand w-fit rounded-sm"><Brand /></a>
        <nav aria-label="Main navigation" className="main-navigation">
          <a href="#purpose">Our Purpose</a>
          <a href="#experience">The Experience</a>
          <a href="#how-it-works">How It Works</a>
        </nav>
        <AppLink className="header-app-link" />
      </div>
    </header>
  )
}
