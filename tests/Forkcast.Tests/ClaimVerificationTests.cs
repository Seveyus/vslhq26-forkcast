using Forkcast.Core.Comparison;
using Forkcast.Core.Demo;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;

namespace Forkcast.Tests;

public class ClaimVerificationTests
{
    private readonly SimulationEngine _engine = new();
    private readonly ComparisonService _comparisons = new();
    private readonly ClaimSetBuilder _claimSets = new();
    private readonly ClaimVerifier _verifier = new();

    private PlanComparison BuildComparison() => _comparisons.Compare(
        _engine.Run(DemoScenario.Incident, DemoScenario.PlanA),
        _engine.Run(DemoScenario.Incident, DemoScenario.PlanB));

    private static VerificationContext Context() =>
        VerificationContext.FromIncident(DemoScenario.Incident, SimulationOptions.Default);

    [Fact]
    public void The_demo_produces_eight_verified_claims()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        Assert.Equal(8, claims.Count);
        Assert.All(claims, c => Assert.True(c.Verified, $"Claim {c.Id} did not verify."));
        Assert.All(claims, c => Assert.False(double.IsNaN(c.Value)));
        Assert.All(claims, c => Assert.Equal(SimulationOptions.DefaultSeed, c.SimulationSeed));
        Assert.All(claims, c => Assert.Equal(SimulationOptions.DefaultTrialCount, c.TrialCount));
        Assert.Equal(claims.Select(c => c.Id).Distinct().Count(), claims.Count);
    }

    [Fact]
    public void Every_claim_still_resolves_to_the_simulation_field_it_names()
    {
        var comparison = BuildComparison();
        var claims = _claimSets.Build(comparison, DemoScenario.Vocabulary);

        foreach (var claim in claims)
        {
            var resolved = ClaimSetBuilder.Resolve(claim.SourceField, comparison);

            Assert.True(resolved.HasValue, $"{claim.SourceField} did not resolve.");
            Assert.Equal(claim.Value, resolved!.Value, 9);
        }
    }

    [Fact]
    public void A_tampered_claim_no_longer_matches_its_source()
    {
        var comparison = BuildComparison();
        var claim = _claimSets.Build(comparison, DemoScenario.Vocabulary).Single(c => c.Id == "alternative-on-time");

        var tampered = claim with { Value = claim.Value + 5.0 };
        var resolved = ClaimSetBuilder.Resolve(tampered.SourceField, comparison);

        Assert.NotEqual(tampered.Value, resolved!.Value, 9);
    }

    [Fact]
    public void An_unknown_source_field_does_not_resolve()
    {
        var comparison = BuildComparison();

        Assert.Null(ClaimSetBuilder.Resolve("alternative.profitMargin", comparison));
        Assert.Null(ClaimSetBuilder.Resolve("nonsense", comparison));
        Assert.Null(ClaimSetBuilder.Resolve("mystery.onTimeDeparturePct", comparison));
    }

    [Fact]
    public void A_narrative_built_from_claim_values_is_accepted()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);
        var onTime = claims.Single(c => c.Id == "alternative-on-time");
        var atRisk = claims.Single(c => c.Id == "alternative-at-risk");

        var narrative = $"Acting lifts on-time departures to {onTime.DisplayValue} and leaves "
                        + $"{atRisk.DisplayValue} vehicle at risk.";

        var result = _verifier.Verify(narrative, "azure-openai", "fallback", claims, Context());

        Assert.True(result.NarrativeAccepted);
        Assert.Equal(0, result.UnsupportedNumbers);
        Assert.Equal("azure-openai", result.NarrativeSource);
        Assert.Equal(narrative, result.Narrative);
    }

    [Fact]
    public void An_invented_number_is_rejected_and_the_narrative_is_replaced()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);
        var narrative = "Acting avoids £12,400 of penalties and recovers 3.5 hours of depot time.";

        var result = _verifier.Verify(
            narrative, "azure-openai", "The deterministic summary.", claims, Context());

        Assert.False(result.NarrativeAccepted);
        Assert.Equal(2, result.UnsupportedNumbers);
        Assert.Equal("The deterministic summary.", result.Narrative);
        Assert.Equal("deterministic", result.NarrativeSource);
        Assert.Contains(result.Unsupported, u => u.Token == "12,400");
        Assert.Contains(result.Unsupported, u => u.Token == "3.5");
        Assert.All(result.Unsupported, u => Assert.NotEmpty(u.Context));
    }

    [Fact]
    public void One_invented_number_discards_the_whole_narrative()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);
        var onTime = claims.Single(c => c.Id == "alternative-on-time");

        var narrative = $"On-time departures reach {onTime.DisplayValue}, saving £4,900.";

        var result = _verifier.Verify(narrative, "azure-openai", "fallback", claims, Context());

        Assert.False(result.NarrativeAccepted);
        Assert.Equal("fallback", result.Narrative);
    }

    [Fact]
    public void Rounded_forms_of_a_claim_are_accepted()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);
        var onTime = claims.Single(c => c.Id == "alternative-on-time");
        var rounded = Math.Round(onTime.Value, 0, MidpointRounding.AwayFromZero);

        var findings = _verifier.FindUnsupportedNumbers(
            $"Roughly {rounded:0}% of vehicles leave on time.", claims, Context());

        Assert.Empty(findings);
    }

    [Fact]
    public void Clock_times_and_vehicle_identifiers_are_not_treated_as_quantities()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        var findings = _verifier.FindUnsupportedNumbers(
            "At 18:40 the fault was raised; EV-04 and CP-09 were affected before the 06:00 deadline.",
            claims,
            Context());

        Assert.Empty(findings);
    }

    [Fact]
    public void Facts_taken_from_the_incident_are_allowed()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        var findings = _verifier.FindUnsupportedNumbers(
            "All 20 vehicles share 8 working charge points; 6 are on priority routes. "
            + "Simulated over 500 nights with seed 20260728.",
            claims,
            Context());

        Assert.Empty(findings);
    }

    [Fact]
    public void An_empty_narrative_falls_back_without_reporting_invented_numbers()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        var result = _verifier.Verify(null, "azure-openai", "fallback", claims, Context());

        Assert.False(result.NarrativeAccepted);
        Assert.Equal(0, result.UnsupportedNumbers);
        Assert.Equal("fallback", result.Narrative);
        Assert.Equal(8, result.VerifiedClaims);
        Assert.True(result.AllClaimsVerified);
    }

    [Fact]
    public void An_empty_allow_list_still_catches_invented_numbers()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        var findings = _verifier.FindUnsupportedNumbers(
            "There are 20 vehicles.", claims, VerificationContext.Empty);

        Assert.Single(findings);
        Assert.Equal("20", findings[0].Token);
    }

    [Fact]
    public void Claims_render_with_their_unit()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        Assert.EndsWith("%", claims.Single(c => c.Id == "alternative-on-time").DisplayValue, StringComparison.Ordinal);
        Assert.StartsWith("£", claims.Single(c => c.Id == "additional-cost").DisplayValue, StringComparison.Ordinal);
        Assert.EndsWith("kWh", claims.Single(c => c.Id == "baseline-unmet-energy").DisplayValue, StringComparison.Ordinal);
        Assert.StartsWith("+", claims.Single(c => c.Id == "on-time-improvement").DisplayValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_claim_explains_how_it_was_calculated()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        Assert.All(claims, c => Assert.False(string.IsNullOrWhiteSpace(c.Label)));
        Assert.All(claims, c => Assert.False(string.IsNullOrWhiteSpace(c.CalculationMethod)));
        Assert.All(claims, c => Assert.Contains(".", c.SourceField, StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_fallback_is_a_programming_error()
    {
        var claims = _claimSets.Build(BuildComparison(), DemoScenario.Vocabulary);

        Assert.Throws<ArgumentException>(
            () => _verifier.Verify("text", "azure-openai", "   ", claims, Context()));
    }
}
