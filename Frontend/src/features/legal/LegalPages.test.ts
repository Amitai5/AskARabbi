import { describe, expect, it } from 'vitest'
import privacyHtml from '../../../privacy-policy.html?raw'
import termsHtml from '../../../terms-of-service.html?raw'

const Pages = [
  { path: '/privacy-policy', title: 'Privacy Policy', html: privacyHtml },
  { path: '/terms-of-service', title: 'Terms of Service', html: termsHtml },
]

function readPage(path: string) {
  const page = Pages.find(value => value.path === path)
  if (!page) { throw new Error(`Unknown legal page: ${path}`) }
  return new DOMParser().parseFromString(page.html, 'text/html')
}

describe('Distributed public legal documents', () => {
  it.each(Pages)('provides $path as a complete HTML document without authentication or application JavaScript', ({ path, title }) => {
    const page = readPage(path)
    expect(page.title).toBe(`${title} | AskRabbi`)
    expect(page.querySelectorAll('h1')).toHaveLength(1)
    expect(page.querySelector('h1')?.textContent).toBe(title)
    expect(page.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe(`https://askarabbi.ai${path}`)
    expect(page.querySelector('time')?.getAttribute('datetime')).toBe(path === '/privacy-policy' ? '2026-09-17' : '2026-09-11')
    expect(page.querySelector('a[href="mailto:support@askarabbi.ai"]')).not.toBeNull()
    expect(page.querySelector('article')?.textContent).toContain('Settings & Personalization')
    expect(page.body.textContent).not.toMatch(/OPERATOR_PENDING|JURISDICTION_PENDING/)
    expect(page.querySelector('script[type="module"]')).toBeNull()
    expect(page.querySelector('form')).toBeNull()
  })

  it.each(Pages)('keeps all section links and cross-document links on $path valid', ({ path }) => {
    const page = readPage(path)
    const ids = [...page.querySelectorAll('[id]')].map(element => element.id)
    expect(new Set(ids).size).toBe(ids.length)
    for (const link of page.querySelectorAll<HTMLAnchorElement>('a[href]')) {
      const href = link.getAttribute('href')!
      if (href.startsWith('#')) {
        expect(page.getElementById(href.slice(1)), href).not.toBeNull()
      } else if (href.startsWith('/privacy-policy') || href.startsWith('/terms-of-service')) {
        const [target, fragment] = href.split('#')
        expect(Pages.some(value => value.path === target), href).toBe(true)
        if (fragment) { expect(readPage(target).getElementById(fragment), href).not.toBeNull() }
      }
    }
  })
})
