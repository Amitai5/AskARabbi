import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { LegalLink, LegalLinks } from './LegalLinks.tsx'
import { PersonalizationPrivacyNotice } from './PersonalizationPrivacyNotice.tsx'
import { searchSettings } from '../settings/settingsRegistry.ts'
import redirects from '../../../public/_redirects?raw'

describe('Legal policy entry points', () => {
  it('redirects old and current app policy routes to the public website', () => {
    const rules = redirects.trim().split(/\r?\n/)
    for (const [paths, destination] of [[['privacy', 'privacy-policy'], 'https://askarabbi.ai/privacy'], [['terms', 'terms-of-service'], 'https://askarabbi.ai/terms']] as const) {
      for (const path of paths) {
        for (const suffix of ['', '/', '.html']) {
          expect(rules).toContain(`/${path}${suffix} ${destination} 301`)
        }
      }
    }
  })

  it('opens public policies in separate tabs to preserve an unfinished form or chat', () => {
    render(<LegalLinks />)
    for (const [name, path] of [['Privacy Policy', 'https://askarabbi.ai/privacy'], ['Terms of Service', 'https://askarabbi.ai/terms']]) {
      const link = screen.getByRole('link', { name: new RegExp(name) })
      expect(link).toHaveAttribute('href', path)
      expect(link).toHaveAttribute('target', '_blank')
      expect(link).toHaveAttribute('rel', 'noopener noreferrer')
      expect(link).toHaveAccessibleName(new RegExp('opens in a new tab'))
    }
  })

  it('links retention disclosures to their specific policy section', () => {
    render(<LegalLink document="privacy-policy" section="retention">Retention details</LegalLink>)
    expect(screen.getByRole('link', { name: /Retention details/ })).toHaveAttribute('href', 'https://askarabbi.ai/privacy#retention')
  })

  it('explains sensitive profile choices before saving and links the full disclosure', () => {
    render(<PersonalizationPrivacyNotice />)
    expect(screen.getByText(/Religion, heritage, and additional context can be sensitive/)).toBeVisible()
    expect(screen.getByRole('link', { name: /Privacy Policy/ })).toHaveAttribute('href', 'https://askarabbi.ai/privacy#sensitive-information')
    expect(screen.getByRole('link', { name: /Terms of Service/ })).toHaveAttribute('href', 'https://askarabbi.ai/terms')
  })

  it('finds both policies through settings search', () => {
    expect(searchSettings('privacy policy').map(setting => setting.id)).toEqual(['privacy-policy'])
    expect(searchSettings('terms of use').map(setting => setting.id)).toEqual(['terms-of-service'])
  })
})
