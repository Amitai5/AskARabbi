export function Purpose() {
  return (
    <section id="purpose" aria-labelledby="purpose-heading">
      <div className="site-container purpose-layout">
        <div>
          <p className="section-label">Our purpose</p>
          <h2 id="purpose-heading" className="section-heading mt-5">More understanding.<br />More room for you.</h2>
        </div>
        <div className="space-y-6 text-[1.0625rem] leading-[1.85] text-ink-soft lg:pt-11">
          <p>Jewish learning begins with a question. But finding your way through centuries of texts, commentary, and debate can be daunting.</p>
          <p>AskRabbi makes that path easier to follow: helping you see where ideas come from, how interpretations develop, and where traditions differ.</p>
        </div>
      </div>
      <div className="bg-stone py-12 sm:py-16">
        <div className="site-container">
          <div className="border-l-2 border-pomegranate pl-7 sm:pl-11">
            <h3 className="font-display text-[clamp(2rem,4vw,3.25rem)] leading-tight tracking-[-0.035em]">Explain, never judge.</h3>
            <p className="mt-5 max-w-3xl text-[1.0625rem] leading-[1.8] text-ink-soft sm:text-xl">Your background, your questions, and your choices deserve respect.<br className="hidden sm:block" /> Learning should invite you in.</p>
          </div>
        </div>
      </div>
    </section>
  )
}
