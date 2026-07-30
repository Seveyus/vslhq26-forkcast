using System.Globalization;
using Forkcast.Core.Ai;
using Forkcast.Core.Challenges;
using Forkcast.Core.Comparison;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;
using Forkcast.Core.Recommendations;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;

namespace Forkcast.Core.Decisions;

/// <summary>
/// Orchestrates one full pass of the agent: simulate every plan, compare them, build the claim
/// set, ask for an explanation, verify it, and recommend.
/// </summary>
/// <remarks>
/// The order matters. Simulation and comparison finish before the language model is consulted,
/// and the model is handed the finished claims as input. There is no path by which a generated
/// figure can reach the interface.
/// </remarks>
public sealed class DecisionService(
    SimulationEngine engine,
    ComparisonService comparisons,
    ClaimSetBuilder claimSets,
    ClaimVerifier verifier,
    RecommendationService recommendations,
    ChallengeService challenges,
    IIncidentIntelligence intelligence)
{
    public async Task<DecisionResult> DecideAsync(
        Incident incident,
        IReadOnlyList<ResponsePlan> plans,
        SimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(plans);

        if (plans.Count < 2)
        {
            throw new ArgumentException("Forkcast compares two response plans.", nameof(plans));
        }

        var opts = (options ?? SimulationOptions.Default).Validated();

        var outcomes = plans.Select(plan => engine.Run(incident, plan, opts)).ToList();
        var comparison = comparisons.Compare(outcomes[0], outcomes[1]);
        var claims = claimSets.Build(comparison, incident.Vocabulary);
        var recommendation = recommendations.Build(comparison, plans, claims, incident.Vocabulary);

        var candidate = await SafeWriteSummaryAsync(
            incident, comparison, recommendation, claims, opts, cancellationToken);

        var verification = verifier.Verify(
            candidate,
            intelligence.IsLive ? "azure-openai" : "deterministic",
            recommendation.DeterministicSummary,
            claims,
            VerificationContext.FromIncident(incident, opts));

        return new DecisionResult
        {
            Incident = incident,
            Plans = plans,
            Outcomes = outcomes,
            Comparison = comparison,
            Recommendation = recommendation,
            Verification = verification,
            ExecutiveSummary = verification.Narrative,
            Seed = opts.Seed,
            TrialCount = opts.TrialCount,
            IntelligenceProvider = intelligence.ProviderName,
            IntelligenceLive = intelligence.IsLive
        };
    }

    /// <summary>
    /// Reruns the decision with one assumption changed, and reports the difference against the
    /// run being challenged.
    /// </summary>
    public async Task<DecisionResult> ChallengeAsync(
        Incident incident,
        IReadOnlyList<ResponsePlan> plans,
        string question,
        SimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(plans);

        var opts = (options ?? SimulationOptions.Default).Validated();
        var baselineResult = await DecideAsync(incident, plans, opts, cancellationToken);

        // The classifier is trusted for the lever, never for the wording: the sentence a user
        // reads is composed here from this incident's own vocabulary.
        var assumption = (await SafeInterpretAsync(question, cancellationToken))
            .Relabel(incident.Vocabulary);

        // The classifier may not know this domain's nouns. The deterministic matcher does when it
        // is handed the vocabulary, so an unrecognised question gets one domain-aware second look
        // before we tell the user nothing was changed.
        if (!assumption.Recognised)
        {
            assumption = challenges.Interpret(question, incident.Vocabulary)
                .Relabel(incident.Vocabulary);
        }

        if (!assumption.Recognised)
        {
            return baselineResult with { Assumption = assumption };
        }

        var (changedIncident, changedPlans) = challenges.Apply(incident, plans, assumption);
        var challenged = await DecideAsync(changedIncident, changedPlans, opts, cancellationToken);

        return challenged with
        {
            Assumption = assumption,
            Delta = BuildDelta(baselineResult, challenged, assumption, incident.Vocabulary)
        };
    }

    private static DecisionDelta BuildDelta(
        DecisionResult before,
        DecisionResult after,
        AssumptionOverride assumption,
        IncidentVocabulary words)
    {
        var previous = before.Comparison.Recommended;
        var current = after.Comparison.Recommended;
        var previousPrefix = previous.PlanId == before.Comparison.Baseline.PlanId
            ? "baseline"
            : "alternative";
        var previousOnTimeClaim = PreviousClaim(
            before.Verification.Claims,
            $"{previousPrefix}-on-time");
        var previousAtRiskClaim = PreviousClaim(
            before.Verification.Claims,
            $"{previousPrefix}-at-risk");
        var change = Math.Round(
            current.OnTimeDeparturePct - previous.OnTimeDeparturePct, 1, MidpointRounding.AwayFromZero);

        var direction = change switch
        {
            < 0 => "reduces",
            > 0 => "raises",
            _ => "leaves unchanged"
        };

        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"{assumption.Label}. This {direction} expected {words.OnTimeMetricLabel} from "
            + $"{previous.OnTimeDeparturePct:0.#}% to {current.OnTimeDeparturePct:0.#}%, and moves "
            + $"{words.UnitPlural} at risk from {previous.VehiclesAtRisk} to {current.VehiclesAtRisk}.");

        return new DecisionDelta
        {
            PreviousOnTimeClaim = previousOnTimeClaim,
            PreviousAtRiskClaim = previousAtRiskClaim,
            PreviousOnTimeDeparturePct = previous.OnTimeDeparturePct,
            OnTimeDeparturePct = current.OnTimeDeparturePct,
            OnTimeChangePp = change,
            PreviousVehiclesAtRisk = previous.VehiclesAtRisk,
            VehiclesAtRisk = current.VehiclesAtRisk,
            PreviousRiskLevel = previous.RiskLevel,
            RiskLevel = current.RiskLevel,
            RecommendationChanged =
                before.Comparison.RecommendedPlanId != after.Comparison.RecommendedPlanId,
            Summary = summary
        };
    }

    private static Claim PreviousClaim(IReadOnlyList<Claim> claims, string id)
    {
        var claim = claims.Single(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        return claim with
        {
            Id = $"previous-{claim.Id}",
            Label = $"Previous recommended outcome: {claim.Label}"
        };
    }

    /// <summary>
    /// A failure in the language layer must never take the decision down, so the summary call
    /// degrades to the deterministic wording instead of propagating.
    /// </summary>
    private async Task<string?> SafeWriteSummaryAsync(
        Incident incident,
        PlanComparison comparison,
        Recommendation recommendation,
        IReadOnlyList<Claim> claims,
        SimulationOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await intelligence.WriteExecutiveSummaryAsync(
                new ExecutiveSummaryRequest
                {
                    IncidentTitle = incident.Title,
                    RecommendedPlanName = recommendation.RecommendedPlanName,
                    RecommendedHeadline = recommendation.Headline,
                    DecisionRule = comparison.DecisionRule,
                    CriticalConstraint = recommendation.CriticalConstraint,
                    Claims = claims,
                    Seed = options.Seed,
                    TrialCount = options.TrialCount
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<AssumptionOverride> SafeInterpretAsync(
        string question,
        CancellationToken cancellationToken)
    {
        try
        {
            return await intelligence.InterpretChallengeAsync(question, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return challenges.Interpret(question);
        }
    }
}
