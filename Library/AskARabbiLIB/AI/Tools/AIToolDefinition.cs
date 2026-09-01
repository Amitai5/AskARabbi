namespace AskARabbiLIB.AI.Tools;

/// <summary>Defines one bounded function available to the AI provider.</summary>
/// <param name="Name">Stable function name.</param>
/// <param name="Description">Model-facing function description.</param>
/// <param name="ParametersJsonSchema">JSON schema for model-supplied arguments.</param>
public sealed record AIToolDefinition(string Name, string Description, BinaryData ParametersJsonSchema);
