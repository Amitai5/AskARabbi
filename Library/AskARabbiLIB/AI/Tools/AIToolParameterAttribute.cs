namespace AskARabbiLIB.AI.Tools;

/// <summary>Describes one model-supplied AI tool parameter.</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class AIToolParameterAttribute : Attribute
{
    /// <summary>Creates model-facing parameter metadata.</summary>
    /// <param name="description">Clear parameter purpose, format, and default behavior.</param>
    public AIToolParameterAttribute(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
    }

    /// <summary>Gets the model-facing parameter description.</summary>
    public string Description { get; }
}
