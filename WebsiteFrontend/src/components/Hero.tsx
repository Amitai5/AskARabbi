import { ArrowDown } from 'lucide-react'
import { AppLink } from './AppLink.tsx'

const Collections = ['Torah', 'Tanakh', 'Mishnah', 'Talmud', 'And beyond']

export function Hero() {
  return (
    <section aria-labelledby="hero-heading" className="hero-section">
      <div className="site-container hero">
        <div className="hero-copy">
          <h1 id="hero-heading" className="hero-heading">
            <span>Jewish learning</span>
            <span>starts with</span>
            <em>a question.</em>
          </h1>
          <p className="hero-description">Explore Jewish texts, traditions, and ideas with an AI study companion that brings the sources into the conversation.</p>
          <div className="mt-8 flex flex-wrap items-center gap-x-8 gap-y-5 sm:mt-10">
            <AppLink>Start exploring</AppLink>
            <a href="#purpose" className="text-link group">
              Discover the project
              <ArrowDown aria-hidden="true" size={17} strokeWidth={1.65} className="transition-transform group-hover:translate-y-1" />
            </a>
          </div>
          <p className="mt-8 font-display text-lg text-ink-soft sm:mt-10">Rooted in sources. Open to questions.</p>
        </div>
        <div className="hero-art" aria-hidden="true">
          <img src={`${import.meta.env.BASE_URL}library-manuscript.webp`} alt="" width="1122" height="1402" fetchPriority="high" decoding="async" />
        </div>
      </div>
      <div className="site-container">
        <div className="collection-strip">
          <p className="font-display text-[1.5rem] leading-snug tracking-[-0.025em]">A conversation across generations.</p>
          <ul className="collection-list" aria-label="Source collections">
            {Collections.map(collection => <li key={collection}>{collection}</li>)}
          </ul>
        </div>
      </div>
    </section>
  )
}
