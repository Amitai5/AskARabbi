namespace AskARabbiLIB.Conversations;

/// <summary>Identifies who authored a persisted conversation message.</summary>
public enum ConversationMessageRole
{
    /// <summary>The authenticated user authored the message.</summary>
    User,

    /// <summary>AskRabbi authored the validated grounded response.</summary>
    Assistant,
}
