import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { SourceReader } from './SourceReader.tsx'
import type { ConversationSource } from './conversationData.ts'

describe('SourceReader quotation direction', () => {
  it.each([
    ['English', 'Hear, Israel.', 'English source context.'],
    ['Hebrew', 'שְׁמַע יִשְׂרָאֵל', 'הקשר המקור בעברית.'],
    ['Persian', 'بشنو ای اسرائیل', 'متن زمینه به فارسی.'],
    ['Yiddish', 'הער, ישׂראל.', 'דער קאָנטעקסט אויף ייִדיש.'],
  ])('keeps %s quotations and context independent of the page direction', (language, quotation, context) => {
    const source: ConversationSource = { number: 1, title: 'Deuteronomy', hebrewTitle: 'דברים', canonicalReference: 'Deuteronomy 6:4', edition: 'Display fixture', language, collection: 'Torah', license: 'Test fixture', sourceUrl: 'https://example.test/source', attributionUrl: '', quotations: [quotation], context, isExcerpt: false }

    render(<SourceReader messageId="test" sources={[source]} selectedIndex={0} showSourceContextByDefault onSelectSourceNumber={vi.fn()} onClose={vi.fn()} />)

    for (const element of screen.getAllByText(`“${quotation}”`)) {
      expect(element.tagName).toBe('BLOCKQUOTE')
      expect(element).toHaveAttribute('dir', 'auto')
    }
    for (const element of screen.getAllByText(context)) {
      expect(element).toHaveAttribute('dir', 'auto')
      expect(element).toHaveClass('border-s', 'ps-4')
    }
  })
})
