namespace AskARabbi.Api.Development;

/// <summary>Controls explicit local-only substitutes for external development services.</summary>
public sealed class LocalDevelopmentOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "LocalDevelopment";

    /// <summary>Gets whether local in-memory identity and persistence services are enabled.</summary>
    public bool UseDemoServices { get; init; }

    /// <summary>Rejects local substitutes outside the Development environment.</summary>
    /// <param name="environmentName">Current ASP.NET Core environment name.</param>
    public void Validate(string environmentName)
    {
        if (UseDemoServices && !string.Equals(environmentName, Environments.Development, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{SectionName}:UseDemoServices can only be enabled in the Development environment.");
        }
    }
}
