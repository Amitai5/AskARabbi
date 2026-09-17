const applicationUrl = new URL(import.meta.env.VITE_APP_URL?.trim() || 'https://app.askarabbi.ai')

if (applicationUrl.protocol !== 'https:' && applicationUrl.protocol !== 'http:') {
  throw new Error('VITE_APP_URL must be an absolute HTTP or HTTPS URL.')
}

export const SiteLinks = {
  application: applicationUrl.href,
  privacy: '/privacy',
  terms: '/terms',
  contact: 'mailto:support@askarabbi.ai',
} as const
