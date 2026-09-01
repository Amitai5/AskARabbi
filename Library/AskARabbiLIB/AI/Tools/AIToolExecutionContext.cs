using AskARabbiLIB.Profiles;

namespace AskARabbiLIB.AI.Tools;

/// <summary>Contains server-trusted private context available during one tool-enabled model request.</summary>
/// <param name="UserProfile">Optional locally loaded user profile; this object is never serialized into a tool definition.</param>
/// <param name="CurrentUtc">Current UTC instant captured for the request.</param>
public sealed record AIToolExecutionContext(UserProfile? UserProfile, DateTimeOffset CurrentUtc);
