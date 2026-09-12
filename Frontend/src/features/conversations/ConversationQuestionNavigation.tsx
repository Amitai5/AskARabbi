import { useEffect, useId, useMemo, useRef, useState, type RefObject } from 'react'
import { ArrowDownToLine, List, X } from 'lucide-react'
import { normalizeDisplayText } from '../../displayText.ts'
import type { ConversationMessage } from './conversationData.ts'

import { MinimumNavigationQuestions, questionElementId } from './questionNavigation.ts'

interface ConversationQuestionNavigationProps {
  messages: readonly ConversationMessage[]
  scrollRef: RefObject<HTMLElement | null>
  onNavigate(isLatest: boolean): void
}

export function ConversationQuestionNavigation({ messages, scrollRef, onNavigate }: ConversationQuestionNavigationProps) {
  const questions = useMemo(() => messages.filter(message => message.role === 'User').map(message => ({ id: message.id, text: normalizeDisplayText(message.content).trim() })), [messages])
  const [activeId, setActiveId] = useState<string | null>(null)
  const [previewId, setPreviewId] = useState<string | null>(null)
  const [isOpen, setIsOpen] = useState(false)
  const navigationRef = useRef<HTMLElement>(null)
  const browseRef = useRef<HTMLButtonElement>(null)
  const listRef = useRef<HTMLDivElement>(null)
  const railRef = useRef<HTMLDivElement>(null)
  const listId = useId()

  useEffect(() => {
    const container = scrollRef.current
    if (!container) { return }
    let frame = 0
    function updateActiveQuestion() {
      frame = 0
      if (!container) { return }
      const boundary = container.getBoundingClientRect().top + 48
      let current: string | null = questions[0]?.id ?? null
      for (const question of questions) {
        const element = document.getElementById(questionElementId(question.id))
        if (element && element.getBoundingClientRect().top <= boundary) { current = question.id }
      }
      if (container.scrollHeight > container.clientHeight && container.scrollTop + container.clientHeight >= container.scrollHeight - 8) { current = questions.at(-1)?.id ?? null }
      setActiveId(current)
    }
    function scheduleUpdate() {
      if (!frame) { frame = requestAnimationFrame(updateActiveQuestion) }
    }
    scheduleUpdate()
    container.addEventListener('scroll', scheduleUpdate, { passive: true })
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(scheduleUpdate)
    observer?.observe(container)
    if (container.firstElementChild) { observer?.observe(container.firstElementChild) }
    return () => {
      container.removeEventListener('scroll', scheduleUpdate)
      observer?.disconnect()
      cancelAnimationFrame(frame)
    }
  }, [questions, scrollRef])

  useEffect(() => {
    const rail = railRef.current
    const selected = rail?.querySelector<HTMLElement>('[aria-current="location"]')
    if (rail && selected) {
      rail.scrollTo({ top: Math.max(0, selected.offsetTop - rail.clientHeight / 2 + selected.offsetHeight / 2), behavior: 'auto' })
    }
  }, [activeId])

  useEffect(() => {
    if (!isOpen) { return }
    listRef.current?.querySelector<HTMLButtonElement>('[aria-current="location"], [data-question-link]')?.focus()
    function handleOutsideClick(event: PointerEvent) {
      if (event.target instanceof Node && !navigationRef.current?.contains(event.target)) { setIsOpen(false) }
    }
    document.addEventListener('pointerdown', handleOutsideClick)
    return () => document.removeEventListener('pointerdown', handleOutsideClick)
  }, [isOpen])

  function jumpToQuestion(id: string) {
    const container = scrollRef.current
    const target = document.getElementById(questionElementId(id))
    if (!container || !target) { return }
    onNavigate(false)
    setIsOpen(false)
    setPreviewId(null)
    setActiveId(id)
    target.focus({ preventScroll: true })
    // Only move the conversation pane, never the page, sidebar, or composer.
    container.scrollTo({ top: Math.max(0, container.scrollTop + target.getBoundingClientRect().top - container.getBoundingClientRect().top - 20), behavior: 'auto' })
  }

  function jumpToLatest() {
    onNavigate(true)
    setIsOpen(false)
    setPreviewId(null)
    browseRef.current?.focus({ preventScroll: true })
    const container = scrollRef.current
    container?.scrollTo({ top: container.scrollHeight, behavior: 'auto' })
  }

  function closeList() { setIsOpen(false); browseRef.current?.focus() }

  const preview = questions.find(question => question.id === previewId)
  if (questions.length < MinimumNavigationQuestions) { return null }

  return (
    <nav ref={navigationRef} aria-label="Questions in this conversation" className="reading-nonessential absolute right-3 top-1/2 z-20 hidden -translate-y-1/2 flex-col items-center gap-1 md:flex" onKeyDown={event => {
      if (event.key === 'Escape') { event.stopPropagation(); closeList(); setPreviewId(null) }
      if (event.key === 'Tab' && event.target instanceof HTMLElement && listRef.current?.contains(event.target)) {
        const buttons = [...listRef.current.querySelectorAll<HTMLButtonElement>('button')]
        if ((!event.shiftKey && event.target === buttons.at(-1)) || (event.shiftKey && event.target === buttons[0])) { setIsOpen(false); browseRef.current?.focus(); event.preventDefault() }
      }
    }}>
      <button ref={browseRef} type="button" aria-label="Browse questions" title="Browse questions" aria-expanded={isOpen} aria-controls={listId} onClick={() => { setIsOpen(value => !value); setPreviewId(null) }} className="flex size-10 items-center justify-center rounded-lg border border-transparent bg-parchment/95 text-muted transition hover:border-line hover:bg-stone hover:text-ink">
        <List aria-hidden="true" className="size-4" />
      </button>
      <div ref={railRef} className="relative flex max-h-[min(52vh,28rem)] flex-col overflow-y-auto overscroll-contain [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {questions.map((question, index) => (
          <button key={question.id} type="button" aria-label={`Question ${index + 1}: ${question.text}`} aria-current={activeId === question.id ? 'location' : undefined} onClick={() => jumpToQuestion(question.id)} onMouseEnter={() => setPreviewId(question.id)} onMouseLeave={() => setPreviewId(null)} onFocus={() => setPreviewId(question.id)} onBlur={() => setPreviewId(null)} className="group flex h-6 w-10 shrink-0 items-center justify-center rounded-md hover:bg-stone focus-visible:bg-stone">
            <span aria-hidden="true" className={`h-0.5 rounded-full transition-all group-hover:w-6 group-hover:bg-ink group-focus-visible:w-6 ${activeId === question.id ? 'w-6 bg-pomegranate' : 'w-2.5 bg-muted/45'}`} />
          </button>
        ))}
      </div>
      <button type="button" aria-label="Latest answer" title="Latest answer" onClick={jumpToLatest} className="flex size-10 items-center justify-center rounded-lg border border-transparent bg-parchment/95 text-muted transition hover:border-line hover:bg-stone hover:text-ink">
        <ArrowDownToLine aria-hidden="true" className="size-4" />
      </button>
      {preview && !isOpen ? <div aria-hidden="true" className="pointer-events-none absolute right-12 top-1/2 w-72 -translate-y-1/2 rounded-xl border border-line bg-paper p-4 text-sm text-ink shadow-menu"><p className="mb-1 text-xs text-muted">Question {questions.findIndex(question => question.id === preview.id) + 1}</p><p dir="auto" className="line-clamp-4 break-words">{preview.text}</p></div> : null}
      {isOpen ? <div id={listId} ref={listRef} className="absolute right-12 top-1/2 flex max-h-[min(70vh,36rem)] w-80 -translate-y-1/2 flex-col rounded-xl border border-line-strong bg-paper p-2 shadow-menu">
        <div className="flex items-center justify-between gap-2 border-b border-line px-2 pb-2"><p className="text-sm font-semibold text-ink">Your questions <span className="font-normal text-muted">({questions.length})</span></p><button type="button" aria-label="Close question list" onClick={closeList} className="flex size-9 items-center justify-center rounded-lg text-muted hover:bg-stone"><X aria-hidden="true" className="size-4" /></button></div>
        <ol className="min-h-0 overflow-y-auto overscroll-contain py-1">
          {questions.map((question, index) => <li key={question.id}><button type="button" data-question-link aria-current={activeId === question.id ? 'location' : undefined} onClick={() => jumpToQuestion(question.id)} className={`flex w-full items-start gap-3 rounded-lg px-3 py-3 text-left text-sm text-ink hover:bg-stone ${activeId === question.id ? 'bg-stone font-semibold' : ''}`}><span aria-hidden="true" className="shrink-0 text-xs leading-5 text-muted">{index + 1}</span><span dir="auto" className="line-clamp-3 break-words">{question.text}</span></button></li>)}
        </ol>
        <button type="button" onClick={jumpToLatest} className="flex min-h-11 items-center justify-center gap-2 border-t border-line text-sm font-medium text-pomegranate hover:bg-stone"><ArrowDownToLine aria-hidden="true" className="size-4" />Latest answer</button>
      </div> : null}
    </nav>
  )
}
