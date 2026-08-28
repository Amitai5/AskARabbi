namespace AskARabbi.Api.Authentication;

/// <summary>Identifies an optional provider selected before entering hosted authentication.</summary>
public enum ExternalAuthenticationProvider
{
    /// <summary>Uses Google OAuth through WorkOS AuthKit.</summary>
    Google,

    /// <summary>Uses Apple OAuth through WorkOS AuthKit.</summary>
    Apple,

    /// <summary>Uses Microsoft OAuth through WorkOS AuthKit.</summary>
    Microsoft,
}
