import { act, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AssistantMessage } from './AssistantMessage.tsx'
import type { ConversationMessage } from './conversationData.ts'

const Message: ConversationMessage = {
  id: 'answer-with-sources',
  role: 'Assistant',
  content: 'Choose life. [3]',
  createdAtUtc: '2026-09-01T12:00:00Z',
  sources: [{
    number: 3, title: 'Deuteronomy', hebrewTitle: 'דברים', canonicalReference: 'Deuteronomy 30:19',
    edition: 'Hebrew edition', language: 'he', collection: 'Torah', license: 'CC-BY-SA',
    sourceUrl: 'https://www.sefaria.org/Deuteronomy.30.19',
    attributionUrl: 'https://www.sefaria.org/texts/Tanakh',
    quotations: ['וּבָחַרְתָּ בַּחַיִּים'], context: 'More surrounding text.', isExcerpt: true,
  }],
}

describe('answer copy actions', () => {
  afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers() })

  it('offers distinct named actions and explanations, with source navigation still available', async () => {
    const user = userEvent.setup()
    const onSelectSource = vi.fn()
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={onSelectSource} />)

    const actions = screen.getByRole('group', { name: 'Answer actions' })
    const text = within(actions).getByRole('button', { name: 'Copy text' })
    const sources = within(actions).getByRole('button', { name: 'Copy with sources' })
    expect(text).toHaveAttribute('title', 'Copy the answer text only.')
    expect(sources).toHaveAttribute('title', 'Copy the answer with numbered sources, links, quotations, and attribution.')
    expect(text).toHaveAccessibleDescription('Copy the answer text only.')
    expect(sources).toHaveAccessibleDescription('Copy the answer with numbered sources, links, quotations, and attribution.')

    const citation = screen.getByRole('button', { name: 'View source 3' })
    await user.click(citation)
    expect(onSelectSource).toHaveBeenCalledWith(Message.id, 3, citation)
  })

  it('copies each format from the keyboard and announces success independently', async () => {
    const user = userEvent.setup()
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue(undefined)
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />)

    await user.tab() // Citation
    await user.tab() // Print
    await user.tab()
    const text = screen.getByRole('button', { name: 'Copy text' })
    expect(text).toHaveFocus()
    await user.keyboard('{Enter}')
    expect(writeText).toHaveBeenNthCalledWith(1, Message.content)
    expect(text).toHaveAttribute('title', 'Answer text copied to clipboard.')

    await user.tab()
    const sources = screen.getByRole('button', { name: 'Copy with sources' })
    expect(sources).toHaveFocus()
    await user.keyboard(' ')
    const copied = writeText.mock.calls[1][0]
    expect(copied).toContain('Choose life. [3]\n\nSources\n\n[3] Deuteronomy 30:19')
    expect(copied).toContain('https://www.sefaria.org/Deuteronomy.30.19')
    expect(copied).toContain('License: CC-BY-SA')
    expect(copied).toContain('Attribution: https://www.sefaria.org/texts/Tanakh')
    expect(copied).toContain('“וּבָחַרְתָּ בַּחַיִּים”')
    expect(sources).toHaveAttribute('title', 'Answer with sources copied to clipboard.')
    expect(screen.getAllByRole('status')[0]).toHaveTextContent('Answer text copied to clipboard.')
    expect(screen.getAllByRole('status')[1]).toHaveTextContent('Answer with sources copied to clipboard.')
  })

  it.each(['Copy text', 'Copy with sources'])('reports clipboard rejection and supports retry for %s', async label => {
    const user = userEvent.setup()
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValueOnce(new Error('Denied')).mockResolvedValue(undefined)
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />)
    const button = screen.getByRole('button', { name: label })

    await user.click(button)
    expect(button).toHaveAttribute('title', `${label} failed. Try again.`)
    expect(button).toHaveAttribute('aria-disabled', 'false')
    await user.click(button)

    expect(writeText).toHaveBeenCalledTimes(2)
    expect(button).toHaveAttribute('data-copy-status', 'copied')
    expect(screen.queryByText(`${label} failed. Try again.`)).not.toBeInTheDocument()
  })

  it('reports unavailable clipboard access without claiming success', async () => {
    const user = userEvent.setup()
    vi.spyOn(navigator, 'clipboard', 'get').mockReturnValue(undefined as unknown as Clipboard)
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Copy with sources' }))

    expect(screen.getAllByRole('status')[1]).toHaveTextContent('Copy with sources failed. Try again.')
    expect(screen.getAllByRole('status')[0]).toBeEmptyDOMElement()
  })

  it('prevents duplicate writes while a copy is pending and waits for actual success', async () => {
    const user = userEvent.setup()
    let resolve!: () => void
    const promise = new Promise<void>(complete => { resolve = complete })
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockReturnValue(promise)
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />)
    const button = screen.getByRole('button', { name: 'Copy with sources' })

    await user.dblClick(button)
    expect(writeText).toHaveBeenCalledTimes(1)
    expect(button).toHaveAttribute('aria-disabled', 'true')
    expect(button).toHaveAttribute('title', 'Copying…')

    await act(async () => resolve())
    expect(button).toHaveAttribute('aria-disabled', 'false')
    expect(button).toHaveAttribute('title', 'Answer with sources copied to clipboard.')
  })

  it('resets feedback after each copy and keeps the source explanation available', async () => {
    vi.useFakeTimers()
    userEvent.setup()
    vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue(undefined)
    render(<AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />)
    const button = screen.getByRole('button', { name: 'Copy with sources' })

    await act(async () => { fireEvent.click(button) })
    act(() => vi.advanceTimersByTime(1_500))
    await act(async () => { fireEvent.click(button) })
    act(() => vi.advanceTimersByTime(1_500))
    expect(button).toHaveAttribute('title', 'Answer with sources copied to clipboard.')
    act(() => vi.advanceTimersByTime(500))
    expect(button).toHaveAttribute('title', 'Copy the answer with numbered sources, links, quotations, and attribution.')
    expect(screen.getAllByRole('status')[1]).toBeEmptyDOMElement()
  })

  it('copies only the chosen answer and never includes another answer’s sources', async () => {
    const user = userEvent.setup()
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue(undefined)
    const olderMessage = { ...Message, id: 'older-answer', content: 'An older answer.', sources: undefined }
    render(<>
      <AssistantMessage message={Message} selectedSourceNumber={null} onSelectSource={vi.fn()} />
      <AssistantMessage message={olderMessage} selectedSourceNumber={null} onSelectSource={vi.fn()} />
    </>)

    const olderActions = screen.getAllByRole('group', { name: 'Answer actions' })[1]
    await user.click(within(olderActions).getByRole('button', { name: 'Copy with sources' }))

    expect(writeText).toHaveBeenCalledWith('An older answer.')
  })
})
