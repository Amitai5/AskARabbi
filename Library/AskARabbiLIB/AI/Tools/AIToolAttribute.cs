namespace AskARabbiLIB.AI.Tools;

/// <summary>Marks an explicitly registered provider method as callable by the AI engine.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AIToolAttribute : Attribute
{
    /// <summary>Creates metadata for one callable AI tool.</summary>
    /// <param name="name">Stable provider function name.</param>
    /// <param name="description">Instruction explaining when and how the model should use the tool.</param>
    /// <param name="questionHints">Question fragments that allow orchestration to recognize tool-only requests before corpus retrieval succeeds.</param>
    public AIToolAttribute(string name, string description, params string[] questionHints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
        QuestionHints = questionHints ?? [];
    }

    /// <summary>Gets the stable provider function name.</summary>
    public string Name { get; }

    /// <summary>Gets the model-facing function description.</summary>
    public string Description { get; }

    /// <summary>Gets question fragments used only for fail-closed routing.</summary>
    public IReadOnlyList<string> QuestionHints { get; }
}
