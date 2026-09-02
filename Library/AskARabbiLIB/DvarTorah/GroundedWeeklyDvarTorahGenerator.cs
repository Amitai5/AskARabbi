using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AskARabbiLIB.AI;
using AskARabbiLIB.CurrentEvents;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;

namespace AskARabbiLIB.DvarTorah;

/// <summary>Researches, drafts, audits, and materializes a Torah-centered weekly Dvar Torah.</summary>
public sealed class GroundedWeeklyDvarTorahGenerator : IWeeklyDvarTorahGenerator
{
    private const string HighRiskNewsPattern = @"\b(?:assault|assaulted|attack|attacked|attacks|bomb|bombed|bombing|dead|death|deaths|genocide|gunfire|hostage|hostages|kill|killed|killing|massacre|military|missile|murder|murdered|rape|raped|shooting|shootings|slur|terror|terrorism|violent|violence|war|weapon|weapons|wounded)\b";
    private const string HighRiskTorahPattern = @"\b(?:assault|assaulted|attack|attacked|attacks|bomb|bombed|bombing|burn|burned|burning|burnt|curse|cursed|curses|destroy|destroyed|destruction|fury|genocide|gunfire|harm|hostage|hostages|kill|killed|killing|massacre|military|missile|murder|murdered|plague|plagues|punish|punished|punishment|rape|raped|shooting|shootings|slaughter|slay|slur|smite|smitten|smote|sulfur|terror|terrorism|vengeance|vengeful|violent|violence|war|warfare|weapon|weapons|wounded|wrath)\b";
    private const string SensitivePersonalDataPattern = @"(?:\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b|(?<!\d)(?:\+?1[\s.-]?)?(?:\(\d{3}\)|\d{3})[\s.-]\d{3}[\s.-]\d{4}(?!\d)|\b(?:\d{1,3}\.){3}\d{1,3}\b)";
    private static readonly JsonSerializerOptions PromptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly ICurrentEventsSource currentEvents;
    private readonly ISourceRetriever torahRetriever;
    private readonly IAIEngine generationEngine;
    private readonly IAIEngine reviewEngine;
    private readonly WeeklyDvarTorahPromptSet prompts;
    private readonly WeeklyDvarTorahContentOptions options;
    private readonly TimeProvider timeProvider;
    private readonly BinaryData researchSchema;
    private readonly BinaryData draftSchema;
    private readonly BinaryData reviewSchema;

    /// <summary>Initializes a fail-closed weekly content generator.</summary>
    /// <param name="currentEvents">No-subscription current-events source.</param>
    /// <param name="torahRetriever">Approved Torah corpus retriever.</param>
    /// <param name="generationEngine">Structured research and drafting engine.</param>
    /// <param name="reviewEngine">Independent structured grounding and safety review engine.</param>
    /// <param name="prompts">Version-controlled prompt and schema contract.</param>
    /// <param name="options">Research and validation bounds.</param>
    /// <param name="timeProvider">Clock used for research-window provenance.</param>
    public GroundedWeeklyDvarTorahGenerator(ICurrentEventsSource currentEvents, ISourceRetriever torahRetriever, IAIEngine generationEngine, IAIEngine reviewEngine, WeeklyDvarTorahPromptSet prompts, WeeklyDvarTorahContentOptions? options = null, TimeProvider? timeProvider = null)
    {
        this.currentEvents = currentEvents ?? throw new ArgumentNullException(nameof(currentEvents));
        this.torahRetriever = torahRetriever ?? throw new ArgumentNullException(nameof(torahRetriever));
        this.generationEngine = generationEngine ?? throw new ArgumentNullException(nameof(generationEngine));
        this.reviewEngine = reviewEngine ?? throw new ArgumentNullException(nameof(reviewEngine));
        this.prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        prompts.Validate();
        this.options = options ?? new WeeklyDvarTorahContentOptions();
        this.options.Validate();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        researchSchema = BinaryData.FromString(prompts.ResearchJsonSchema);
        draftSchema = BinaryData.FromString(prompts.DraftJsonSchema);
        reviewSchema = BinaryData.FromString(prompts.ReviewJsonSchema);
    }

