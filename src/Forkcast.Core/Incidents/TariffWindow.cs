namespace Forkcast.Core.Incidents;

/// <summary>
/// A time of use electricity price band. Bands are expected to be contiguous and to cover
/// the whole charging window.
/// </summary>
public sealed record TariffWindow
{
    public required string Label { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public required double PricePerKwhGbp { get; init; }
}
