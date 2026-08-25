namespace AskARabbiLIB.AI;

/// <summary>Represents one text-only message sent to an AI engine.</summary>
/// <param name="Role">Message role used by the provider.</param>
/// <param name="Content">Nonempty message content.</param>
public sealed record AIMessage(AIMessageRole Role, string Content);
