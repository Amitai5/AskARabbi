using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AskARabbiLIB.Grounding;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Bounds tool execution and retains trusted calculated evidence for one model request.</summary>
public sealed class AIToolExecutionSession
{
    private static readonly JsonSerializerOptions ToolJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IAIToolRegistry registry;
    private readonly AIToolExecutionContext context;
    private readonly List<EvidenceItem> evidenceItems = [];
    private readonly int initialEvidenceCount;
    private int executionCount;

    /// <summary>Creates one request-local tool session.</summary>
    /// <param name="registry">Explicit tool registry.</param>
    /// <param name="context">Trusted server-side execution context.</param>
    /// <param name="initialEvidenceCount">Count of corpus evidence IDs already allocated for this request.</param>
    /// <param name="maximumExecutionCount">Maximum number of function executions allowed in the request.</param>
    public AIToolExecutionSession(IAIToolRegistry registry, AIToolExecutionContext context, int initialEvidenceCount, int maximumExecutionCount = 4)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        if (initialEvidenceCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialEvidenceCount));
        }
        if (maximumExecutionCount is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExecutionCount), "Maximum tool executions must be between 1 and 8.");
        }
        this.initialEvidenceCount = initialEvidenceCount;
        MaximumExecutionCount = maximumExecutionCount;
    }

    /// <summary>Gets provider-visible tool definitions.</summary>
    public IReadOnlyList<AIToolDefinition> Definitions => registry.Definitions;

    /// <summary>Gets the maximum number of local executions allowed.</summary>
    public int MaximumExecutionCount { get; }

    /// <summary>Gets the number of tool calls already processed.</summary>
    public int ExecutionCount => executionCount;

    /// <summary>Gets trusted calculated evidence created during the request.</summary>
    public IReadOnlyList<EvidenceItem> EvidenceItems => evidenceItems;

    /// <summary>Executes one provider function and returns bounded JSON for its function-call output.</summary>
    /// <param name="toolName">Provider function name.</param>
    /// <param name="arguments">Provider-supplied JSON arguments.</param>
    /// <param name="cancellationToken">Token used to cancel local work.</param>
    /// <returns>JSON containing either a failure or structured data plus a trusted evidence ID.</returns>
    public async Task<BinaryData> ExecuteAsync(string toolName, BinaryData arguments, CancellationToken cancellationToken = default)
    {
        if (executionCount >= MaximumExecutionCount)
        {
            return Serialize(new { isSuccess = false, errorMessage = "The maximum number of calendar tool calls was reached." });
        }

        executionCount++;
        var result = await registry.ExecuteAsync(toolName, arguments, context, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Evidence is null)
        {
            return Serialize(new { isSuccess = false, errorMessage = result.ErrorMessage ?? "The calendar calculation failed." });
        }

        var evidenceId = $"E{initialEvidenceCount + evidenceItems.Count + 1}";
        var evidence = CreateEvidence(evidenceId, toolName, result.Evidence);
        evidenceItems.Add(evidence);
        return Serialize(new
        {
            isSuccess = true,
            data = result.Data,
            evidence = new
            {
                evidenceId,
                exactText = evidence.PresentedText,
                instruction = "Cite this evidence ID for every calendar claim and quote exact contiguous text from exactText.",
            },
        });
    }

    internal static EvidenceItem CreateEvidence(string evidenceId, string toolName, AIToolEvidence evidence)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{toolName}\n{evidence.CanonicalReference}\n{evidence.ExactText}"))).ToLowerInvariant();
        var source = new SourceSegment
        {
            SegmentId = $"calendar:{toolName}:{hash[..24]}",
            DocumentId = "askarabbi:calendar-calculations:v1",
            CanonicalReference = evidence.CanonicalReference,
            DocumentOrdinal = 0,
            Text = evidence.ExactText,
            Title = "AskARabbi Hebrew calendar",
            HebrewTitle = "חישוב לוח שנה עברי",
            Language = "English",
            LanguageCode = "en",
            Collection = "Calendar calculations",
            Categories = ["Calendar"],
            Version = "AskARabbi Hebrew calendar v1",
            License = "Calculated factual output",
            LicenseCategory = SourceLicenseCategory.PublicDomain,
            SourceUrl = "https://www.nuget.org/packages/Zmanim/1.5.0",
            FilePath = string.Empty,
            UsageNote = "A deterministic calendar result; not a religious text or halakhic ruling.",
            OriginalCharacterCount = evidence.ExactText.Length,
        };
        return new EvidenceItem(evidenceId, source, evidence.ExactText, false, evidence.ExactText.Length);
    }

    private static BinaryData Serialize(object value) => BinaryData.FromString(JsonSerializer.Serialize(value, ToolJsonOptions));
}
