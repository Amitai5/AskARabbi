namespace AskARabbi.Api.Contracts.Conversations;

/// <summary>Provides one trusted source citation saved with an assistant response.</summary>
public sealed record ConversationSourceResponse
{
    /// <summary>Gets the answer-local citation number.</summary>
    public required int Number { get; init; }

    /// <summary>Gets the source title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the Hebrew source title when available.</summary>
    public required string HebrewTitle { get; init; }

    /// <summary>Gets the canonical textual reference.</summary>
    public required string CanonicalReference { get; init; }

    /// <summary>Gets the source edition.</summary>
    public required string Edition { get; init; }

    /// <summary>Gets the source language.</summary>
    public required string Language { get; init; }

    /// <summary>Gets the source collection.</summary>
    public required string Collection { get; init; }

    /// <summary>Gets the source license.</summary>
    public required string License { get; init; }

    /// <summary>Gets the canonical passage URL.</summary>
    public required string SourceUrl { get; init; }

    /// <summary>Gets the edition attribution URL.</summary>
    public required string AttributionUrl { get; init; }

    /// <summary>Gets exact quotations validated against the source.</summary>
    public required IReadOnlyList<string> Quotations { get; init; }

    /// <summary>Gets the bounded surrounding source context.</summary>
    public required string Context { get; init; }

    /// <summary>Gets whether the context is an explicit excerpt.</summary>
    public bool IsExcerpt { get; init; }
}
