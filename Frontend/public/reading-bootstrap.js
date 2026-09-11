// Blocking, pre-paint initialization. Keep in sync with readingPreferences.ts's versioned cache.
(() => {
  let preferences = { theme: 'system', textSize: 'default', lineSpacing: 'default' };
  try {
    const user = localStorage.getItem('askarabbi.reading.active-user');
    const cached = user && JSON.parse(localStorage.getItem('askarabbi.reading.v1:' + user) || 'null');
    const value = cached && cached.preferences;
    if (value && ['light', 'dark', 'system'].includes(value.theme)
      && ['small', 'default', 'large', 'extra-large'].includes(value.textSize)
      && ['compact', 'default', 'relaxed'].includes(value.lineSpacing)) {
      preferences = value;
    }
  } catch { /* Continue with system defaults when storage is unavailable. */ }
  const root = document.documentElement;
  root.dataset.theme = preferences.theme === 'system' ? (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light') : preferences.theme;
  root.dataset.readingSize = preferences.textSize;
  root.dataset.readingSpacing = preferences.lineSpacing;
  root.style.colorScheme = root.dataset.theme;
  root.style.backgroundColor = root.dataset.theme === 'dark' ? '#171b22' : '#fbf8f2';
})();
