using Forkcast.Core.Simulation;

namespace Forkcast.Core.Comparison;

/// <summary>
/// The head-to-head result between the do-nothing baseline and the alternative response.
/// </summary>
public sealed record PlanComparison
{
    /// <summary>The plan that changes nothing. Always the reference point for "additional" costs.</summary>
    public required PlanOutcome Baseline { get; init; }

    public required PlanOutcome Alternative { get; init; }

    public required string RecommendedPlanId { get; init; }

    public required string DecisionRule { get; init; }

    /// <summary>Difference in on-time departures, in percentage points.</summary>
    public required double OnTimeImprovementPp { get; init; }

    public required int VehiclesAtRiskAvoided { get; init; }

    public required double UnmetEnergyAvoidedKwh { get; init; }

    /// <summary>Cost of acting, over and above doing nothing.</summary>
    public required double AdditionalCostGbp { get; init; }

    /// <summary>Additional cost divided by the number of departures the action secures.</summary>
    public required double CostPerDepartureSecuredGbp { get; init; }

    public PlanOutcome Recommended =>
        Alternative.PlanId == RecommendedPlanId ? Alternative : Baseline;

    public PlanOutcome Rejected =>
        Alternative.PlanId == RecommendedPlanId ? Baseline : Alternative;
}
