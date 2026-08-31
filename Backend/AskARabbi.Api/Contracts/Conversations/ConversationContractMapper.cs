using AskARabbiLIB.Conversations;

namespace AskARabbi.Api.Contracts.Conversations;

internal static class ConversationContractMapper
{
    internal static ConversationSummaryResponse ToResponse(ConversationSummary summary) => new(summary.Id, summary.Title, summary.EnabledSourceKeys, summary.UpdatedAtUtc);

    internal static ConversationSummaryResponse ToSummaryResponse(Conversation conversation) => new(conversation.Id, conversation.Title, conversation.EnabledSourceKeys, conversation.UpdatedAtUtc);

    internal static ConversationResponse ToResponse(Conversation conversation) => new(
        conversation.Id,
        conversation.Title,
        conversation.EnabledSourceKeys,
        conversation.Messages.Select(ToResponse).ToArray(),
        conversation.CreatedAtUtc,
        conversation.UpdatedAtUtc);

    internal static ConversationMessageResponse ToResponse(ConversationMessage message) => new(message.Id, message.Role, message.Content, message.CreatedAtUtc)
        {
            Sources = message.Sources.Select(source => new ConversationSourceResponse
            {
                Number = source.Number,
                Title = source.Title,
                HebrewTitle = source.HebrewTitle,
                CanonicalReference = source.CanonicalReference,
                Edition = source.Edition,
                Language = source.Language,
                Collection = source.Collection,
                License = source.License,
                SourceUrl = source.SourceUrl,
                AttributionUrl = source.AttributionUrl,
                Quotations = source.Quotations,
                Context = source.Context,
                IsExcerpt = source.IsExcerpt,
            }).ToArray(),
        };
}
