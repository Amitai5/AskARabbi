namespace AskARabbi.Api.Configuration;

/// <summary>Configures browser origins that may call the AskRabbi API with credentials.</summary>
public sealed class FrontendCorsOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Gets the restrictive CORS policy name.</summary>
    public const string PolicyName = "AskRabbiFrontend";

    /// <summary>Gets explicitly allowed frontend origins.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];

    /// <summary>Validates and normalizes configured origins, adding the Vite origin only in development when none are configured.</summary>
    /// <param name="isDevelopment">Whether the current host environment is Development.</param>
    /// <returns>Distinct normalized origins.</returns>
    public IReadOnlyList<string> GetAllowedOrigins(bool isDevelopment)
    {
        var values = AllowedOrigins.Count == 0 && isDevelopment ? ["http://localhost:5173"] : AllowedOrigins;
        var normalized = new List<string>(values.Count);

        foreach (var value in values)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException($"{SectionName}:AllowedOrigins entries must be HTTP or HTTPS origins without a path, query, or fragment.");
            }

            normalized.Add(uri.GetLeftPart(UriPartial.Authority));
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
