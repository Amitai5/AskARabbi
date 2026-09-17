import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'

const pages = await Promise.all([
  { path: '/privacy', title: 'Privacy Policy', effectiveDate: '2026-09-17' },
  { path: '/terms', title: 'Terms of Service', effectiveDate: '2026-09-11' },
].map(async page => ({ ...page, html: await readFile(new URL(`../dist${page.path}.html`, import.meta.url), 'utf8') })))

function attributes(html, name) {
  return [...html.matchAll(new RegExp(`\\b${name}="([^"]*)"`, 'g'))].map(match => match[1])
}

for (const page of pages) {
  test(`${page.path} is a complete public document without application JavaScript`, () => {
    assert.match(page.html, /<!doctype html>/i)
    assert.ok(page.html.includes(`<title>${page.title} | AskRabbi</title>`))
    assert.deepEqual([...page.html.matchAll(/<h1\b[^>]*>([^<]*)<\/h1>/g)].map(match => match[1]), [page.title])
    assert.ok(page.html.includes(`rel="canonical" href="https://askarabbi.ai${page.path}"`))
    assert.ok(page.html.includes(`datetime="${page.effectiveDate}"`))
    assert.ok(page.html.includes('href="mailto:support@askarabbi.ai"'))
    assert.ok(page.html.includes('Settings &amp; Personalization'))
    assert.doesNotMatch(page.html, /<script\b|<form\b|OPERATOR_PENDING|JURISDICTION_PENDING/i)
  })

  test(`${page.path} preserves valid section links, policy navigation, and local assets`, async () => {
    const ids = attributes(page.html, 'id')
    assert.equal(new Set(ids).size, ids.length, 'Section IDs must be unique')
    for (const href of attributes(page.html, 'href')) {
      const url = new URL(href, `https://askarabbi.ai${page.path}`)
      if (url.origin !== 'https://askarabbi.ai') { continue }
      if (url.pathname === '/privacy' || url.pathname === '/terms') {
        const target = pages.find(value => value.path === url.pathname)
        if (url.hash) { assert.ok(attributes(target.html, 'id').includes(url.hash.slice(1)), href) }
      } else if (url.pathname !== '/') {
        assert.ok(url.pathname.startsWith('/assets/') || url.pathname === '/favicon.svg', `Unexpected local link: ${href}`)
        await readFile(new URL(`../dist${url.pathname}`, import.meta.url))
      }
    }
    assert.ok(attributes(page.html, 'href').includes('/'))
    assert.ok(attributes(page.html, 'href').includes(page.path === '/privacy' ? '/terms' : '/privacy'))
  })
}

test('the prerendered homepage links to both policy pages without client JavaScript', async () => {
  const homepage = await readFile(new URL('../dist/index.html', import.meta.url), 'utf8')
  assert.match(homepage, /Jewish learning/)
  assert.ok(attributes(homepage, 'href').includes('/privacy'))
  assert.ok(attributes(homepage, 'href').includes('/terms'))
  assert.doesNotMatch(homepage, /<script\b|<!--app-html-->/i)
})

test('existing website policy URLs permanently redirect to the canonical documents', async () => {
  const redirects = (await readFile(new URL('../dist/_redirects', import.meta.url), 'utf8')).trim().split(/\r?\n/)
  for (const [oldPath, newPath] of [['/privacy-policy', '/privacy'], ['/terms-of-service', '/terms']]) {
    for (const suffix of ['', '/', '.html']) {
      assert.ok(redirects.includes(`${oldPath}${suffix} ${newPath} 301`))
    }
  }
})

test('policy responses require revalidation', async () => {
  const headers = (await readFile(new URL('../dist/_headers', import.meta.url), 'utf8')).replaceAll('\r\n', '\n')
  for (const page of pages) {
    for (const suffix of ['', '.html']) {
      assert.ok(headers.includes(`${page.path}${suffix}\n  Cache-Control: no-cache`))
    }
  }
})
