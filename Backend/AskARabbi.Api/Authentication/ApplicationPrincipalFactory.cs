using System.Security.Claims;
using AskARabbiLIB.Accounts;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AskARabbi.Api.Authentication;

internal static class ApplicationPrincipalFactory
{
    internal static ClaimsPrincipal Create(UserAccount account, string? workOsSessionId)
    {
        ArgumentNullException.ThrowIfNull(account);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString("D")),
            new(ClaimTypes.Name, account.DisplayName),
            new(ClaimTypes.Email, account.Email),
            new(ApplicationClaimTypes.WorkOsUserId, account.ProviderUserId),
        };
        if (!string.IsNullOrWhiteSpace(workOsSessionId))
        {
            claims.Add(new Claim(ApplicationClaimTypes.WorkOsSessionId, workOsSessionId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
