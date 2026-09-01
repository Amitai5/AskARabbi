namespace AskARabbiLIB.AI.Tools;

/// <summary>Exposes explicitly registered, attribute-declared AI functions.</summary>
public interface IAIToolRegistry
{
    /// <summary>Gets immutable provider definitions for every registered tool.</summary>
    IReadOnlyList<AIToolDefinition> Definitions { get; }

    /// <summary>Determines whether a question contains a routing hint for any registered tool.</summary>
    /// <param name="question">Current user question.</param>
    /// <returns>True when at least one tool may answer calculation data independently of the text corpus.</returns>
    bool MayApply(string question);

    /// <summary>Executes one registered tool with validated JSON arguments and trusted request context.</summary>
    /// <param name="toolName">Provider function name.</param>
    /// <param name="arguments">Provider-supplied JSON argument object.</param>
    /// <param name="context">Private server-trusted execution context.</param>
    /// <param name="cancellationToken">Token used to cancel tool execution.</param>
    /// <returns>A bounded tool result.</returns>
    Task<AIToolExecutionResult> ExecuteAsync(string toolName, BinaryData arguments, AIToolExecutionContext context, CancellationToken cancellationToken = default);
}
