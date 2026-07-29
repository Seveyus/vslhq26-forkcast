using Forkcast.Core.Challenges;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;
using Forkcast.Core.Verification;

namespace Forkcast.Core.Ai;

public sealed record ExtractionResult
{
    public required IncidentDraft Draft { get; init; }

    /// <summary>"azure-openai" or "deterministic".</summary>
    public required string Source { get; init; }

    /// <summary>Anything the reader should know about how the text was interpreted.</summary>
    public required IReadOnlyList<string> Notes { get; init; }
}

/// <summary>Wording for a response plan. Never carries a metric.</summary>
public sealed record PlanNarrative
{
    public required string PlanId { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }
}

/// <summary>
/// Everything a language model is given when asked to write the executive explanation. Note
/// that it receives the verified claims as explicit inputs and is asked to reuse those exact
/// figures, so any number it produces can be checked against them.
/// </summary>
public sealed record ExecutiveSummaryRequest
{
    public required string IncidentTitle { get; init; }

    public required string RecommendedPlanName { get; init; }

    public required string RecommendedHeadline { get; init; }

    public required string DecisionRule { get; init; }

    public required string CriticalConstraint { get; init; }

    public required IReadOnlyList<Claim> Claims { get; init; }

    public required long Seed { get; init; }

    public required int TrialCount { get; init; }
}

/// <summary>
/// The language boundary of the product. Implementations interpret text and produce wording;
/// none of them is allowed to produce an operational metric.
/// </summary>
public interface IIncidentIntelligence
{
    /// <summary>Shown in the interface badge, e.g. "Azure OpenAI" or "Deterministic".</summary>
    string ProviderName { get; }

    /// <summary>True when a real model is answering rather than the built-in fallback.</summary>
    bool IsLive { get; }

    Task<ExtractionResult> ExtractAsync(string narrative, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanNarrative>> DescribePlansAsync(
        Incident incident,
        IReadOnlyList<ResponsePlan> plans,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns candidate prose, or null to fall back to the deterministic summary. The caller
    /// always passes the result through <see cref="ClaimVerifier"/> before displaying it.
    /// </summary>
    Task<string?> WriteExecutiveSummaryAsync(
        ExecutiveSummaryRequest request,
        CancellationToken cancellationToken = default);

    Task<AssumptionOverride> InterpretChallengeAsync(
        string question,
        CancellationToken cancellationToken = default);
}
