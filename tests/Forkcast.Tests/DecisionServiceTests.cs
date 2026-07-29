using Forkcast.Core.Challenges;
using Forkcast.Core.Demo;
using Forkcast.Core.Simulation;

namespace Forkcast.Tests;

public class DecisionServiceTests
{
    private const string Challenge = "What happens if the temporary battery arrives one hour late?";

    [Fact]
    public async Task The_whole_decision_runs_without_any_credentials()
    {
        var result = await TestHarness.BuildDecisionService()
            .DecideAsync(DemoScenario.Incident, DemoScenario.Plans);

        Assert.False(result.IntelligenceLive);
        Assert.Equal("Deterministic", result.IntelligenceProvider);
        Assert.Equal("plan-b", result.Recommendation.RecommendedPlanId);
        Assert.Equal(8, result.Verification.TotalClaims);
        Assert.Equal(8, result.Verification.VerifiedClaims);
        Assert.Equal(0, result.Verification.UnsupportedNumbers);
        Assert.Equal("deterministic", result.Verification.NarrativeSource);
        Assert.NotEmpty(result.ExecutiveSummary);
        Assert.Equal(SimulationOptions.DefaultSeed, result.Seed);
        Assert.Equal(SimulationOptions.DefaultTrialCount, result.TrialCount);
    }

    [Fact]
    public async Task The_deterministic_summary_contains_no_unsupported_numbers()
    {
        var service = TestHarness.BuildDecisionService();
        var result = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);

        var findings = new Core.Verification.ClaimVerifier().FindUnsupportedNumbers(
            result.ExecutiveSummary,
            result.Verification.Claims,
            Core.Verification.VerificationContext.FromIncident(
                DemoScenario.Incident, SimulationOptions.Default));

