export interface ConversationSummary {
  id: string
  title: string
}

export const InitialConversations: ConversationSummary[] = [
  { id: 'chicken-dairy', title: 'Chicken and dairy' },
  { id: 'shabbat-automation', title: 'Shabbat and automation' },
  { id: 'customs-differ', title: 'Why customs differ' },
  { id: 'amidah', title: 'Understanding the Amidah' },
  { id: 'mezuzah', title: 'A question about mezuzah' },
]
