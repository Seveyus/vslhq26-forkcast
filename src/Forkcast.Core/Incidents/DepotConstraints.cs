namespace Forkcast.Core.Incidents;

/// <summary>
/// Site level constraints that apply regardless of which response plan is chosen.
/// </summary>
public sealed record DepotConstraints
{
    /// <summary>
    /// Capacity shared by the AC charging array, in kW, after the site base load is subtracted
    /// from the grid connection. When the combined rating of the AC posts exceeds this, the
    /// site's static load management shares the available capacity evenly between them.
    /// The DC fast charger sits on its own feeder and is not subject to this limit.
    /// </summary>
    public required double AcArrayCapacityKw { get; init; }

    /// <summary>Fixed part of the manual re-plug delay when a charge point frees up, in minutes.</summary>
    public required double PlugSwapBaseMinutes { get; init; }

    /// <summary>Mean of the exponential tail added to the re-plug delay, in minutes.</summary>
    public required double PlugSwapTailMeanMinutes { get; init; }

    /// <summary>Upper bound applied to a single re-plug delay, in minutes.</summary>
    public required double PlugSwapMaxMinutes { get; init; }

    /// <summary>Walk-around checks before departure. A vehicle must be charged this far ahead.</summary>
    public required double PreDepartureReadyMinutes { get; init; }

    /// <summary>Standard deviation of departure time jitter, in minutes.</summary>
    public required double DepartureJitterStdDevMinutes { get; init; }

    /// <summary>Probability that the failed charge point is repaired before the deadline.</summary>
    public required double FaultRecoveryProbability { get; init; }

    public required DateTimeOffset FaultRecoveryWindowStart { get; init; }

    public required DateTimeOffset FaultRecoveryWindowEnd { get; init; }

    /// <summary>Mean of the effective power factor applied to each charge point.</summary>
    public required double ChargePointPowerFactorMean { get; init; }

    public required double ChargePointPowerFactorStdDev { get; init; }

    /// <summary>Standard deviation of the per-vehicle energy requirement, as a multiplier.</summary>
    public required double EnergyRequirementStdDev { get; init; }

    /// <summary>State of charge tolerance, in kWh, before a vehicle counts as short.</summary>
    public required double EnergyToleranceKwh { get; init; }
}
