namespace Forkcast.Core.Simulation;

/// <summary>
/// Knobs for a simulation run. The seed and trial count are surfaced on every claim so that
/// any number in the interface can be traced back to a reproducible run.
/// </summary>
public sealed record SimulationOptions
{
    /// <summary>Default seed used by the demo. Deliberately fixed and published.</summary>
    public const long DefaultSeed = 20260728L;

    public const int DefaultTrialCount = 500;

    public long Seed { get; init; } = DefaultSeed;

    public int TrialCount { get; init; } = DefaultTrialCount;

    /// <summary>A vehicle counts as at risk when its on-time probability falls below this.</summary>
    public double AtRiskProbabilityThreshold { get; init; } = 0.90;

    public static SimulationOptions Default => new();

    public SimulationOptions Validated()
    {
        if (TrialCount is < 1 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TrialCount), TrialCount, "Trial count must be between 1 and 5000.");
        }

        if (AtRiskProbabilityThreshold is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AtRiskProbabilityThreshold),
                AtRiskProbabilityThreshold,
                "At-risk threshold must be between 0 (exclusive) and 1.");
        }

        return this;
    }
}
