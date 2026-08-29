namespace AskARabbiLIB.Retrieval;

/// <summary>Defines the immutable production vector store accepted by retrieval.</summary>
public sealed record AzureOpenAIVectorStoreRetrieverOptions
{
    /// <summary>Gets the Azure OpenAI vector-store identifier.</summary>
    public required string VectorStoreId { get; init; }

    /// <summary>Gets the lowercase SHA-256 fingerprint expected in store and file metadata.</summary>
    public required string ExpectedCorpusFingerprint { get; init; }

    /// <summary>Gets the provider score threshold applied before local fail-closed filtering.</summary>
    public double ScoreThreshold { get; init; }

    /// <summary>Gets whether Azure may rewrite natural-language retrieval queries.</summary>
    public bool RewriteQuery { get; init; } = true;

    /// <summary>Validates the store identifier, fingerprint, and score threshold.</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(VectorStoreId);
        if (!IsLowercaseSha256(ExpectedCorpusFingerprint))
        {
            throw new ArgumentException("Expected corpus fingerprint must be a lowercase SHA-256 value.", nameof(ExpectedCorpusFingerprint));
        }
        if (ScoreThreshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ScoreThreshold), "Vector-store score threshold must be between zero and one.");
        }
    }

    private static bool IsLowercaseSha256(string value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
