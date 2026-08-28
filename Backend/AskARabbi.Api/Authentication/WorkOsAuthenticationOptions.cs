namespace AskARabbi.Api.Authentication;

/// <summary>Configures WorkOS AuthKit for the AskRabbi backend.</summary>
public sealed class WorkOsAuthenticationOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "WorkOS";

    /// <summary>Gets the server-only WorkOS API key.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Gets the WorkOS application client ID.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Gets the exact backend callback URL registered with WorkOS.</summary>
    public string RedirectUri { get; init; } = "http://localhost:5090/api/user/callback";

    /// <summary>Gets the frontend destination after authentication and logout.</summary>
    public string FrontendUri { get; init; } = "http://localhost:5173/";

    /// <summary>Gets whether the minimum WorkOS server configuration is present.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>Validates WorkOS configuration.</summary>
    /// <param name="requiresHttps">Whether public redirect URIs must use HTTPS.</param>
    public void Validate(bool requiresHttps = false)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException($"{SectionName}:ApiKey and {SectionName}:ClientId are required.");
        }
        ValidateRedirectUris(requiresHttps);
    }

    /// <summary>Validates the callback and frontend redirect URIs independently of provider credentials.</summary>
    /// <param name="requiresHttps">Whether public redirect URIs must use HTTPS.</param>
    public void ValidateRedirectUris(bool requiresHttps = false)
    {
        var redirectUri = ParseWebUri(RedirectUri, nameof(RedirectUri));
        var frontendUri = ParseWebUri(FrontendUri, nameof(FrontendUri));
        if (requiresHttps && (!string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || !string.Equals(frontendUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{SectionName}:RedirectUri and {SectionName}:FrontendUri must use HTTPS outside Development.");
        }
    }

    private static Uri ParseWebUri(string value, string propertyName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{SectionName}:{propertyName} must be an absolute HTTP or HTTPS URI.");
        }

        return uri;
    }
}
