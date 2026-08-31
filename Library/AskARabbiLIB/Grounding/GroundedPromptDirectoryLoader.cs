using System.Text.Json;
using System.Text.Json.Serialization;

namespace AskARabbiLIB.Grounding;

/// <summary>Loads the complete reviewable grounded-answer prompt set from a directory.</summary>
public static class GroundedPromptDirectoryLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Loads and validates every required prompt and structured-output schema.</summary>
    /// <param name="promptDirectory">Directory containing the tracked prompt files.</param>
    /// <returns>A validated immutable prompt set.</returns>
    public static GroundedPromptSet Load(string promptDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptDirectory);
        var fullDirectory = Path.GetFullPath(promptDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException($"AI prompt directory was not found: {fullDirectory}");
        }
        var request = DeserializeRequest(ReadRequiredFile(fullDirectory, "current-question.json"));
        var prompts = new GroundedPromptSet
        {
            SystemBehaviorPrompt = ReadRequiredFile(fullDirectory, "system-behavior.txt"),
            PriorUserContextPrompt = ReadRequiredFile(fullDirectory, "prior-user-context.txt"),
            PriorAssistantContextPrompt = ReadRequiredFile(fullDirectory, "prior-assistant-context.txt"),
            CurrentQuestionInstruction = request.Instruction,
            EvidenceStartMarker = request.EvidenceStartMarker,
            EvidenceEndMarker = request.EvidenceEndMarker,
            ValidationRepairPrompt = ReadRequiredFile(fullDirectory, "validation-repair.txt"),
            InterpretiveNotice = ReadRequiredFile(fullDirectory, "interpretive-notice.txt"),
            ResponseJsonSchema = ReadRequiredFile(fullDirectory, "grounded-answer.schema.json"),
            SupportValidationPrompt = ReadRequiredFile(fullDirectory, "grounded-support-validation.txt"),
            SupportValidationJsonSchema = ReadRequiredFile(fullDirectory, "grounded-support-validation.schema.json"),
        };
        prompts.Validate();
        return prompts;
    }

    private static RequestPrompt DeserializeRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<RequestPrompt>(json, SerializerOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.Instruction) || string.IsNullOrWhiteSpace(request.EvidenceStartMarker) || string.IsNullOrWhiteSpace(request.EvidenceEndMarker))
            {
                throw new InvalidOperationException("Prompt file 'current-question.json' must define nonempty instruction, evidenceStartMarker, and evidenceEndMarker values.");
            }
            return request;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Prompt file 'current-question.json' is not valid JSON.", exception);
        }
    }

    private static string ReadRequiredFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required AI prompt file was not found: {path}", path);
        }
        return File.ReadAllText(path).Trim();
    }

    private sealed record RequestPrompt
    {
        [JsonPropertyName("instruction")]
        public required string Instruction { get; init; }

        [JsonPropertyName("evidenceStartMarker")]
        public required string EvidenceStartMarker { get; init; }

        [JsonPropertyName("evidenceEndMarker")]
        public required string EvidenceEndMarker { get; init; }
    }
}
