import { describe, expect, it } from 'vitest'
import { normalizeConversationTitle } from './conversationData.ts'

describe('normalizeConversationTitle', () => {
  it('repairs malformed typography in generated conversation titles', () => {
    expect(normalizeConversationTitle('  Joseph\u0019s reunion\u0014explained  ')).toBe('Joseph’s reunion—explained')
  })
})