    /// <inheritdoc/>
    public async Task<WeeklyDvarTorahDraft> GenerateAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(week);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.OverallTimeout);
        try
        {
            return await GenerateCoreAsync(week, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Weekly Dvar Torah research exceeded the {options.OverallTimeout.TotalMinutes:N0}-minute limit.");
        }
    }

    private async Task<WeeklyDvarTorahDraft> GenerateCoreAsync(WeeklyDvarTorahWeek week, CancellationToken cancellationToken)
    {
        var newsWindowEndedAtUtc = timeProvider.GetUtcNow();
        var newsWindowStartedAtUtc = newsWindowEndedAtUtc.AddDays(-options.ResearchWindowDays);
        var recentItems = await currentEvents.GetRecentAsync(newsWindowStartedAtUtc, newsWindowEndedAtUtc, cancellationToken).ConfigureAwait(false);
        var newsCandidates = SelectNewsCandidates(recentItems);
        if (newsCandidates.Select(candidate => candidate.Item.Publisher).Distinct(StringComparer.OrdinalIgnoreCase).Count() < options.MinimumNewsPublishers)
        {
            throw new WeeklyDvarTorahGenerationException("CurrentEventsInsufficientPublishers", $"Free current-events research did not return at least {options.MinimumNewsPublishers} independent publishers.");
        }

        var research = await ResearchAsync(week, newsWindowStartedAtUtc, newsWindowEndedAtUtc, newsCandidates, cancellationToken).ConfigureAwait(false);
        var researchErrors = ValidateResearch(research, newsCandidates);
        if (researchErrors.Count > 0)
        {
            research = await ResearchAsync(week, newsWindowStartedAtUtc, newsWindowEndedAtUtc, newsCandidates, cancellationToken, research, string.Join(" ", researchErrors)).ConfigureAwait(false);
            researchErrors = ValidateResearch(research, newsCandidates);
        }
        if (researchErrors.Count > 0)
        {
            throw new WeeklyDvarTorahGenerationException("ResearchSelectionInvalid", $"Weekly Dvar Torah research selection failed its repair attempt: {string.Join(" ", researchErrors)}");
        }

        var selectedNews = research.SelectedNewsEvidenceIds.Select(id => newsCandidates.Single(candidate => candidate.EvidenceId == id)).ToArray();
        var evidence = new List<WeeklyDvarTorahEvidence>();
        evidence.AddRange(await RetrieveTorahEvidenceAsync(week, research, cancellationToken).ConfigureAwait(false));
        evidence.AddRange(selectedNews.Select(candidate => new WeeklyDvarTorahEvidence(
            candidate.EvidenceId,
            WeeklyDvarTorahSourceKind.News,
            candidate.Item.Title,
            candidate.Item.Publisher,
            candidate.Item.SourceUrl,
            candidate.Item.Summary,
            candidate.Item.RetrievedAtUtc,
            null,
            candidate.Item.PublishedAtUtc,
            "Public RSS/Atom metadata; linked article remains with its publisher.")));
        var materializedSources = MaterializeSources(evidence);

        var draftMessages = BuildDraftMessages(week, research, evidence);
        WeeklyDvarTorahArticleDraft? previousDraft = null;
        string? validationError = null;
        string? diagnosticCategory = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            IReadOnlyList<AIMessage> messages = attempt switch
            {
                0 => draftMessages,
                _ when previousDraft is null => draftMessages.Concat([new AIMessage(AIMessageRole.User, prompts.FormatRepair(validationError ?? "The prior completion was blocked."))]).ToArray(),
                _ => draftMessages.Concat(
                [
                    new AIMessage(AIMessageRole.Assistant, JsonSerializer.Serialize(previousDraft, PromptJsonOptions)),
                    new AIMessage(AIMessageRole.User, prompts.FormatRepair(validationError ?? "The prior draft did not pass validation.")),
                ]).ToArray(),
            };
            var draftResult = await generationEngine.GenerateStructuredAsync<WeeklyDvarTorahArticleDraft>(messages, prompts.DraftSchemaName, draftSchema, cancellationToken).ConfigureAwait(false);
            if (!draftResult.IsSuccess || draftResult.Value is not { } draft)
            {
                if (attempt == 0 && IsCompletionContentFilterFailure(draftResult))
                {
                    validationError = "The prior completion was blocked by the provider. Produce a fresh, peaceful article using original paraphrase in every field and no direct quotations or substantial contiguous source wording. Do not output URLs, contact details, email addresses, telephone numbers, IP addresses, timestamps, chapter-and-verse numbers, or any digits.";
                    continue;
                }
                throw new WeeklyDvarTorahGenerationException(CreateProviderFailureCode("DraftProviderFailed", draftResult), $"The weekly Dvar Torah drafting model failed: {draftResult.ErrorMessage ?? draftResult.Status.ToString()}.");
            }

            var generatedDraft = draft;
            var completedDraft = AddMissingBodyEvidenceMarkers(generatedDraft, evidence, options.MaximumBodyCharacters);
            completedDraft = WeeklyDvarTorahQuotationRenderer.AddTrustedQuotations(completedDraft, evidence, options.MaximumBodyCharacters);
            previousDraft = generatedDraft;
            var validation = WeeklyDvarTorahCandidateValidator.Validate(completedDraft, evidence, options);
            if (!validation.IsValid)
            {
                validationError = string.Join(" ", validation.Errors);
                diagnosticCategory = ClassifyCandidateValidationErrors(validation.Errors);
                continue;
            }

            var review = await ReviewAsync(week, research, completedDraft, evidence, validation, cancellationToken).ConfigureAwait(false);
            var reviewErrors = WeeklyDvarTorahReviewValidator.Validate(review);
            if (reviewErrors.Count > 0)
            {
                validationError = string.Join(" ", reviewErrors);
                diagnosticCategory = "IndependentReview";
                continue;
            }

            var sources = validation.UsedEvidenceIds.Select(id => materializedSources[id]).ToArray();
            var tags = CreateTags(completedDraft.Tags, research.SuggestedTags, week);
            try
            {
                var metadata = new WeeklyDvarTorahContentMetadata(
                    completedDraft.CentralTeaching,
                    tags,
                    sources,
                    validation.TorahGroundingPercent,
                    prompts.ReviewSchemaName,
                    draftResult.Diagnostics.Model,
                    newsWindowStartedAtUtc,
                    newsWindowEndedAtUtc);
                return new WeeklyDvarTorahDraft(completedDraft.Title, completedDraft.Body, options.GeneratorVersion, metadata);
            }
            catch (ArgumentException)
            {
                throw new WeeklyDvarTorahGenerationException("PublicationMetadataInvalid", "The validated weekly Dvar Torah could not be materialized into the publication contract.");
            }
        }

        throw new WeeklyDvarTorahGenerationException("CandidateValidationFailed", $"Weekly Dvar Torah generation failed its repair attempt: {validationError ?? "unknown validation failure"}", diagnosticCategory);
    }

    private async Task<WeeklyDvarTorahResearchDraft> ResearchAsync(WeeklyDvarTorahWeek week, DateTimeOffset windowStart, DateTimeOffset windowEnd, IReadOnlyList<NewsCandidate> candidates, CancellationToken cancellationToken, WeeklyDvarTorahResearchDraft? previousResearch = null, string? validationError = null)
    {
        var input = new
        {
            week = new
            {
                week.WeekKey,
                week.ShabbatDate,
                week.HebrewDate,
                week.Parashah,
                week.Holiday,
                week.InIsrael,
            },
            researchWindow = new { startedAtUtc = windowStart, endedAtUtc = windowEnd },
            requirements = new
            {
                focus = "United States news, technology, science, health, economic life, or a major global event with material U.S. impact",
                minimumIndependentPublishers = options.MinimumNewsPublishers,
                maximumNewsSources = options.MaximumNewsSources,
                torahGroundingPercent = options.MinimumTorahGroundingPercent,
            },
            newsEvidence = candidates.Select(candidate => new
            {
                evidenceId = candidate.EvidenceId,
                candidate.Item.Publisher,
                candidate.Item.Category,
                candidate.Item.Title,
                candidate.Item.Summary,
                candidate.Item.SourceUrl,
                candidate.Item.PublishedAtUtc,
            }),
        };
        var builder = new AIPromptBuilder()
            .AddSystem(prompts.ResearchSystemPrompt)
            .AddUser($"<UNTRUSTED_CURRENT_EVENTS_JSON>\n{JsonSerializer.Serialize(input, PromptJsonOptions)}\n</UNTRUSTED_CURRENT_EVENTS_JSON>");
        if (previousResearch is not null)
        {
            builder
                .AddAssistant(JsonSerializer.Serialize(previousResearch, PromptJsonOptions))
                .AddUser(prompts.FormatRepair(validationError ?? "The prior research selection did not pass validation."));
        }
        var messages = builder.Build();
        var result = await generationEngine.GenerateStructuredAsync<WeeklyDvarTorahResearchDraft>(messages, prompts.ResearchSchemaName, researchSchema, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is not { } research)
        {
            throw new WeeklyDvarTorahGenerationException(CreateProviderFailureCode("ResearchProviderFailed", result), $"The weekly Dvar Torah research model failed: {result.ErrorMessage ?? result.Status.ToString()}.");
        }

        return research;
    }

    private async Task<IReadOnlyList<WeeklyDvarTorahEvidence>> RetrieveTorahEvidenceAsync(WeeklyDvarTorahWeek week, WeeklyDvarTorahResearchDraft research, CancellationToken cancellationToken)
    {
        var reading = week.Parashah ?? week.Holiday ?? "the weekly Torah reading";
        if (!WeeklyTorahReadingRangeCatalog.IsSupported(week))
        {
            throw new WeeklyDvarTorahGenerationException("UnsupportedTorahReading", $"The canonical Torah range for '{reading}' on {week.ShabbatDate:yyyy-MM-dd} is not configured; generation stopped without publishing.");
        }

        var queries = research.TorahSearchQueries
            .Append($"{reading}: {research.Theme}. {research.MoralQuestion}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        var hits = new Dictionary<string, SourceRetrievalHit>(StringComparer.Ordinal);
        foreach (var query in queries)
        {
            var results = await torahRetriever.SearchAsync(new SourceRetrievalQuery
            {
                QueryText = query,
                Languages = ["English"],
                Collections = ["Torah"],
                CandidateLimit = 30,
            }, cancellationToken).ConfigureAwait(false);
            foreach (var hit in results)
            {
                if (!WeeklyTorahReadingRangeCatalog.Contains(week, hit.Segment.CanonicalReference))
                {
                    continue;
                }
                if (!HasUnrestrictedQuotationLicense(hit))
                {
                    continue;
                }
                if (!CanPersistTorahEvidence(hit))
                {
                    continue;
                }
                if (ContainsHighRiskTorahContent(hit))
                {
                    continue;
                }
                if (!hits.TryGetValue(hit.Segment.SegmentId, out var existing) || hit.Score > existing.Score)
                {
                    hits[hit.Segment.SegmentId] = hit;
                }
            }
        }

        var selected = hits.Values
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Segment.CanonicalReference, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaximumTorahEvidenceItems)
            .ToArray();
        if (selected.Length < options.MinimumTorahEvidenceItems)
        {
            throw new WeeklyDvarTorahGenerationException("TorahEvidenceInsufficient", $"Approved corpus retrieval found only {selected.Length} passages for '{reading}'; at least {options.MinimumTorahEvidenceItems} are required.");
        }

        var retrievedAtUtc = timeProvider.GetUtcNow();
        return selected.Select((hit, index) => new WeeklyDvarTorahEvidence(
            CreateEvidenceId('T', index),
            WeeklyDvarTorahSourceKind.Torah,
            hit.Segment.Title,
            hit.Segment.Version,
            CreateHttpsSourceUrl(hit.Segment.SourceUrl),
            Bound(hit.Segment.Text, 2_000),
            retrievedAtUtc,
            hit.Segment.CanonicalReference,
            null,
            hit.Segment.License)).ToArray();
    }

    private IReadOnlyList<AIMessage> BuildDraftMessages(WeeklyDvarTorahWeek week, WeeklyDvarTorahResearchDraft research, IReadOnlyList<WeeklyDvarTorahEvidence> evidence)
    {
        var input = new
        {
            week = new { week.Parashah, week.Holiday },
            research = new { research.Theme, research.MoralQuestion },
            requirements = new
            {
                minimumTorahGroundingPercent = options.MinimumTorahGroundingPercent,
                minimumTorahSources = options.MinimumTorahEvidenceItems,
                minimumNewsPublishers = options.MinimumNewsPublishers,
                bodyCharacters = new { minimum = options.MinimumBodyCharacters, maximum = WeeklyDvarTorahQuotationRenderer.GetMaximumGeneratedBodyCharacters(options) },
                compositionTargets = new
                {
                    torahTeachingStatements = 8,
                    currentEventFactStatements = 1,
                    connectionStatements = 1,
                    distinctTorahEvidenceIds = options.MinimumTorahEvidenceItems,
                    distinctNewsEvidenceIds = options.MinimumNewsPublishers,
                    featuredTorahQuotationCount = WeeklyDvarTorahQuotationRenderer.RequiredQuotationCount,
                },
            },
            evidence = evidence.Select(item => new
            {
                evidenceId = item.EvidenceId,
                kind = item.Kind.ToString(),
                item.Title,
                item.Publisher,
                text = item.PresentedText,
            }),
        };
        return new AIPromptBuilder()
            .AddSystem(prompts.DraftSystemPrompt)
            .AddUser($"<UNTRUSTED_EVIDENCE_JSON>\n{JsonSerializer.Serialize(input, PromptJsonOptions)}\n</UNTRUSTED_EVIDENCE_JSON>")
            .Build();
    }

    private static WeeklyDvarTorahArticleDraft AddMissingBodyEvidenceMarkers(WeeklyDvarTorahArticleDraft draft, IReadOnlyList<WeeklyDvarTorahEvidence> evidence, int maximumBodyCharacters)
    {
        var knownIds = evidence.Select(item => item.EvidenceId).ToHashSet(StringComparer.Ordinal);
        var citedIds = (draft.TorahTeachings ?? [])
            .Concat(draft.CurrentEventFacts ?? [])
            .Concat(draft.Connections ?? [])
            .Where(statement => statement is not null)
            .SelectMany(statement => statement.EvidenceIds ?? [])
            .Where(knownIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !draft.Body.Contains($"[{id}]", StringComparison.Ordinal))
            .ToArray();
        if (citedIds.Length == 0)
        {
            return draft;
        }

        var suffix = $"\n\nSources: {string.Join(' ', citedIds.Select(id => $"[{id}]"))}";
        var body = draft.Body.TrimEnd();
        return body.Length + suffix.Length <= maximumBodyCharacters ? draft with { Body = body + suffix } : draft;
    }

    private static string ClassifyCandidateValidationErrors(IReadOnlyList<string> errors)
    {
        if (errors.Any(error => error.Contains("Torah quotation", StringComparison.Ordinal)))
        {
            return "TorahQuotations";
        }
        if (errors.Any(error => error.Contains("Torah grounding", StringComparison.Ordinal)))
        {
            return "TorahGrounding";
        }
        if (errors.Any(error => error.Contains("inline marker", StringComparison.Ordinal) || error.Contains("evidence marker", StringComparison.Ordinal)))
        {
            return "BodyCitations";
        }
        if (errors.Any(error => error.Contains("evidence ID", StringComparison.Ordinal)))
        {
            return "EvidenceIds";
        }
        if (errors.Any(error => error.Contains("news publisher", StringComparison.Ordinal) || error.Contains("current event", StringComparison.Ordinal)))
        {
            return "CurrentEvents";
        }
        if (errors.Any(error => error.Contains("Torah teaching", StringComparison.Ordinal) || error.Contains("Torah passage", StringComparison.Ordinal)))
        {
            return "TorahTeachings";
        }
        if (errors.Any(error => error.Contains("tag", StringComparison.OrdinalIgnoreCase)))
        {
            return "Tags";
        }
        if (errors.Any(error => error.Contains("practical action", StringComparison.Ordinal)))
        {
            return "PracticalActions";
        }
        if (errors.Any(error => error.Contains("contact details", StringComparison.Ordinal)))
        {
            return "SensitiveData";
        }

        return "ContentShape";
    }

    private async Task<WeeklyDvarTorahReviewDraft> ReviewAsync(WeeklyDvarTorahWeek week, WeeklyDvarTorahResearchDraft research, WeeklyDvarTorahArticleDraft draft, IReadOnlyList<WeeklyDvarTorahEvidence> evidence, WeeklyDvarTorahCandidateValidation validation, CancellationToken cancellationToken)
    {
        var used = validation.UsedEvidenceIds.ToHashSet(StringComparer.Ordinal);
        var input = new
        {
            week = new { week.WeekKey, week.ShabbatDate, week.HebrewDate, week.Parashah, week.Holiday },
            research = new { research.Theme, research.MoralQuestion },
            deterministicTorahGroundingPercent = validation.TorahGroundingPercent,
            article = draft,
            evidence = evidence.Where(item => used.Contains(item.EvidenceId)).Select(item => new
            {
                evidenceId = item.EvidenceId,
                kind = item.Kind.ToString(),
                item.Title,
                item.Publisher,
                item.CanonicalReference,
                text = item.PresentedText,
            }),
        };
        var messages = new AIPromptBuilder()
            .AddSystem(prompts.ReviewSystemPrompt)
            .AddUser($"<UNTRUSTED_DRAFT_AND_EVIDENCE_JSON>\n{JsonSerializer.Serialize(input, PromptJsonOptions)}\n</UNTRUSTED_DRAFT_AND_EVIDENCE_JSON>")
            .Build();
        var result = await reviewEngine.GenerateStructuredAsync<WeeklyDvarTorahReviewDraft>(messages, prompts.ReviewSchemaName, reviewSchema, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is not { } review)
        {
            throw new WeeklyDvarTorahGenerationException(CreateProviderFailureCode("IndependentReviewFailed", result), $"The independent weekly Dvar Torah review failed: {result.ErrorMessage ?? result.Status.ToString()}.");
        }

        return review;
    }

    private IReadOnlyList<NewsCandidate> SelectNewsCandidates(IReadOnlyList<CurrentEventItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var groups = items
            .Where(item => item is not null)
            .Where(item => !ContainsHighRiskNewsContent(item))
            .Where(item => !ContainsSensitivePersonalData(item))
            .Where(CanPersistNewsEvidence)
            .GroupBy(item => item.Publisher, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Queue<CurrentEventItem>(group.OrderByDescending(item => item.PublishedAtUtc)))
            .ToArray();
        var selected = new List<CurrentEventItem>(options.MaximumNewsCandidates);
        while (selected.Count < options.MaximumNewsCandidates && groups.Any(group => group.Count > 0))
        {
            foreach (var group in groups)
            {
                if (group.Count > 0 && selected.Count < options.MaximumNewsCandidates)
                {
                    selected.Add(group.Dequeue());
                }
            }
        }

        return selected.Select((item, index) => new NewsCandidate(CreateEvidenceId('N', index), item)).ToArray();
    }

    private static IReadOnlyDictionary<string, WeeklyDvarTorahSource> MaterializeSources(IReadOnlyList<WeeklyDvarTorahEvidence> evidence)
    {
        try
        {
            return evidence.ToDictionary(item => item.EvidenceId, item => item.ToSource(), StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            throw new WeeklyDvarTorahGenerationException("EvidenceMetadataInvalid", "Selected weekly evidence did not satisfy the persisted source contract.");
        }
    }

    private static bool CanPersistNewsEvidence(CurrentEventItem item) => CanPersistEvidence(new WeeklyDvarTorahEvidence(
        "N",
        WeeklyDvarTorahSourceKind.News,
        item.Title,
        item.Publisher,
        item.SourceUrl,
        item.Summary,
        item.RetrievedAtUtc,
        null,
        item.PublishedAtUtc,
        "Public RSS/Atom metadata; linked article remains with its publisher."));

    private static bool CanPersistTorahEvidence(SourceRetrievalHit hit) => CanPersistEvidence(new WeeklyDvarTorahEvidence(
        "T",
        WeeklyDvarTorahSourceKind.Torah,
        hit.Segment.Title,
        hit.Segment.Version,
        CreateHttpsSourceUrl(hit.Segment.SourceUrl),
        Bound(hit.Segment.Text, 2_000),
        DateTimeOffset.UnixEpoch,
        hit.Segment.CanonicalReference,
        null,
        hit.Segment.License));

    private static bool CanPersistEvidence(WeeklyDvarTorahEvidence evidence)
    {
        try
        {
            _ = evidence.ToSource();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string CreateHttpsSourceUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri;
    }

    private IReadOnlyList<string> ValidateResearch(WeeklyDvarTorahResearchDraft research, IReadOnlyList<NewsCandidate> candidates)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(research.Theme) || research.Theme.Length > 300)
        {
            errors.Add("Research theme must contain at most three hundred characters.");
        }
        if (string.IsNullOrWhiteSpace(research.MoralQuestion) || research.MoralQuestion.Length > 500)
        {
            errors.Add("Research moral question must contain at most five hundred characters.");
        }

        var selectedIds = research.SelectedNewsEvidenceIds ?? [];
        var candidateById = candidates.ToDictionary(candidate => candidate.EvidenceId, StringComparer.Ordinal);
        if (selectedIds.Count < options.MinimumNewsPublishers || selectedIds.Count > options.MaximumNewsSources || selectedIds.Any(id => !candidateById.ContainsKey(id)) || selectedIds.Distinct(StringComparer.Ordinal).Count() != selectedIds.Count)
        {
            errors.Add($"Research must select between {options.MinimumNewsPublishers} and {options.MaximumNewsSources} unique known news evidence IDs.");
        }
        else if (selectedIds.Select(id => candidateById[id].Item.Publisher).Distinct(StringComparer.OrdinalIgnoreCase).Count() < options.MinimumNewsPublishers)
        {
            errors.Add($"Research must select at least {options.MinimumNewsPublishers} independent publishers.");
        }

        var queries = research.TorahSearchQueries ?? [];
        if (queries.Count is < 2 or > 4 || queries.Any(query => string.IsNullOrWhiteSpace(query) || query.Length > 300) || queries.Distinct(StringComparer.OrdinalIgnoreCase).Count() != queries.Count)
        {
            errors.Add("Research must provide two to four unique bounded Torah search queries.");
        }
        var tags = research.SuggestedTags ?? [];
        if (tags.Count is < 3 or > 12 || tags.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 60))
        {
            errors.Add("Research must suggest between three and twelve bounded tags.");
        }

        return errors;
    }

    private static IReadOnlyList<string> CreateTags(IReadOnlyList<string> draftTags, IReadOnlyList<string> researchTags, WeeklyDvarTorahWeek week)
    {
        var values = draftTags.Concat(researchTags)
            .Append(week.Parashah ?? week.Holiday ?? "weekly Torah")
            .Append("weekly dvar Torah")
            .Select(tag => string.Join(' ', tag.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant())
            .Where(tag => tag.Length is > 0 and <= 60)
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToArray();
        return values;
    }

    private static string Bound(string value, int maximumCharacters) => value.Length <= maximumCharacters ? value : value[..maximumCharacters].TrimEnd();

    private static string CreateEvidenceId(char prefix, int zeroBasedIndex)
    {
        if (zeroBasedIndex is < 0 or >= 676)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));
        }

        return zeroBasedIndex < 26
            ? $"{prefix}{(char)('A' + zeroBasedIndex)}"
            : $"{prefix}{(char)('A' + zeroBasedIndex / 26 - 1)}{(char)('A' + zeroBasedIndex % 26)}";
    }

    private static string CreateProviderFailureCode<T>(string stage, AIEngineResult<T> result) => $"{stage}.{result.Status}.{result.Diagnostics.CompletionReason ?? "unknown"}";

    private static bool IsCompletionContentFilterFailure<T>(AIEngineResult<T> result) => result.Status == AIEngineStatus.InvalidResponse && result.Diagnostics.CompletionReason?.StartsWith("content_filter", StringComparison.Ordinal) == true;

    private static bool ContainsHighRiskNewsContent(CurrentEventItem item) => Regex.IsMatch($"{item.Title}\n{item.Summary}", HighRiskNewsPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static bool ContainsSensitivePersonalData(CurrentEventItem item) => Regex.IsMatch($"{item.Title}\n{item.Summary}", SensitivePersonalDataPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static bool HasUnrestrictedQuotationLicense(SourceRetrievalHit hit) => hit.Segment.LicenseCategory is SourceLicenseCategory.PublicDomain or SourceLicenseCategory.Cc0;

    private static bool ContainsHighRiskTorahContent(SourceRetrievalHit hit) => Regex.IsMatch(hit.Segment.Text, HighRiskTorahPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private sealed record NewsCandidate(string EvidenceId, CurrentEventItem Item);
}
