using AskARabbiLIB.DvarTorah.Audio;
using Azure.Core;
using Azure.Identity;

namespace AskARabbi.Api.DvarTorahAudio;

internal static class DvarTorahAudioServiceCollectionExtensions
{
    internal static IServiceCollection AddDvarTorahAudio(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(DvarTorahAudioOptions.SectionName).Get<DvarTorahAudioOptions>() ?? new DvarTorahAudioOptions();
        services.AddSingleton(options);
        if (!options.Enabled)
        {
            services.AddSingleton<IDvarTorahAudioReader, UnavailableDvarTorahAudioReader>();
            return services;
        }

        options.ValidateStorage();
        services.AddSingleton<IDvarTorahAudioReader>(_ =>
        {
            TokenCredential credential = environment.IsDevelopment() ? new DefaultAzureCredential() : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
            return new AzureBlobDvarTorahAudioStorage(options, credential);
        });
        return services;
    }
}
