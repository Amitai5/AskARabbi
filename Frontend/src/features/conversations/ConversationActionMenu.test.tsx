import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ConversationActionMenu } from './ConversationActionMenu.tsx'

describe('Conversation action menu', () => {
  afterEach(() => { vi.restoreAllMocks(); document.querySelector('[data-test-anchor]')?.remove() })

  function setup() {
    const anchor = document.createElement('button')
    anchor.dataset.testAnchor = 'true'
    document.body.append(anchor)
    vi.spyOn(anchor, 'getBoundingClientRect').mockReturnValue({ left: 250, right: 290, top: 730, bottom: 766, width: 40, height: 36, x: 250, y: 730, toJSON: () => ({}) })
    vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(136)
    vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockReturnValue(208)
    const onClose = vi.fn()
    const onRename = vi.fn()
    const onDelete = vi.fn()
    const onPrint = vi.fn()
    const { container } = render(<ConversationActionMenu anchor={anchor} title="Parashat Nitzavim" onClose={onClose} onRename={onRename} onPrint={onPrint} onDelete={onDelete} />)
    return { anchor, onClose, onRename, onPrint, onDelete, container }
  }

  it('portals out of the scrolling list and opens above a bottom-row trigger', () => {
    const { container } = setup()
    const menu = screen.getByRole('menu')
    expect(container).not.toContainElement(menu)
    expect(menu.parentElement).toBe(document.body)
    expect(menu).toHaveStyle({ top: '588px', left: '82px' })
    expect(screen.getByRole('menuitem', { name: 'Rename' })).toHaveFocus()
  })

  it('supports arrow navigation and restores focus with Escape', async () => {
    const user = userEvent.setup()
    const { anchor, onClose } = setup()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: 'Print' })).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: 'Delete' })).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: 'Rename' })).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(anchor).toHaveFocus()
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('prints from the keyboard and returns focus to the sidebar trigger', async () => {
    const user = userEvent.setup()
    const { anchor, onPrint, onDelete } = setup()
    await user.keyboard('{ArrowDown}{Enter}')
    expect(onPrint).toHaveBeenCalledOnce()
    expect(onDelete).not.toHaveBeenCalled()
    expect(anchor).toHaveFocus()
  })

  it('dismisses when the list scrolls or the user clicks outside', () => {
    const { onClose } = setup()
    fireEvent.scroll(window)
    fireEvent.pointerDown(document.body)
    expect(onClose).toHaveBeenCalledTimes(2)
  })
})
