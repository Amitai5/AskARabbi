export interface ConversationStarter {
  heading: string
  supportingText: string
}

export const ConversationStarters: ConversationStarter[] = [
  {
    heading: 'What shall we study together?',
    supportingText: 'Ask a question, follow a source, or begin with something you have always wondered.',
  },
  {
    heading: 'Where shall our learning begin?',
    supportingText: 'Bring a question, a text, or a tradition you would like to understand more deeply.',
  },
  {
    heading: 'What question shall we follow?',
    supportingText: 'Start with what feels clear, confusing, meaningful, or simply worth exploring.',
  },
  {
    heading: 'Which text shall we open together?',
    supportingText: 'Ask about a source, a custom, or an idea you have been carrying with you.',
  },
  {
    heading: 'What would you like to understand?',
    supportingText: 'We can trace an idea through the sources, one thoughtful question at a time.',
  },
]

const StarterIndexStorageKey = 'askrabbi.conversation-starter-index.v1'

export function getInitialConversationStarterIndex() {
  return readNextStoredIndex()
}

export function advanceConversationStarterIndex(currentIndex: number) {
  const nextIndex = (currentIndex + 1) % ConversationStarters.length
  writeStoredIndex(nextIndex)
  return nextIndex
}

function readNextStoredIndex() {
  if (typeof window === 'undefined') {
    return 0
  }

  try {
    const storedIndex = Number.parseInt(window.sessionStorage.getItem(StarterIndexStorageKey) ?? '-1', 10)
    const nextIndex = Number.isInteger(storedIndex) && storedIndex >= 0 && storedIndex < ConversationStarters.length
      ? (storedIndex + 1) % ConversationStarters.length
      : 0
    writeStoredIndex(nextIndex)
    return nextIndex
  } catch {
    return 0
  }
}

function writeStoredIndex(index: number) {
  try {
    window.sessionStorage.setItem(StarterIndexStorageKey, index.toString())
  } catch {
    // Greeting rotation is optional and must never block the conversation experience.
  }
}
