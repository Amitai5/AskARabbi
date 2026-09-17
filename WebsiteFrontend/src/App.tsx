import { Experience } from './components/Experience.tsx'
import { Hero } from './components/Hero.tsx'
import { HowItWorks } from './components/HowItWorks.tsx'
import { Purpose } from './components/Purpose.tsx'
import { Invitation, SiteFooter } from './components/SiteFooter.tsx'
import { SiteHeader } from './components/SiteHeader.tsx'

export function App() {
  return (
    <div id="top">
      <a href="#main" className="skip-link">Skip to content</a>
      <SiteHeader />
      <main id="main" tabIndex={-1}>
        <Hero />
        <Purpose />
        <Experience />
        <HowItWorks />
        <Invitation />
      </main>
      <SiteFooter />
    </div>
  )
}
