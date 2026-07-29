namespace Forkcast.Api.Ai;

/// <summary>
/// Azure OpenAI connection details.
/// </summary>
/// <remarks>
/// Environment variables win over appsettings, because that is where a real key belongs and
/// because it matches the documented <c>.env</c> flow. Absent or partial configuration is not an
/// error: it selects the deterministic provider, and the product runs unchanged.
/// </remarks>
public sealed record AzureOpenAiOptions
{
    public string? Endpoint { get; init; }

    public string? ApiKey { get; init; }

    public string? Deployment { get; init; }

    public string ApiVersion { get; init; } = "2024-10-21";

    public int TimeoutSeconds { get; init; } = 12;

    /// <summary>True only when all three of endpoint, key and deployment are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Deployment)
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out _);

    public Uri ChatCompletionsUri => new(
        $"{Endpoint!.TrimEnd('/')}/openai/deployments/{Deployment}/chat/completions"
        + $"?api-version={ApiVersion}");

    public static AzureOpenAiOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AzureOpenAiOptions
        {
            Endpoint = Read(configuration, "AZURE_OPENAI_ENDPOINT", "Endpoint"),
            ApiKey = Read(configuration, "AZURE_OPENAI_API_KEY", "ApiKey"),
            Deployment = Read(configuration, "AZURE_OPENAI_DEPLOYMENT", "Deployment"),
            ApiVersion = Read(configuration, "AZURE_OPENAI_API_VERSION", "ApiVersion") ?? "2024-10-21",
            TimeoutSeconds =
                int.TryParse(Read(configuration, "AZURE_OPENAI_TIMEOUT_SECONDS", "TimeoutSeconds"), out var seconds)
                && seconds is > 0 and <= 120
                    ? seconds
                    : 12
        };
    }

    private static string? Read(IConfiguration configuration, string environmentKey, string settingKey)
    {
        var value = configuration[environmentKey] ?? configuration[$"AzureOpenAI:{settingKey}"];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
