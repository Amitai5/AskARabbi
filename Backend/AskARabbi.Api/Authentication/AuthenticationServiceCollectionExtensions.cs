using WorkOS;

namespace AskARabbi.Api.Authentication;

internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddAskRabbiAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var options = configuration.GetSection(WorkOsAuthenticationOptions.SectionName).Get<WorkOsAuthenticationOptions>() ?? new WorkOsAuthenticationOptions();
        services.AddSingleton(options);

        if (!options.IsConfigured)
        {
            services.AddSingleton<IUserAuthenticationService, UnavailableUserAuthenticationService>();
            return services;
        }

        options.Validate(!environment.IsDevelopment());
        services.AddSingleton(new WorkOSClient(new WorkOSOptions
        {
            ApiKey = options.ApiKey,
            ClientId = options.ClientId,
            MaxRetries = 2,
        }));
        services.AddSingleton<IUserAuthenticationService, WorkOsUserAuthenticationService>();
        return services;
    }
}
