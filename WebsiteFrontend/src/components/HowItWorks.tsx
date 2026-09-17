import { BookOpen } from 'lucide-react'

const Steps = [
  { title: 'Ask in your own words', description: 'Bring a question about a text, a practice, or an idea. Choose the source collections you want to explore.' },
  { title: 'Explore the evidence', description: 'The system searches the selected texts, checks quotations, and reviews its answer. Open citations to read the passages yourself.' },
  { title: 'Make room for nuance', description: 'Follow the reasoning, notice differences in interpretation, and ask another question. When support is limited, uncertainty should be visible.' },
] as const

export function HowItWorks() {
  return (
    <section id="how-it-works" aria-labelledby="how-heading" className="site-container section-space">
      <p className="section-label">How it works</p>
      <h2 id="how-heading" className="section-heading mt-5 max-w-5xl">From a question to a deeper understanding.</h2>
      <ol className="mt-12 grid gap-9 md:grid-cols-3 md:gap-12">
        {Steps.map((step, index) => (
          <li key={step.title} className="border-t border-line pt-5">
            <span className="text-lg text-pomegranate" aria-hidden="true">0{index + 1}</span>
            <h3 className="mt-4 font-display text-[1.65rem] leading-snug tracking-[-0.025em]">{step.title}</h3>
            <p className="mt-3 text-base leading-[1.8] text-ink-soft">{step.description}</p>
          </li>
        ))}
      </ol>
      <aside aria-labelledby="companion-heading" className="companion-note">
        <div className="hidden self-stretch border-r border-line pr-8 sm:flex sm:items-center">
          <BookOpen aria-hidden="true" size={42} strokeWidth={1.5} className="shrink-0 text-brass" />
        </div>
        <div>
          <h3 id="companion-heading" className="font-display text-[1.65rem] tracking-[-0.025em]">A companion for learning</h3>
          <p className="mt-2 text-[0.95rem] leading-[1.8] text-ink-soft">AskRabbi is an AI-assisted educational project. It can make mistakes and does not issue personal halakhic rulings. For practical decisions, bring the conversation to a trusted rabbi or teacher.</p>
        </div>
      </aside>
    </section>
  )
}
