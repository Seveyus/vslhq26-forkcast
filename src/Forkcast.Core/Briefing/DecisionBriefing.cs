using Forkcast.Core.Verification;

namespace Forkcast.Core.Briefing;

/// <summary>One beat of a decision brief: a moment on screen and the evidence behind it.</summary>
public sealed record BriefingBeat
{
    public required string Id { get; init; }

    /// <summary>"situation", "futures", "recommendation", "evidence", "counterfactual" or "close".</summary>
    public required string Kind { get; init; }

    public required double StartSeconds { get; init; }

    public required double DurationSeconds { get; init; }

    public required string Heading { get; init; }

    /// <summary>On-screen caption. Composed from claim display values, never from free prose.</summary>
    public required string Caption { get; init; }

    /// <summary>Claims this beat is allowed to show. Empty means the beat carries no figures.</summary>
    public required IReadOnlyList<string> ClaimIds { get; init; }
}

/// <summary>How one unit is faring, for the canvas and for the brief.</summary>
public sealed record CanvasUnit
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required bool IsPriority { get; init; }

    /// <summary>Share of trials this unit met its requirement in, 0 to 1.</summary>
    public required double OnTimeProbability { get; init; }

    public required double ShortfallLevel { get; init; }

    public required double SlackMinutes { get; init; }

    public required bool AtRisk { get; init; }
}

/// <summary>How one resource stands, for the canvas.</summary>
public sealed record CanvasResource
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required double Rate { get; init; }

    public required bool Operational { get; init; }

    public required string? FaultCode { get; init; }
}

/// <summary>The state of one plan, drawn.</summary>
public sealed record CanvasPlan
{
    public required string PlanId { get; init; }

    public required string PlanName { get; init; }

    public required bool Recommended { get; init; }

    public required double OnTimePct { get; init; }

    public required int AtRiskCount { get; init; }

    public required string RiskLevel { get; init; }

    public required IReadOnlyList<CanvasUnit> Units { get; init; }
}

/// <summary>
/// A decision brief, composed from a finished and verified decision.
/// </summary>
/// <remarks>
/// <para>
/// This exists so the animated briefing is demonstrably a projection of the current verified world
/// state rather than a template with the numbers poured in. Every caption here is built from claim
/// display values and the incident's own vocabulary, so a brief cannot contain a figure the claim
/// set does not carry — the same rule the executive summary lives under.
/// </para>
/// <para>
/// It is the payload a renderer consumes. Switch domain and the beats re-word themselves; change an
/// assumption and the counterfactual beat appears with the real before-and-after in it.
/// </para>
/// </remarks>
public sealed record DecisionBriefing
{
    public required string DomainKey { get; init; }

    public required string DomainLabel { get; init; }

    public required string Title { get; init; }

    /// <summary>One sentence naming the bind, from incident facts only.</summary>
    public required string Situation { get; init; }

    public required string RecommendedPlanId { get; init; }

    public required string Headline { get; init; }

    public required long Seed { get; init; }

    public required int TrialCount { get; init; }

    public required int VerifiedClaims { get; init; }

    public required int UnsupportedNumbers { get; init; }

    /// <summary>
    /// The claim set the beats may draw on. Carried in the payload so a renderer can resolve every
    /// <see cref="BriefingBeat.ClaimIds"/> entry without a second request — and so it can never
    /// invent one it was not given.
    /// </summary>
    public required IReadOnlyList<Claim> Claims { get; init; }

    public required IReadOnlyList<BriefingBeat> Beats { get; init; }

    public required IReadOnlyList<CanvasResource> Resources { get; init; }

    public required IReadOnlyList<CanvasPlan> Plans { get; init; }

    /// <summary>Present only when the brief was produced after a counterfactual was applied.</summary>
    public string? CounterfactualLabel { get; init; }

    public double TotalSeconds => Beats.Count == 0
        ? 0
        : Beats[^1].StartSeconds + Beats[^1].DurationSeconds;
}
