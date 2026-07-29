namespace Forkcast.Api.Configuration;

/// <summary>
/// Loads a <c>.env</c> file into the configuration builder.
/// </summary>
/// <remarks>
/// The README tells people to copy <c>.env.example</c> to <c>.env</c>, so that file has to
/// actually do something. Values already present in the environment are left alone, so a real
/// shell export always wins over a checked-out file.
/// </remarks>
public static class DotEnv
{
    public static IConfigurationBuilder AddDotEnvFile(this IConfigurationBuilder builder, string path)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!File.Exists(path))
        {
            return builder;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');

            if (key.Length == 0 || value.Length == 0)
            {
                continue;
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                values[key] = value;
            }
        }

        return values.Count == 0 ? builder : builder.AddInMemoryCollection(values);
    }

    /// <summary>Walks up from the content root looking for a .env at the repository root.</summary>
    public static string? Locate(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        for (var depth = 0; directory is not null && depth < 6; depth++)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
