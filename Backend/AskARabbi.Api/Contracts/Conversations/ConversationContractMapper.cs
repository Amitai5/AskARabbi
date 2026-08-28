using AskARabbiLIB.Conversations;

namespace AskARabbi.Api.Contracts.Conversations;

internal static class ConversationContractMapper
{
    internal static ConversationSummaryResponse ToResponse(ConversationSummary summary) => new(summary.Id, summary.Title, summary.EnabledSourceKeys, summary.UpdatedAtUtc);

    internal static ConversationResponse ToResponse(Conversation conversation) => new(
        conversation.Id,
        conversation.Title,
        conversation.EnabledSourceKeys,
        conversation.Messages.Select(message => new ConversationMessageResponse(message.Id, message.Role, message.Content, message.CreatedAtUtc)).ToArray(),
        conversation.CreatedAtUtc,
        conversation.UpdatedAtUtc);
}
