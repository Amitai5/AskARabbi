using System.Text.Json;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Contains the versioned structured prompts used by weekly generation and independent review.</summary>
public sealed record WeeklyDvarTorahPromptSet
{
    /// <summary>Gets the stable structured schema name for research selection.</summary>
    public string ResearchSchemaName { get; init; } = "weekly_dvar_torah_research_v1";

    /// <summary>Gets the stable structured schema name for drafting.</summary>
    public string DraftSchemaName { get; init; } = "weekly_dvar_torah_draft_v1";

    /// <summary>Gets the stable structured schema name for independent safety and quality review.</summary>
    public string ReviewSchemaName { get; init; } = "weekly_dvar_torah_review_v1";

    /// <summary>Gets the current-events selection instructions.</summary>
    public required string ResearchSystemPrompt { get; init; }

    /// <summary>Gets the strict research response schema.</summary>
    public required string ResearchJsonSchema { get; init; }

    /// <summary>Gets the Torah-centered drafting instructions.</summary>
    public required string DraftSystemPrompt { get; init; }

    /// <summary>Gets the strict draft response schema.</summary>
    public required string DraftJsonSchema { get; init; }

    /// <summary>Gets the independent grounding, neutrality, violence, hate, and inclusion review instructions.</summary>
    public required string ReviewSystemPrompt { get; init; }

    /// <summary>Gets the strict review response schema.</summary>
    public required string ReviewJsonSchema { get; init; }

    /// <summary>Gets the deterministic repair prompt containing exactly one validation-error placeholder.</summary>
    public required string RepairPrompt { get; init; }

    /// <summary>Validates every prompt, schema name, JSON schema, and repair placeholder.</summary>
    public void Validate()
    {
        ValidateSchemaName(ResearchSchemaName, nameof(ResearchSchemaName));
        ValidateSchemaName(DraftSchemaName, nameof(DraftSchemaName));
        ValidateSchemaName(ReviewSchemaName, nameof(ReviewSchemaName));
        ValidateRequired(ResearchSystemPrompt, nameof(ResearchSystemPrompt));
        ValidateRequired(DraftSystemPrompt, nameof(DraftSystemPrompt));
        ValidateRequired(ReviewSystemPrompt, nameof(ReviewSystemPrompt));
        ValidateJsonSchema(ResearchJsonSchema, nameof(ResearchJsonSchema));
        ValidateJsonSchema(DraftJsonSchema, nameof(DraftJsonSchema));
        ValidateJsonSchema(ReviewJsonSchema, nameof(ReviewJsonSchema));
        ValidateRequired(RepairPrompt, nameof(RepairPrompt));
        const string placeholder = "{{validationErrors}}";
        var first = RepairPrompt.IndexOf(placeholder, StringComparison.Ordinal);
        if (first < 0 || RepairPrompt.IndexOf(placeholder, first + placeholder.Length, StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException($"Repair prompt must contain exactly one '{placeholder}' placeholder.", nameof(RepairPrompt));
        }
    }

    internal string FormatRepair(string errors) => RepairPrompt.Replace("{{validationErrors}}", errors, StringComparison.Ordinal);

    private static void ValidateSchemaName(string value, string parameterName)
    {
        ValidateRequired(value, parameterName);
        if (value.Length > 64 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("Schema names may contain at most sixty-four ASCII letters, digits, underscores, or hyphens.", parameterName);
        }
    }

    private static void ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Prompt content cannot be blank.", parameterName);
        }
    }

    private static void ValidateJsonSchema(string value, string parameterName)
    {
        ValidateRequired(value, parameterName);
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("A structured prompt schema must contain an object at its root.", parameterName);
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("A structured prompt schema is invalid JSON.", parameterName, exception);
        }
    }
}
