namespace Forkcast.Core.Incidents;

/// <summary>
/// A single electric delivery vehicle waiting at the depot overnight.
/// </summary>
public sealed record Vehicle
{
    public required string Id { get; init; }

    public required string Route { get; init; }

    public required double BatteryCapacityKwh { get; init; }

    /// <summary>State of charge when the incident was detected, in percent.</summary>
    public required double InitialStateOfChargePct { get; init; }

    /// <summary>State of charge the route needs before the vehicle can leave, in percent.</summary>
    public required double RequiredStateOfChargePct { get; init; }

    /// <summary>Onboard AC charger limit. Depot wall boxes are constrained by this.</summary>
    public required double MaxAcChargePowerKw { get; init; }

    /// <summary>DC limit. Applies to the fast charger and to the mobile battery buffer.</summary>
    public required double MaxDcChargePowerKw { get; init; }

    public required bool IsPriorityRoute { get; init; }

    public required DateTimeOffset ScheduledDeparture { get; init; }

    /// <summary>
    /// Charge point this vehicle was rostered onto before the incident. When that charge point
    /// is down, "continue current schedule" leaves the vehicle at the back of the yard list.
    /// </summary>
    public required string RosteredChargePointId { get; init; }

    /// <summary>Energy needed to reach the route requirement, ignoring charging losses.</summary>
    public double RequiredEnergyKwh =>
        Math.Max(0.0, (RequiredStateOfChargePct - InitialStateOfChargePct) / 100.0 * BatteryCapacityKwh);

    /// <summary>Energy needed to reach a 100% state of charge.</summary>
    public double EnergyToFullKwh =>
        Math.Max(0.0, (100.0 - InitialStateOfChargePct) / 100.0 * BatteryCapacityKwh);

    /// <summary>Energy needed to reach the requirement plus an operating margin.</summary>
    public double EnergyToTargetKwh(double marginPct)
    {
        var target = Math.Min(100.0, RequiredStateOfChargePct + marginPct);
        return Math.Max(0.0, (target - InitialStateOfChargePct) / 100.0 * BatteryCapacityKwh);
    }
}
