using Forkcast.Core.Incidents;

namespace Forkcast.Core.Plans;

/// <summary>How the yard team decides who gets plugged in next.</summary>
public enum ChargingPolicy
{
    /// <summary>
    /// Leave the pre-incident rota untouched. Vehicles that were rostered onto the failed
    /// charge point fall to the back of the yard list.
    /// </summary>
    KeepExistingSchedule,

    /// <summary>Priority routes first, then whichever vehicle has the least schedule slack.</summary>
    PriorityAndTightestMargin
}

/// <summary>How full each vehicle is charged before the connector is moved on.</summary>
public enum ChargeTargetPolicy
{
    /// <summary>Charge to 100%, as the standing overnight rota does.</summary>
    Full,

    /// <summary>Charge to the route requirement plus a small operating margin, then move on.</summary>
    RouteRequirementPlusMargin
}

/// <summary>
/// A towed battery unit that can be brought on site. It is battery fed, so unlike the site
/// chargers it is not limited by the depot grid connection.
/// </summary>
public sealed record MobileBufferOption
{
    public required int Outlets { get; init; }

    public required double OutletPowerKw { get; init; }

    /// <summary>Usable energy in the trailer. This is the hard cap on what it can deliver.</summary>
    public required double StoredEnergyKwh { get; init; }

    public required DateTimeOffset PlannedArrival { get; init; }

    public required double ArrivalDelayMeanMinutes { get; init; }

    public required double ArrivalDelayStdDevMinutes { get; init; }

    public required double CallOutCostGbp { get; init; }

    public required double EnergyCostPerKwhGbp { get; init; }

    /// <summary>Applies a what-if delay without touching any other part of the option.</summary>
    public MobileBufferOption DelayedBy(TimeSpan delay) => this with
    {
        PlannedArrival = PlannedArrival + delay
    };
}

/// <summary>
/// One candidate response to the incident. The language model may name and describe a plan;
/// only these structured levers ever reach the simulation.
/// </summary>
public sealed record ResponsePlan
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>One imperative sentence a duty manager can act on.</summary>
    public required string Headline { get; init; }

    public required string Description { get; init; }

    /// <summary>Concrete operational steps, shown to the duty manager.</summary>
    public required IReadOnlyList<string> Actions { get; init; }

    public required ChargingPolicy ChargingPolicy { get; init; }

    public required ChargeTargetPolicy ChargeTargetPolicy { get; init; }

    /// <summary>Extra state of charge above the route requirement, in percent.</summary>
    public required double ChargeMarginPct { get; init; }

    public MobileBufferOption? MobileBuffer { get; init; }

    /// <summary>Fixed cost of running the plan, before any energy is priced.</summary>
    public required double FixedInterventionCostGbp { get; init; }

    public bool UsesMobileBuffer => MobileBuffer is not null;

    public double TargetEnergyKwh(Vehicle vehicle) => ChargeTargetPolicy switch
    {
        ChargeTargetPolicy.Full => vehicle.EnergyToFullKwh,
        _ => vehicle.EnergyToTargetKwh(ChargeMarginPct)
    };
}
