import { ArrowRight, BookOpen, Compass, MessageCircle } from 'lucide-react'
import { SiteLinks } from '../siteLinks.ts'
import { Brand } from './Brand.tsx'

const ExplorationSteps = [
  { title: 'Explore the tradition', description: 'Understand the ideas and context behind a practice.' },
  { title: 'Read the sources', description: 'Open exact quotations and the passages around them.' },
  { title: 'Keep asking', description: 'Follow a new question without losing the conversation.' },
] as const

const Features = [
  { icon: MessageCircle, title: 'Questions, with context', description: 'Explore ideas, customs, and different interpretations at your own pace.' },
  { icon: BookOpen, title: 'The text, within reach', description: 'Inspect citations, original wording, and available translations.' },
  { icon: Compass, title: 'Learning that continues', description: 'Return to saved conversations, read a weekly Dvar Torah, and listen when a recording is available.' },
] as const

export function Experience() {
  return (
    <section id="experience" aria-labelledby="experience-heading" className="site-container section-space">
      <p className="section-label">The experience</p>
      <h2 id="experience-heading" className="section-heading mt-5">Follow your curiosity.<br />Find the sources.</h2>
      <p className="section-description mt-5">A thoughtful starting point for a question—and a way to keep learning.</p>

      <div className="experience-layout">
        <article className="exploration-panel" aria-label="An invitation to explore">
          <Brand compact />
          <h3 className="mt-7 font-display text-[clamp(1.8rem,3vw,2.75rem)] leading-[1.13] tracking-[-0.035em]">Why do we light candles<br className="hidden sm:block" /> before Shabbat?</h3>
          <p className="mt-3 text-base leading-relaxed text-ink-soft">Start with a question. Follow the discussion.</p>
          <ol className="mt-7 border-t border-line">
            {ExplorationSteps.map((step, index) => (
              <li key={step.title} className="flex gap-5 border-b border-line py-6 sm:gap-7">
                <span className="pt-1 font-display text-lg text-pomegranate" aria-hidden="true">0{index + 1}</span>
                <div>
                  <h4 className="font-display text-[1.4rem] leading-snug tracking-[-0.02em]">{step.title}</h4>
                  <p className="mt-1 text-[0.95rem] leading-relaxed text-ink-soft">{step.description}</p>
                </div>
              </li>
            ))}
          </ol>
          <a href={SiteLinks.application} className="group mt-6 inline-flex min-h-11 items-center gap-3 font-medium text-pomegranate transition-colors hover:text-pomegranate-dark">
            Explore in AskRabbi
            <ArrowRight aria-hidden="true" size={18} strokeWidth={1.65} className="transition-transform group-hover:translate-x-1" />
          </a>
        </article>

        <div className="feature-list">
          {Features.map(({ icon: Icon, title, description }) => (
            <article key={title} className="flex items-start gap-5 sm:gap-6">
              <Icon aria-hidden="true" size={35} strokeWidth={1.5} className="mt-1 shrink-0 text-brass" />
              <div>
                <h3 className="font-display text-[1.65rem] leading-snug tracking-[-0.025em]">{title}</h3>
                <p className="mt-3 text-base leading-[1.75] text-ink-soft">{description}</p>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
