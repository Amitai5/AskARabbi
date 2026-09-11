using System.ComponentModel.DataAnnotations;

namespace AskARabbi.Api.Contracts.ConversationSettings;

/// <summary>Selects a location without accepting client-supplied timezone or coordinates.</summary>
public sealed record PersonalizationLocationRequest
{
    /// <summary>Gets the location kind, either zip or city.</summary>
    [Required, StringLength(4)]
    public required string Kind { get; init; }

    /// <summary>Gets a five-digit U.S. ZIP code or a reviewed GeoNames city identifier.</summary>
    [Required, StringLength(10)]
    public required string Id { get; init; }
}
