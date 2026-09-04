using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AskARabbi.Api.Authentication;

/// <summary>Checks that the key ring used for browser sessions can protect and recover data.</summary>
public sealed class SessionProtectionHealthCheck : IHealthCheck
{
    private const string ProbePurpose = "AskARabbi.HealthChecks.SessionProtection.v1";
    private const string ProbeValue = "session-protection-probe";
    private readonly IDataProtectionProvider dataProtectionProvider;

    /// <summary>Initializes a check against the application's actual Data Protection provider.</summary>
    /// <param name="dataProtectionProvider">Provider sharing the authentication-cookie key ring.</param>
    public SessionProtectionHealthCheck(IDataProtectionProvider dataProtectionProvider)
    {
        this.dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Use an isolated purpose: no authentication ticket or user data is created or returned.
            var protector = dataProtectionProvider.CreateProtector(ProbePurpose);
            var protectedValue = protector.Protect(ProbeValue);
            var recoveredValue = protector.Unprotect(protectedValue);
            return Task.FromResult(string.Equals(ProbeValue, recoveredValue, StringComparison.Ordinal)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Session encryption did not recover the probe."));
        }
        catch (Exception exception) when (exception is CryptographicException or UnauthorizedAccessException or IOException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Session encryption is unavailable.", exception));
        }
    }
}
