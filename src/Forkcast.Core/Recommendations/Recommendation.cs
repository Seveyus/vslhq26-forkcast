using Forkcast.Core.Simulation;

namespace Forkcast.Core.Recommendations;

/// <summary>A single supporting statement, tied to the claim that proves it.</summary>
public sealed record RationalePoint
{
    public required string Text { get; init; }

    /// <summary>Ids of the claims this statement is built from.</summary>
    public required IReadOnlyList<string> ClaimIds { get; init; }
}

/// <summary>
/// The deterministic decision Forkcast is prepared to defend, before any language model is
/// involved.
/// </summary>
public sealed record Recommendation
{
    public required string RecommendedPlanId { get; init; }

    public required string RecommendedPlanName { get; init; }

    public required string Headline { get; init; }

    public required IReadOnlyList<string> Actions { get; init; }

    public required IReadOnlyList<RationalePoint> Rationale { get; init; }

    /// <summary>The decision rule that selected this plan, in plain words.</summary>
    public required string DecisionRule { get; init; }

    public required RiskLevel ResidualRisk { get; init; }

    public required string CriticalConstraint { get; init; }

    /// <summary>
    /// A summary assembled purely from claim values. Used whenever a generated narrative is
    /// rejected, so the interface always has something safe to show.
    /// </summary>
    public required string DeterministicSummary { get; init; }
}
