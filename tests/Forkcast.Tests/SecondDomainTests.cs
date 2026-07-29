using System.Net.Http.Json;
using System.Text.Json;
using Forkcast.Api.Contracts;
using Forkcast.Core.Challenges;
using Forkcast.Core.Demo;
using Forkcast.Core.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Forkcast.Tests;

/// <summary>
/// The generality claim, tested rather than asserted.
/// </summary>
/// <remarks>
/// Forkcast says its engine is not fleet-shaped. The only way to hold that claim honest is to run
/// a domain with no vehicles in it through exactly the same code and check that the decision, the
/// claims and the what-if all still work — and that the wording follows the domain rather than the
/// engine's first industry.
/// </remarks>
public class SecondDomainTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly SimulationEngine _engine = new();

    public SecondDomainTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _client = factory.CreateClient();
    }

    [Fact]
    public void Two_domains_ship_and_they_share_no_vocabulary()
    {
        Assert.Equal(2, ScenarioCatalog.All.Count);

        var fleet = DemoScenario.Vocabulary;
        var compute = ComputeScenario.Vocabulary;

        Assert.NotEqual(fleet.UnitPlural, compute.UnitPlural);
        Assert.NotEqual(fleet.ResourcePlural, compute.ResourcePlural);
        Assert.NotEqual(fleet.LevelUnit, compute.LevelUnit);
        Assert.NotEqual(fleet.DeadlineNoun, compute.DeadlineNoun);
        Assert.NotEqual(fleet.OnTimeMetricLabel, compute.OnTimeMetricLabel);
        Assert.NotEqual(fleet.BufferLabel, compute.BufferLabel);
        Assert.NotEqual(fleet.CapacityPoolLabel, compute.CapacityPoolLabel);
    }

    [Fact]
    public void The_compute_incident_is_internally_consistent()
    {
        var incident = ComputeScenario.Incident;

        Assert.Equal(24, incident.VehicleCount);
        Assert.Equal(10, incident.OperationalChargePointCount);
        Assert.Equal(2, incident.FailedChargePointCount);
        Assert.Equal(7, incident.PriorityVehicleCount);
        Assert.Equal(22.0, incident.Fleet.Min(v => v.InitialStateOfChargePct));
        Assert.Equal(68.0, incident.Fleet.Max(v => v.InitialStateOfChargePct));
        Assert.True(incident.DepartureDeadline > incident.DetectedAt);
        Assert.All(incident.Fleet, v => Assert.True(v.ScheduledDeparture <= incident.DepartureDeadline));
        Assert.All(
            incident.Fleet,
            v => Assert.Contains(incident.ChargePoints, c => c.Id == v.RosteredChargePointId));

        Assert.Equal(incident.DetectedAt, incident.Tariff[0].From);
        Assert.Equal(incident.DepartureDeadline, incident.Tariff[^1].To);
    }

    /// <summary>
    /// The same engine instance, unmodified, decides an incident with no vehicles in it.
    /// </summary>
    [Fact]
    public void Acting_beats_doing_nothing_in_the_compute_scenario_too()
    {
        var baseline = _engine.Run(ComputeScenario.Incident, ComputeScenario.PlanA);
        var alternative = _engine.Run(ComputeScenario.Incident, ComputeScenario.PlanB);

        Assert.True(
            alternative.OnTimeDeparturePct > baseline.OnTimeDeparturePct + 20.0,
            $"Expected a decisive improvement, got {baseline.OnTimeDeparturePct} to {alternative.OnTimeDeparturePct}.");
        Assert.True(alternative.VehiclesAtRisk < baseline.VehiclesAtRisk);
        Assert.Equal(100.0, alternative.PriorityOnTimeDeparturePct);
    }

    /// <summary>Pins the compute figures, and that they are not the fleet's figures relabelled.</summary>
    [Fact]
    public void Published_compute_figures_hold_and_differ_from_the_fleet()
    {
        var baseline = _engine.Run(ComputeScenario.Incident, ComputeScenario.PlanA);
        var alternative = _engine.Run(ComputeScenario.Incident, ComputeScenario.PlanB);

        Assert.InRange(baseline.OnTimeDeparturePct, 55.0, 61.0);
        Assert.InRange(alternative.OnTimeDeparturePct, 92.0, 97.0);

        var fleetBaseline = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);
        var fleetAlternative = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);

        Assert.NotEqual(fleetBaseline.OnTimeDeparturePct, baseline.OnTimeDeparturePct);
        Assert.NotEqual(fleetAlternative.OnTimeDeparturePct, alternative.OnTimeDeparturePct);
    }

    /// <summary>
    /// The engine's own strings follow the domain. This is what stops "generalises" being a
    /// README adjective.
    /// </summary>
    [Fact]
    public void The_critical_constraint_speaks_the_domain_language()
    {
        var compute = _engine.Run(ComputeScenario.Incident, ComputeScenario.PlanA);
        var fleet = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);

        Assert.DoesNotContain("vehicle", compute.CriticalConstraint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("charge point", compute.CriticalConstraint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("job", compute.CriticalConstraint, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("vehicle", fleet.CriticalConstraint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("job", fleet.CriticalConstraint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_compute_decision_is_fully_verified_over_the_api()
    {
        var run = await _client.GetFromJsonAsync<DecisionResponse>(
            new Uri("/api/demo/result?scenario=compute", UriKind.Relative), Json);

        Assert.NotNull(run);
        Assert.Equal("compute", run!.Incident.Vocabulary.DomainKey);
        Assert.Equal(24, run.Incident.VehicleCount);
        Assert.Equal("plan-b", run.Recommendation.RecommendedPlanId);
        Assert.Equal(8, run.Verification.VerifiedClaims);
        Assert.Equal(0, run.Verification.UnsupportedNumbers);

        // Claim labels, not just outcome numbers, follow the domain.
        Assert.Contains(run.Verification.Claims, c => c.Label.Contains("jobs at risk", StringComparison.Ordinal));
        Assert.Contains(run.Verification.Claims, c => c.Unit == "GPU-hours");
        Assert.DoesNotContain(run.Verification.Claims, c => c.Label.Contains("vehicle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_what_if_reruns_in_the_compute_domain_and_is_worded_for_it()
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/simulations/challenge", UriKind.Relative),
            new
            {
                scenario = "compute",
                question = "What happens if the burst capacity comes online an hour late?"
            },
            Json);

        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<DecisionResponse>(Json))!;

        Assert.NotNull(result.Assumption);
        Assert.Equal(nameof(AssumptionKind.BufferArrivalDelayMinutes), result.Assumption!.Kind);
        Assert.Contains("burst capacity", result.Assumption.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("battery", result.Assumption.Label, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(result.Delta);
        Assert.True(result.Delta!.OnTimeChangePp < 0);
        Assert.Contains("on-time completions", result.Delta.Summary, StringComparison.Ordinal);
        Assert.Contains("jobs at risk", result.Delta.Summary, StringComparison.Ordinal);
        Assert.Equal(0, result.Verification.UnsupportedNumbers);
    }

    [Fact]
    public async Task The_scenario_listing_advertises_both_domains()
    {
        var scenarios = await _client.GetFromJsonAsync<List<ScenarioSummaryDto>>(
            new Uri("/api/scenarios", UriKind.Relative), Json);

        Assert.NotNull(scenarios);
        Assert.Equal(2, scenarios!.Count);
        Assert.Contains(scenarios, s => s.Key == "fleet");
        Assert.Contains(scenarios, s => s.Key == "compute");
        Assert.All(scenarios, s => Assert.False(string.IsNullOrWhiteSpace(s.DomainLabel)));
        Assert.All(scenarios, s => Assert.False(string.IsNullOrWhiteSpace(s.SuggestedChallenge)));
    }

    [Fact]
    public async Task The_compute_probe_examples_behave_as_their_labels_promise()
    {
        var demo = await _client.GetFromJsonAsync<DemoIncidentResponse>(
            new Uri("/api/demo/incident?scenario=compute", UriKind.Relative), Json);

        Assert.NotNull(demo);
        Assert.Equal("compute", demo!.ScenarioKey);
        Assert.Equal(4, demo.ExampleProbes.Count);

        foreach (var example in demo.ExampleProbes)
        {
            var response = await _client.PostAsJsonAsync(
                new Uri("/api/verification/probe", UriKind.Relative),
                new { scenario = "compute", submitted = example.Narrative },
                Json);
            response.EnsureSuccessStatusCode();

            var probe = (await response.Content.ReadFromJsonAsync<VerificationProbeResponse>(Json))!;
            var shouldPass = example.Label.Contains("passes", StringComparison.OrdinalIgnoreCase);

            Assert.Equal(shouldPass, probe.Accepted);
        }
    }

    [Fact]
    public async Task An_unknown_scenario_key_falls_back_rather_than_failing()
    {
        var demo = await _client.GetFromJsonAsync<DemoIncidentResponse>(
            new Uri("/api/demo/incident?scenario=nonsense", UriKind.Relative), Json);

        Assert.NotNull(demo);
        Assert.Equal("fleet", demo!.ScenarioKey);
    }
}
