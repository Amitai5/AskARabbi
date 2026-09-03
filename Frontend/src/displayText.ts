const TypographyReplacements = new Map([
  ['\u0013', '–'],
  ['\u0014', '—'],
  ['\u0018', '‘'],
  ['\u0019', '’'],
  ['\u001c', '“'],
  ['\u001d', '”'],
  ['\u0085', '…'],
  ['\u0091', '‘'],
  ['\u0092', '’'],
  ['\u0093', '“'],
  ['\u0094', '”'],
  ['\u0096', '–'],
  ['\u0097', '—'],
])

export function normalizeDisplayText(value: string) {
  let normalized = ''
  for (const character of value) {
    const replacement = TypographyReplacements.get(character)
    if (replacement !== undefined) {
      normalized += replacement
      continue
    }

    if (!isUnsupportedControlCharacter(character.charCodeAt(0))) {
      normalized += character
    }
  }
  return normalized
}

function isUnsupportedControlCharacter(characterCode: number) {
  return characterCode <= 0x08
    || characterCode === 0x0b
    || characterCode === 0x0c
    || characterCode >= 0x0e && characterCode <= 0x12
    || characterCode >= 0x15 && characterCode <= 0x17
    || characterCode === 0x1a
    || characterCode === 0x1b
    || characterCode >= 0x1e && characterCode <= 0x1f
    || characterCode >= 0x7f && characterCode <= 0x9f
}
