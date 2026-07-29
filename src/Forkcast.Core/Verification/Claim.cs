using System.Globalization;

namespace Forkcast.Core.Verification;

/// <summary>
/// A single numerical statement Forkcast is willing to put on screen.
/// </summary>
/// <remarks>
/// Every number the interface displays must exist as a claim. A claim carries its own
/// provenance: which simulation field it came from, how that field is computed, and under
/// which seed and trial count. If a number cannot be expressed as a claim, it does not get shown.
/// </remarks>
public sealed record Claim
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required double Value { get; init; }

    /// <summary>One of "%", "vehicles", "percentage points", "GBP", "kWh".</summary>
    public required string Unit { get; init; }

    /// <summary>Dotted path of the simulation output field this value was read from.</summary>
    public required string SourceField { get; init; }

    public required string CalculationMethod { get; init; }

    public required long SimulationSeed { get; init; }

    public required int TrialCount { get; init; }

    /// <summary>
    /// True when the value still round-trips to <see cref="SourceField"/> in the simulation
    /// output. Set by <see cref="ClaimSetBuilder"/>, never by a caller.
    /// </summary>
    public required bool Verified { get; init; }

    public string DisplayValue => Unit switch
    {
        "%" => Value.ToString("0.#", CultureInfo.InvariantCulture) + "%",
        "percentage points" => (Value >= 0 ? "+" : "")
                               + Value.ToString("0.#", CultureInfo.InvariantCulture) + " pp",
        "GBP" => "£" + Value.ToString("0", CultureInfo.InvariantCulture),
        "kWh" => Value.ToString("0.#", CultureInfo.InvariantCulture) + " kWh",
        "vehicles" => Value.ToString("0", CultureInfo.InvariantCulture),
        _ => Value.ToString("0.##", CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// Numeric forms a narrative is allowed to use for this claim: the exact value plus the
    /// sensible roundings a writer would reach for.
    /// </summary>
    public IEnumerable<double> AcceptableForms()
    {
        yield return Value;
        yield return Math.Round(Value, 0, MidpointRounding.AwayFromZero);
        yield return Math.Round(Value, 1, MidpointRounding.AwayFromZero);
        yield return Math.Round(Value, 2, MidpointRounding.AwayFromZero);
        yield return Math.Abs(Value);
        yield return Math.Round(Math.Abs(Value), 0, MidpointRounding.AwayFromZero);
        yield return Math.Round(Math.Abs(Value), 1, MidpointRounding.AwayFromZero);
    }
}
