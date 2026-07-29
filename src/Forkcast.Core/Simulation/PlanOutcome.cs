namespace Forkcast.Core.Simulation;

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>Per-vehicle summary across every trial.</summary>
public sealed record VehicleOutcome
{
    public required string VehicleId { get; init; }

    public required string Route { get; init; }

    public required bool IsPriorityRoute { get; init; }

    /// <summary>Share of trials in which the vehicle met its route requirement in time.</summary>
    public required double OnTimeProbability { get; init; }

    public required double ExpectedShortfallKwh { get; init; }

    /// <summary>Mean minutes between reaching the route requirement and the ready-by time.
    /// Negative means the vehicle is typically still short when it has to leave.</summary>
    public required double ExpectedSlackMinutes { get; init; }

    public required bool IsAtRisk { get; init; }
}

/// <summary>A single point on the depot load curve, used for the timeline visual.</summary>
public sealed record LoadSample
{
    public required DateTimeOffset At { get; init; }

    public required double GridPowerKw { get; init; }

    public required double BufferPowerKw { get; init; }

    public required int VehiclesCharging { get; init; }

    public required int VehiclesReady { get; init; }
}

/// <summary>
/// Everything the simulation knows about one response plan. Every number the interface shows
/// originates in this record.
/// </summary>
public sealed record PlanOutcome
{
    public required string PlanId { get; init; }

    public required string PlanName { get; init; }

    public required long Seed { get; init; }

    public required int TrialCount { get; init; }

    public required double OnTimeDeparturePct { get; init; }

    public required double OnTimeDeparturePctP5 { get; init; }

    public required double OnTimeDeparturePctP95 { get; init; }

    public required double PriorityOnTimeDeparturePct { get; init; }

    public required int VehiclesAtRisk { get; init; }

    public required double ExpectedLateVehicles { get; init; }

    public required double ExpectedUnmetEnergyKwh { get; init; }

    public required double ExpectedEnergyCostGbp { get; init; }

    /// <summary>Cost of acting: hire, call-out and buffer energy. Zero for a do-nothing plan.</summary>
    public required double ExpectedInterventionCostGbp { get; init; }

    public required double ExpectedOperationalCostGbp { get; init; }

    public required double ExpectedBufferEnergyKwh { get; init; }

    public required double ExpectedGridEnergyKwh { get; init; }

    public required double ChargePointUtilisationPct { get; init; }

    public required RiskLevel RiskLevel { get; init; }

    public required string CriticalConstraint { get; init; }

    public required IReadOnlyList<VehicleOutcome> Vehicles { get; init; }

    public required IReadOnlyList<LoadSample> LoadCurve { get; init; }
}
