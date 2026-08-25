namespace AskARabbiLIB.AI;

/// <summary>Builds immutable snapshots of ordered text-only AI messages.</summary>
public sealed class AIPromptBuilder
{
    private readonly List<AIMessage> messages = [];

    /// <summary>Adds a system behavior message.</summary>
    /// <param name="content">System instructions.</param>
    /// <returns>This builder for chaining.</returns>
    public AIPromptBuilder AddSystem(string content) => Add(AIMessageRole.System, content);

    /// <summary>Adds a user message.</summary>
    /// <param name="content">User content.</param>
    /// <returns>This builder for chaining.</returns>
    public AIPromptBuilder AddUser(string content) => Add(AIMessageRole.User, content);

    /// <summary>Adds an assistant message.</summary>
    /// <param name="content">Assistant content.</param>
    /// <returns>This builder for chaining.</returns>
    public AIPromptBuilder AddAssistant(string content) => Add(AIMessageRole.Assistant, content);

    /// <summary>Returns a caller-owned immutable message snapshot.</summary>
    /// <returns>The ordered messages.</returns>
    public IReadOnlyList<AIMessage> Build() => messages.ToArray();

    private AIPromptBuilder Add(AIMessageRole role, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        messages.Add(new AIMessage(role, content));
        return this;
    }
}
