namespace AskARabbi.Api.Contracts.Users;

/// <summary>Public registration availability, without private account statistics.</summary>
/// <param name="IsOpen">Whether new users may attempt signup.</param>
public sealed record RegistrationAvailabilityResponse(bool IsOpen);
