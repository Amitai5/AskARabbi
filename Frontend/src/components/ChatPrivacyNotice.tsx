export function ChatPrivacyNotice() {
  return (
    <div className="space-y-3 text-sm leading-6 text-muted sm:text-base">
      <p>AskRabbi saves your questions and answers in your account so you can return to your conversations. You can delete saved chats or your account in Settings → Your data.</p>
      <p>We use Azure OpenAI to generate answers and disable its stored-response feature for chats, source lookups, and background generation. Microsoft may still retain prompts and answers for abuse monitoring, including review by authorized Microsoft staff.</p>
      <p>Deleting chats from AskRabbi does not delete Microsoft’s abuse-monitoring records.</p>
      <a href="https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy" target="_blank" rel="noopener noreferrer" className="inline-flex min-h-11 items-center rounded-sm font-semibold text-pomegranate underline decoration-pomegranate/40 underline-offset-4 hover:decoration-pomegranate focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-pomegranate">Microsoft’s Azure AI privacy details<span className="sr-only"> (opens in a new tab)</span></a>
    </div>
  )
}