        Assert.Empty(findings);
    }

    [Fact]
    public async Task The_recommendation_carries_its_rule_actions_and_evidence()
    {
        var result = await TestHarness.BuildDecisionService()
            .DecideAsync(DemoScenario.Incident, DemoScenario.Plans);

        Assert.NotEmpty(result.Recommendation.Actions);
        Assert.NotEmpty(result.Recommendation.DecisionRule);
        Assert.NotEmpty(result.Recommendation.CriticalConstraint);
        Assert.NotEmpty(result.Recommendation.Rationale);

        var claimIds = result.Verification.Claims.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var point in result.Recommendation.Rationale)
        {
            Assert.NotEmpty(point.ClaimIds);
            Assert.All(point.ClaimIds, id => Assert.Contains(id, claimIds));
        }
    }

    [Fact]
    public async Task Two_plans_are_required()
    {
        var service = TestHarness.BuildDecisionService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.DecideAsync(DemoScenario.Incident, [DemoScenario.PlanA]));
    }

    [Fact]
    public async Task A_challenge_reruns_the_simulation_and_reports_the_difference()
    {
        var service = TestHarness.BuildDecisionService();

        var before = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);
        var after = await service.ChallengeAsync(DemoScenario.Incident, DemoScenario.Plans, Challenge);

        Assert.NotNull(after.Assumption);
        Assert.Equal(AssumptionKind.BufferArrivalDelayMinutes, after.Assumption!.Kind);
        Assert.Equal(60, after.Assumption.Value);

        Assert.NotNull(after.Delta);
        var delta = after.Delta!;
        Assert.Equal(before.Comparison.Recommended.OnTimeDeparturePct, delta.PreviousOnTimeDeparturePct);
        Assert.True(
            delta.OnTimeDeparturePct < delta.PreviousOnTimeDeparturePct,
            "A late battery unit must not improve the outcome.");
        Assert.True(delta.OnTimeChangePp < 0);
        Assert.True(delta.VehiclesAtRisk > delta.PreviousVehiclesAtRisk);
        Assert.NotEmpty(delta.Summary);
    }

    [Fact]
    public async Task A_challenged_run_is_verified_as_strictly_as_the_original()
    {
        var after = await TestHarness.BuildDecisionService()
            .ChallengeAsync(DemoScenario.Incident, DemoScenario.Plans, Challenge);

        Assert.Equal(8, after.Verification.TotalClaims);
        Assert.Equal(8, after.Verification.VerifiedClaims);
        Assert.Equal(0, after.Verification.UnsupportedNumbers);
    }

    [Fact]
    public async Task A_challenge_that_removes_the_buffer_can_overturn_the_recommendation()
    {
        var after = await TestHarness.BuildDecisionService().ChallengeAsync(
            DemoScenario.Incident, DemoScenario.Plans, "What if the buffer cannot be sourced at all?");

        Assert.Equal(AssumptionKind.BufferUnavailable, after.Assumption!.Kind);
        Assert.NotNull(after.Delta);
        Assert.True(after.Delta!.OnTimeChangePp < 0);
    }

    [Fact]
    public async Task An_unrecognised_challenge_returns_the_original_answer_and_says_so()
    {
        var service = TestHarness.BuildDecisionService();

        var before = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);
        var after = await service.ChallengeAsync(
            DemoScenario.Incident, DemoScenario.Plans, "Is the moon made of cheese?");

        Assert.NotNull(after.Assumption);
        Assert.False(after.Assumption!.Recognised);
        Assert.Null(after.Delta);
        Assert.Equal(
            before.Comparison.Recommended.OnTimeDeparturePct,
            after.Comparison.Recommended.OnTimeDeparturePct);
    }

    [Fact]
    public async Task A_generated_summary_that_uses_only_claim_values_is_shown()
    {
        var probe = await TestHarness.BuildDecisionService()
            .DecideAsync(DemoScenario.Incident, DemoScenario.Plans);
        var onTime = probe.Verification.Claims.Single(c => c.Id == "alternative-on-time");

        var service = TestHarness.BuildDecisionService(
            new TestHarness.ScriptedIntelligence(
                $"Bring in the battery unit: on-time departures reach {onTime.DisplayValue}."));

        var result = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);

        Assert.True(result.Verification.NarrativeAccepted);
        Assert.Equal("azure-openai", result.Verification.NarrativeSource);
        Assert.Contains(onTime.DisplayValue, result.ExecutiveSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_generated_summary_that_invents_a_number_is_discarded()
    {
        var service = TestHarness.BuildDecisionService(
            new TestHarness.ScriptedIntelligence(
                "Acting saves £18,250 and recovers 4.75 hours of depot capacity."));

        var result = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);

        Assert.False(result.Verification.NarrativeAccepted);
        Assert.Equal(2, result.Verification.UnsupportedNumbers);
        Assert.Equal("deterministic", result.Verification.NarrativeSource);
        Assert.Equal(result.Recommendation.DeterministicSummary, result.ExecutiveSummary);
        Assert.DoesNotContain("18,250", result.ExecutiveSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_language_model_does_not_take_the_decision_down()
    {
        var service = TestHarness.BuildDecisionService(new TestHarness.FailingIntelligence());

        var result = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);

        Assert.Equal("plan-b", result.Recommendation.RecommendedPlanId);
        Assert.Equal(8, result.Verification.VerifiedClaims);
        Assert.Equal(result.Recommendation.DeterministicSummary, result.ExecutiveSummary);
    }

    [Fact]
    public async Task A_failing_language_model_still_allows_a_challenge_to_run()
    {
        var service = TestHarness.BuildDecisionService(new TestHarness.FailingIntelligence());

        var result = await service.ChallengeAsync(
            DemoScenario.Incident, DemoScenario.Plans, Challenge);

        Assert.Equal(AssumptionKind.BufferArrivalDelayMinutes, result.Assumption!.Kind);
        Assert.NotNull(result.Delta);
        Assert.True(result.Delta!.OnTimeChangePp < 0);
    }

    [Fact]
    public async Task The_same_request_twice_gives_the_same_answer()
    {
        var service = TestHarness.BuildDecisionService();

        var first = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);
        var second = await service.DecideAsync(DemoScenario.Incident, DemoScenario.Plans);

        Assert.Equal(first.ExecutiveSummary, second.ExecutiveSummary);
        Assert.Equal(
            first.Verification.Claims.Select(c => c.Value),
            second.Verification.Claims.Select(c => c.Value));
    }
}
