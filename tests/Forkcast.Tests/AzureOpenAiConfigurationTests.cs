using Forkcast.Api.Ai;
using Forkcast.Api.Configuration;
using Forkcast.Core.Ai;
using Forkcast.Core.Challenges;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Forkcast.Tests;

public class AzureOpenAiConfigurationTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void No_configuration_means_deterministic_mode_not_a_failure()
    {
        var options = AzureOpenAiOptions.FromConfiguration(Configuration());

        Assert.False(options.IsConfigured);
        Assert.Null(options.Endpoint);
        Assert.Equal("2024-10-21", options.ApiVersion);
    }

    [Theory]
    [InlineData("AZURE_OPENAI_ENDPOINT")]
    [InlineData("AZURE_OPENAI_API_KEY")]
    [InlineData("AZURE_OPENAI_DEPLOYMENT")]
    public void Partial_configuration_is_treated_as_no_configuration(string omitted)
    {
        var values = new List<(string, string)>
        {
            ("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com"),
            ("AZURE_OPENAI_API_KEY", "not-a-real-key"),
            ("AZURE_OPENAI_DEPLOYMENT", "gpt-4o-mini")
        };

        values.RemoveAll(v => v.Item1 == omitted);

        Assert.False(AzureOpenAiOptions.FromConfiguration(Configuration([.. values])).IsConfigured);
    }

    [Fact]
    public void A_complete_configuration_builds_the_expected_endpoint()
    {
        var options = AzureOpenAiOptions.FromConfiguration(Configuration(
            ("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com/"),
            ("AZURE_OPENAI_API_KEY", "not-a-real-key"),
            ("AZURE_OPENAI_DEPLOYMENT", "gpt-4o-mini")));

        Assert.True(options.IsConfigured);
        Assert.Equal(
            "https://example.openai.azure.com/openai/deployments/gpt-4o-mini/chat/completions?api-version=2024-10-21",
            options.ChatCompletionsUri.ToString());
    }

    [Fact]
    public void A_malformed_endpoint_does_not_enable_the_provider()
    {
        var options = AzureOpenAiOptions.FromConfiguration(Configuration(
            ("AZURE_OPENAI_ENDPOINT", "not a url"),
            ("AZURE_OPENAI_API_KEY", "not-a-real-key"),
            ("AZURE_OPENAI_DEPLOYMENT", "gpt-4o-mini")));

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void Environment_style_keys_win_over_appsettings()
    {
        var options = AzureOpenAiOptions.FromConfiguration(Configuration(
            ("AZURE_OPENAI_ENDPOINT", "https://from-env.openai.azure.com"),
            ("AzureOpenAI:Endpoint", "https://from-settings.openai.azure.com"),
            ("AZURE_OPENAI_API_KEY", "not-a-real-key"),
            ("AZURE_OPENAI_DEPLOYMENT", "gpt-4o-mini")));

        Assert.Equal("https://from-env.openai.azure.com", options.Endpoint);
    }

    [Fact]
    public void An_absurd_timeout_falls_back_to_the_default()
    {
        var options = AzureOpenAiOptions.FromConfiguration(Configuration(
            ("AZURE_OPENAI_TIMEOUT_SECONDS", "99999")));

        Assert.Equal(12, options.TimeoutSeconds);
    }

    [Fact]
    public void Without_credentials_the_container_resolves_the_deterministic_provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ChallengeService>();
        services.AddForkcastIntelligence(Configuration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var intelligence = scope.ServiceProvider.GetRequiredService<IIncidentIntelligence>();

        Assert.False(intelligence.IsLive);
        Assert.Equal("Deterministic", intelligence.ProviderName);
    }

    [Fact]
    public void With_credentials_the_container_resolves_azure_openai()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ChallengeService>();
        services.AddForkcastIntelligence(Configuration(
            ("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com"),
            ("AZURE_OPENAI_API_KEY", "not-a-real-key"),
            ("AZURE_OPENAI_DEPLOYMENT", "gpt-4o-mini")));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        var intelligence = scope.ServiceProvider.GetRequiredService<IIncidentIntelligence>();

        Assert.True(intelligence.IsLive);
        Assert.Equal("Azure OpenAI", intelligence.ProviderName);
    }

    /// <summary>
    /// Plan wording is the only generated prose that does not pass through the claim verifier,
    /// because plans are described before anything is simulated. The rule that protects it is
    /// blunt on purpose.
    /// </summary>
    [Theory]
    [InlineData("Re-sequence the yard queue and bring in a towed battery unit.", true)]
    [InlineData("Leave the rota untouched and repair the charger in the morning.", true)]
    [InlineData("Bring in a 420 kWh battery unit.", false)]
    [InlineData("This lifts on-time departures to 97 percent.", false)]
    [InlineData("Costs about £380 more.", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Plan_wording_containing_any_digit_is_rejected(string? description, bool expected)
    {
        Assert.Equal(expected, PlanWording.IsAcceptable(description));
    }

    [Fact]
    public void Plan_wording_that_runs_long_is_rejected()
    {
        Assert.False(PlanWording.IsAcceptable(new string('a', 401)));
    }

    [Fact]
    public void A_dot_env_file_is_read_without_overriding_the_real_environment()
    {
        var path = Path.Combine(Path.GetTempPath(), $"forkcast-{Guid.NewGuid():N}.env");
        File.WriteAllText(
            path,
            """
            # a comment
            AZURE_OPENAI_ENDPOINT=https://from-dotenv.openai.azure.com
            AZURE_OPENAI_DEPLOYMENT="gpt-4o-mini"

            MALFORMED_LINE
            EMPTY_VALUE=
            """);

        try
        {
            var configuration = new ConfigurationBuilder().AddDotEnvFile(path).Build();

            Assert.Equal("https://from-dotenv.openai.azure.com", configuration["AZURE_OPENAI_ENDPOINT"]);
            Assert.Equal("gpt-4o-mini", configuration["AZURE_OPENAI_DEPLOYMENT"]);
            Assert.Null(configuration["MALFORMED_LINE"]);
            Assert.Null(configuration["EMPTY_VALUE"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_missing_dot_env_file_is_not_an_error()
    {
        var configuration = new ConfigurationBuilder()
            .AddDotEnvFile(Path.Combine(Path.GetTempPath(), "forkcast-does-not-exist.env"))
            .Build();

        Assert.Null(configuration["AZURE_OPENAI_ENDPOINT"]);
    }
}
