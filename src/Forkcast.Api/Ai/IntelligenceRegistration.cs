using Forkcast.Api.Diagnostics;
using Forkcast.Core.Ai;
using Forkcast.Core.Challenges;

namespace Forkcast.Api.Ai;

public static class IntelligenceRegistration
{
    /// <summary>
    /// Selects the intelligence provider from configuration.
    /// </summary>
    /// <remarks>
    /// Missing credentials are a supported configuration, not a failure. The deterministic
    /// provider is always registered and always available as the fallback the Azure provider
    /// delegates to, which is why a live demonstration cannot be taken down by a network.
    /// </remarks>
    public static IServiceCollection AddForkcastIntelligence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = AzureOpenAiOptions.FromConfiguration(configuration);
        services.AddSingleton(options);
        services.AddSingleton<DeterministicIntelligence>(
            provider => new DeterministicIntelligence(provider.GetRequiredService<ChallengeService>()));

        if (!options.IsConfigured)
        {
            services.AddScoped<IIncidentIntelligence>(
                provider => provider.GetRequiredService<DeterministicIntelligence>());
            return services;
        }

        // A typed client is not a singleton: its HttpClient lifetime belongs to the factory, so
        // the interface is registered scoped to avoid capturing one for the life of the process.
        services.AddHttpClient<AzureOpenAiIntelligence>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds + 3);
            client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
        });

        services.AddScoped<IIncidentIntelligence>(provider =>
            provider.GetRequiredService<AzureOpenAiIntelligence>());

        return services;
    }

    /// <summary>Reports the selected provider once at startup, so the mode is never a mystery.</summary>
    public static void LogIntelligenceProvider(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var intelligence = scope.ServiceProvider.GetRequiredService<IIncidentIntelligence>();
        Log.IntelligenceProvider(app.Logger, intelligence.ProviderName, intelligence.IsLive);
    }
}
