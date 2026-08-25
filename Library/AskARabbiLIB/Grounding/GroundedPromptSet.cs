using System.Text.Json;

namespace AskARabbiLIB.Grounding;

/// <summary>Contains every externally supplied instruction and schema used by grounded answer generation.</summary>
public sealed record GroundedPromptSet
{
    /// <summary>Gets the placeholder used by prior-turn prompt templates.</summary>
    public const string ContextPlaceholder = "{{context}}";

    /// <summary>Gets the placeholder used by the deterministic repair prompt.</summary>
    public const string ValidationErrorPlaceholder = "{{validationError}}";

    /// <summary>Gets the structured-output schema name used by grounded answer generation.</summary>
    public string ResponseSchemaName { get; init; } = "grounded_answer_v1";

    /// <summary>Gets the structured-output schema name used by the independent claim-support audit.</summary>
    public string SupportValidationSchemaName { get; init; } = "grounded_support_validation_v1";

    /// <summary>Gets the behavior and safety contract sent as the system message.</summary>
    public required string SystemBehaviorPrompt { get; init; }

    /// <summary>Gets the template used to delimit prior user messages.</summary>
    public required string PriorUserContextPrompt { get; init; }

    /// <summary>Gets the template used to delimit prior validated assistant messages.</summary>
    public required string PriorAssistantContextPrompt { get; init; }

    /// <summary>Gets the instruction that joins the current question to its evidence packet.</summary>
    public required string CurrentQuestionInstruction { get; init; }

    /// <summary>Gets the marker placed before untrusted retrieved evidence.</summary>
    public required string EvidenceStartMarker { get; init; }

    /// <summary>Gets the marker placed after untrusted retrieved evidence.</summary>
    public required string EvidenceEndMarker { get; init; }

    /// <summary>Gets the single-repair prompt template used after deterministic validation fails.</summary>
    public required string ValidationRepairPrompt { get; init; }

    /// <summary>Gets the fixed interpretive notice materialized by trusted application code.</summary>
    public required string InterpretiveNotice { get; init; }

    /// <summary>Gets the strict JSON schema for a grounded answer draft.</summary>
    public required string ResponseJsonSchema { get; init; }

    /// <summary>Gets the independent relevance and evidentiary-support audit instructions.</summary>
    public required string SupportValidationPrompt { get; init; }

    /// <summary>Gets the strict structured-output schema for the claim-support audit.</summary>
    public required string SupportValidationJsonSchema { get; init; }

    /// <summary>Validates required content, template placeholders, evidence boundaries, schema name, and JSON syntax.</summary>
    public void Validate()
    {
        ValidateRequiredText(ResponseSchemaName, nameof(ResponseSchemaName));
        ValidateRequiredText(SupportValidationSchemaName, nameof(SupportValidationSchemaName));
        ValidateRequiredText(SystemBehaviorPrompt, nameof(SystemBehaviorPrompt));
        ValidateTemplate(PriorUserContextPrompt, ContextPlaceholder, nameof(PriorUserContextPrompt));
        ValidateTemplate(PriorAssistantContextPrompt, ContextPlaceholder, nameof(PriorAssistantContextPrompt));
        ValidateRequiredText(CurrentQuestionInstruction, nameof(CurrentQuestionInstruction));
        ValidateRequiredText(EvidenceStartMarker, nameof(EvidenceStartMarker));
        ValidateRequiredText(EvidenceEndMarker, nameof(EvidenceEndMarker));
        ValidateTemplate(ValidationRepairPrompt, ValidationErrorPlaceholder, nameof(ValidationRepairPrompt));
        ValidateRequiredText(InterpretiveNotice, nameof(InterpretiveNotice));
        ValidateRequiredText(ResponseJsonSchema, nameof(ResponseJsonSchema));
        ValidateRequiredText(SupportValidationPrompt, nameof(SupportValidationPrompt));
        ValidateRequiredText(SupportValidationJsonSchema, nameof(SupportValidationJsonSchema));

        if (InterpretiveNotice.Length > 1_000)
        {
            throw new ArgumentException("Interpretive notice must contain no more than 1,000 characters.", nameof(InterpretiveNotice));
        }

        ValidateSchemaName(ResponseSchemaName, nameof(ResponseSchemaName));
        ValidateSchemaName(SupportValidationSchemaName, nameof(SupportValidationSchemaName));
        if (string.Equals(EvidenceStartMarker, EvidenceEndMarker, StringComparison.Ordinal))
        {
            throw new ArgumentException("Evidence start and end markers must be different.", nameof(EvidenceEndMarker));
        }

        ValidateJsonSchema(ResponseJsonSchema, nameof(ResponseJsonSchema), "Response");
        ValidateJsonSchema(SupportValidationJsonSchema, nameof(SupportValidationJsonSchema), "Support validation");
    }

    internal string FormatPriorUserContext(string context) => ApplyTemplate(PriorUserContextPrompt, ContextPlaceholder, context);

    internal string FormatPriorAssistantContext(string context) => ApplyTemplate(PriorAssistantContextPrompt, ContextPlaceholder, context);

    internal string FormatValidationRepair(string validationError) => ApplyTemplate(ValidationRepairPrompt, ValidationErrorPlaceholder, validationError);

    private static string ApplyTemplate(string template, string placeholder, string value) => template.Replace(placeholder, value, StringComparison.Ordinal);

    private static void ValidateRequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Prompt content cannot be empty.", parameterName);
        }
    }

    private static void ValidateSchemaName(string value, string parameterName)
    {
        if (value.Length > 64 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("Schema name must contain at most 64 ASCII letters, digits, underscores, or hyphens.", parameterName);
        }
    }

    private static void ValidateJsonSchema(string value, string parameterName, string displayName)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"{displayName} JSON schema must contain an object at its root.", parameterName);
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"{displayName} JSON schema is invalid JSON.", parameterName, exception);
        }
    }

    private static void ValidateTemplate(string template, string placeholder, string parameterName)
    {
        ValidateRequiredText(template, parameterName);
        var first = template.IndexOf(placeholder, StringComparison.Ordinal);
        if (first < 0 || template.IndexOf(placeholder, first + placeholder.Length, StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException($"Prompt template must contain exactly one '{placeholder}' placeholder.", parameterName);
        }
    }
}
