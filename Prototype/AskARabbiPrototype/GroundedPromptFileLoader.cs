using System.Text.Json;
using System.Text.Json.Serialization;
using AskARabbiLIB.Grounding;

namespace AskARabbiPrototype;

internal static class GroundedPromptFileLoader
{
    private const string PromptDirectoryName = "Prompts";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static GroundedPromptSet Load(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var promptDirectory = ResolvePromptDirectory(repositoryRoot);
        var request = DeserializeRequest(ReadRequiredFile(promptDirectory, "current-question.json"));
        var prompts = new GroundedPromptSet
        {
            SystemBehaviorPrompt = ReadRequiredFile(promptDirectory, "system-behavior.txt"),
            PriorUserContextPrompt = ReadRequiredFile(promptDirectory, "prior-user-context.txt"),
            PriorAssistantContextPrompt = ReadRequiredFile(promptDirectory, "prior-assistant-context.txt"),
            CurrentQuestionInstruction = request.Instruction,
            EvidenceStartMarker = request.EvidenceStartMarker,
            EvidenceEndMarker = request.EvidenceEndMarker,
            ValidationRepairPrompt = ReadRequiredFile(promptDirectory, "validation-repair.txt"),
            InterpretiveNotice = ReadRequiredFile(promptDirectory, "interpretive-notice.txt"),
            ResponseJsonSchema = ReadRequiredFile(promptDirectory, "grounded-answer.schema.json"),
            SupportValidationPrompt = ReadRequiredFile(promptDirectory, "grounded-support-validation.txt"),
            SupportValidationJsonSchema = ReadRequiredFile(promptDirectory, "grounded-support-validation.schema.json"),
        };
        prompts.Validate();
        return prompts;
    }

    private static GroundedRequestPromptFile DeserializeRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<GroundedRequestPromptFile>(json, SerializerOptions);
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

    private static string ResolvePromptDirectory(string repositoryRoot)
    {
        var repositoryDirectory = Path.Combine(repositoryRoot, "Prototype", PromptDirectoryName);
        if (Directory.Exists(repositoryDirectory))
        {
            return repositoryDirectory;
        }

        var outputDirectory = Path.Combine(AppContext.BaseDirectory, PromptDirectoryName);
        if (Directory.Exists(outputDirectory))
        {
            return outputDirectory;
        }
        throw new DirectoryNotFoundException($"AI prompt directory was not found at '{repositoryDirectory}' or '{outputDirectory}'.");
    }
}
