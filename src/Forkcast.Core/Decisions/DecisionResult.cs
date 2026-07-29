using Forkcast.Core.Challenges;
using Forkcast.Core.Comparison;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;
using Forkcast.Core.Recommendations;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;

namespace Forkcast.Core.Decisions;

/// <summary>
/// Everything Forkcast produces for one incident: the two simulated futures, the comparison
/// between them, the recommendation, and the verification record for the text on screen.
/// </summary>
public sealed record DecisionResult
{
    public required Incident Incident { get; init; }

    public required IReadOnlyList<ResponsePlan> Plans { get; init; }

    public required IReadOnlyList<PlanOutcome> Outcomes { get; init; }

    public required PlanComparison Comparison { get; init; }

    public required Recommendation Recommendation { get; init; }

    public required ClaimVerification Verification { get; init; }

    /// <summary>The text safe to display, whoever wrote it.</summary>
    public required string ExecutiveSummary { get; init; }

    public required long Seed { get; init; }

    public required int TrialCount { get; init; }

    public required string IntelligenceProvider { get; init; }

    public required bool IntelligenceLive { get; init; }

    /// <summary>Set only on a challenge result, describing what was changed and rerun.</summary>
    public AssumptionOverride? Assumption { get; init; }

    /// <summary>The result this one is being compared against, when it is a challenge.</summary>
    public DecisionDelta? Delta { get; init; }
}

/// <summary>How a challenged run differs from the run it was launched from.</summary>
public sealed record DecisionDelta
{
    public required double PreviousOnTimeDeparturePct { get; init; }

    public required double OnTimeDeparturePct { get; init; }

    public required double OnTimeChangePp { get; init; }

    public required int PreviousVehiclesAtRisk { get; init; }

    public required int VehiclesAtRisk { get; init; }

    public required RiskLevel PreviousRiskLevel { get; init; }

    public required RiskLevel RiskLevel { get; init; }

    public required bool RecommendationChanged { get; init; }

    /// <summary>A single sentence built only from the two verified figures above.</summary>
    public required string Summary { get; init; }
}
