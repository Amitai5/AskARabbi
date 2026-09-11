interface ConnectionInformation extends EventTarget {
  effectiveType?: string
  saveData?: boolean
  downlink?: number
  rtt?: number
}

export function getConnectionInformation() {
  return (navigator as Navigator & { connection?: ConnectionInformation }).connection
}

export function canPreloadLearning() {
  const connection = getConnectionInformation()
  return navigator.onLine && document.visibilityState === 'visible' && connection?.effectiveType === '4g' && connection.saveData !== true
    && (connection.downlink === undefined || Number.isFinite(connection.downlink) && connection.downlink >= 1.5)
    && (connection.rtt === undefined || Number.isFinite(connection.rtt) && connection.rtt >= 0 && connection.rtt <= 300)
}

export function scheduleIdlePreload(callback: () => void) {
  if (typeof window.requestIdleCallback === 'function') {
    const id = window.requestIdleCallback(callback)
    return () => window.cancelIdleCallback(id)
  }
  const id = window.setTimeout(callback, 2500)
  return () => window.clearTimeout(id)
}
