using Forkcast.Core.Ai;
using Forkcast.Core.Challenges;
using Forkcast.Core.Comparison;
using Forkcast.Core.Decisions;
using Forkcast.Core.Recommendations;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;

namespace Forkcast.Tests;

/// <summary>Builds the decision pipeline the way the API composes it.</summary>
internal static class TestHarness
{
    public static DecisionService BuildDecisionService(IIncidentIntelligence? intelligence = null)
    {
        var challenges = new ChallengeService();
        return new DecisionService(
            new SimulationEngine(),
            new ComparisonService(),
            new ClaimSetBuilder(),
            new ClaimVerifier(),
            new RecommendationService(),
            challenges,
            intelligence ?? new DeterministicIntelligence(challenges));
    }

    /// <summary>A model that writes whatever it is told to, so verification can be exercised.</summary>
    internal sealed class ScriptedIntelligence(string? summary, bool live = true) : IIncidentIntelligence
    {
        public string ProviderName => "Scripted";

        public bool IsLive => live;

        public Task<ExtractionResult> ExtractAsync(string narrative, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExtractionResult
            {
                Draft = IncidentDraft.Empty,
                Source = "scripted",
                Notes = []
            });

        public Task<IReadOnlyList<PlanNarrative>> DescribePlansAsync(
            Core.Incidents.Incident incident,
            IReadOnlyList<Core.Plans.ResponsePlan> plans,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanNarrative>>([]);

        public Task<string?> WriteExecutiveSummaryAsync(
            ExecutiveSummaryRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(summary);

        public Task<AssumptionOverride> InterpretChallengeAsync(
            string question,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChallengeService().Interpret(question));
    }

    /// <summary>A model that always fails, to prove the pipeline survives it.</summary>
    internal sealed class FailingIntelligence : IIncidentIntelligence
    {
        public string ProviderName => "Failing";

        public bool IsLive => true;

        public Task<ExtractionResult> ExtractAsync(string narrative, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("upstream unavailable");

        public Task<IReadOnlyList<PlanNarrative>> DescribePlansAsync(
            Core.Incidents.Incident incident,
            IReadOnlyList<Core.Plans.ResponsePlan> plans,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("upstream unavailable");

        public Task<string?> WriteExecutiveSummaryAsync(
            ExecutiveSummaryRequest request,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("upstream unavailable");

        public Task<AssumptionOverride> InterpretChallengeAsync(
            string question,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("upstream unavailable");
    }
}
