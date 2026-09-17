import { ArrowRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { SiteLinks } from '../siteLinks.ts'

export function AppLink({ children = 'Open AskRabbi', className = '' }: { children?: ReactNode; className?: string }) {
  return (
    <a href={SiteLinks.application} className={`app-link group ${className}`}>
      <span>{children}</span>
      <ArrowRight aria-hidden="true" size={19} strokeWidth={1.65} className="shrink-0 transition-transform group-hover:translate-x-1" />
    </a>
  )
}
