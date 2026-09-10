export async function registerServiceWorker(): Promise<ServiceWorkerRegistration | null> {
  if (!import.meta.env.PROD || !window.isSecureContext || !('serviceWorker' in navigator)) {
    return null
  }

  try {
    return await navigator.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' })
  } catch {
    // Offline support is optional; an unavailable worker must not prevent sign-in or normal web use.
    console.warn('AskRabbi offline support could not be initialized. The website is still available online.')
    return null
  }
}
