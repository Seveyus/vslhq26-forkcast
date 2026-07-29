namespace Forkcast.Api.Diagnostics;

/// <summary>Source-generated log messages, so the hot path allocates nothing to say nothing.</summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled failure serving {Path}")]
    public static partial void UnhandledFailure(ILogger logger, Exception? exception, string path);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Forkcast intelligence provider: {Provider} (live: {Live})")]
    public static partial void IntelligenceProvider(ILogger logger, string provider, bool live);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Azure OpenAI call failed, falling back to the deterministic provider")]
    public static partial void AzureOpenAiFallback(ILogger logger, Exception exception);
}
