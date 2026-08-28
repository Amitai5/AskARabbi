export const LanguageOptions = [
  'English',
  'French',
  'German',
  'Hebrew',
  'Italian',
  'Persian',
  'Polish',
  'Russian',
  'Spanish',
  'Yiddish',
] as const

export const LanguageValues = new Set<string>(LanguageOptions)
