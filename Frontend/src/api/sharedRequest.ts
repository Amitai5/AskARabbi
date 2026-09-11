// Each consumer can cancel independently. The fetch stops only when nobody still needs it.
export function createSharedRequest<T>(load: (signal: AbortSignal) => Promise<T>) {
  const controller = new AbortController()
  let readers = 0
  let settled = false
  const promise = Promise.resolve().then(() => { controller.signal.throwIfAborted(); return load(controller.signal) })
  void promise.then(() => { settled = true }, () => { settled = true })
  return {
    signal: controller.signal,
    abort: () => controller.abort(),
    read(signal?: AbortSignal): Promise<T> {
      if (signal?.aborted) { return Promise.reject(signal.reason) }
      readers++
      return new Promise<T>((resolve, reject) => {
        let finished = false
        function finish() {
          if (finished) { return }
          finished = true
          signal?.removeEventListener('abort', cancel)
          readers--
          if (readers === 0 && !settled) { controller.abort() }
        }
        function cancel() { finish(); reject(signal?.reason ?? new DOMException('Aborted', 'AbortError')) }
        signal?.addEventListener('abort', cancel, { once: true })
        void promise.then(value => { finish(); resolve(value) }, error => { finish(); reject(error) })
      })
    },
  }
}
