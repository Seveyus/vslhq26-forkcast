using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Forkcast.Api.Contracts;
using Forkcast.Core.Comparison;
using Forkcast.Core.Demo;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Forkcast.Tests;

/// <summary>
/// The verifier offered for inspection rather than asserted: hand it a paragraph and it reports a
/// verdict on every number in it, naming what backs each one.
/// </summary>
public class VerifierProbeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly ClaimVerifier _verifier = new();
    private readonly SimulationEngine _engine = new();
    private readonly ComparisonService _comparisons = new();
    private readonly ClaimSetBuilder _claimSets = new();

    public VerifierProbeTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _client = factory.CreateClient();
    }

    private IReadOnlyList<Claim> DemoClaims() => _claimSets.Build(_comparisons.Compare(
        _engine.Run(DemoScenario.Incident, DemoScenario.PlanA),
        _engine.Run(DemoScenario.Incident, DemoScenario.PlanB)));

    private static VerificationContext Context() =>
        VerificationContext.FromIncident(DemoScenario.Incident, SimulationOptions.Default);

    private async Task<VerificationProbeResponse> ProbeAsync(string submitted)
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/verification/probe", UriKind.Relative), new { submitted }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VerificationProbeResponse>(Json))!;
    }

    [Fact]
    public void Every_number_gets_a_verdict_not_just_the_rejected_ones()
    {
        var claims = DemoClaims();
        var findings = _verifier.AnalyseNumbers(
            "On-time departures reach 97.2% across the 20 vehicles, saving £4,200.",
            claims,
            Context());

        Assert.Equal(3, findings.Count);

        var onTime = findings.Single(f => f.Token == "97.2");
        Assert.True(onTime.Supported);
        Assert.Equal("alternative-on-time", onTime.ClaimId);
        Assert.NotNull(onTime.Reason);

        var fleet = findings.Single(f => f.Token == "20");
        Assert.True(fleet.Supported);
        Assert.Null(fleet.ClaimId);
        Assert.Equal("vehicles in the fleet", fleet.Reason);

        var invented = findings.Single(f => f.Token == "4,200");
        Assert.False(invented.Supported);
        Assert.Null(invented.ClaimId);
        Assert.Null(invented.Reason);
    }

    [Fact]
    public void The_analyser_and_the_rejection_list_agree()
    {
        var claims = DemoClaims();
        const string text = "Saves £4,200 and recovers 14 hours, lifting on-time to 97.2%.";

        var unsupported = _verifier.FindUnsupportedNumbers(text, claims, Context());
        var analysed = _verifier.AnalyseNumbers(text, claims, Context())
            .Where(f => !f.Supported)
            .ToList();

        Assert.Equal(analysed.Count, unsupported.Count);
        Assert.Equal(analysed.Select(f => f.Token), unsupported.Select(u => u.Token));
    }

    [Fact]
    public async Task A_paragraph_with_one_invented_figure_is_rejected_whole()
    {
        var probe = await ProbeAsync(
            "Reprioritising lifts on-time departures to 97.2%, avoiding £4,200 of penalties.");

        Assert.False(probe.Accepted);
        Assert.Equal(1, probe.NumbersUnsupported);
        Assert.Equal("deterministic", probe.DisplayedSource);
        Assert.DoesNotContain("4,200", probe.Displayed, StringComparison.Ordinal);
        Assert.Contains("not backed by any claim", probe.Verdict, StringComparison.Ordinal);

        // The supported figure is still reported as supported — the rejection is about the
        // paragraph, not about that number.
        Assert.Contains(probe.Findings, f => f.Token == "97.2" && f.Supported);
        Assert.Contains(probe.Findings, f => f.Token == "4,200" && !f.Supported);
    }

    [Fact]
    public async Task A_paragraph_built_only_from_claims_survives()
    {
        var probe = await ProbeAsync(
            "Expected on-time departures rise from 60.9% to 97.2%, a gain of 36.3 pp, and "
            + "vehicles at risk fall from 9 to 1 across the 20 in the fleet.");

        Assert.True(probe.Accepted);
        Assert.Equal(0, probe.NumbersUnsupported);
        Assert.Equal("submitted", probe.DisplayedSource);
        Assert.Contains("97.2", probe.Displayed, StringComparison.Ordinal);
        Assert.All(probe.Findings, f => Assert.True(f.Supported));
    }

    [Fact]
    public async Task A_plausible_rounding_is_still_rejected()
    {
        var probe = await ProbeAsync("On-time departures reach 98% with zero vehicles at risk.");

        Assert.False(probe.Accepted);
        Assert.Contains(probe.Findings, f => f.Token == "98" && !f.Supported);
    }

    [Fact]
    public async Task A_paragraph_with_no_numbers_at_all_survives()
    {
        var probe = await ProbeAsync("Bring in the battery unit and reorder the yard queue.");

        Assert.True(probe.Accepted);
        Assert.Equal(0, probe.NumbersFound);
        Assert.Equal("submitted", probe.DisplayedSource);
    }

    [Fact]
    public async Task The_probe_reports_the_seed_it_checked_against()
    {
        var probe = await ProbeAsync("Nothing quantitative here.");

        Assert.Equal(SimulationOptions.DefaultSeed, probe.SimulationSeed);
        Assert.Equal(SimulationOptions.DefaultTrialCount, probe.TrialCount);
        Assert.Equal(8, probe.Claims.Count);
    }

    [Fact]
    public async Task An_empty_submission_is_refused()
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/verification/probe", UriKind.Relative), new { submitted = "   " }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The examples the interface offers have to actually behave as advertised, or the
    /// demonstration teaches the wrong lesson.
    /// </summary>
    [Fact]
    public async Task The_offered_examples_behave_as_their_labels_promise()
    {
        var demo = await _client.GetFromJsonAsync<DemoIncidentResponse>(
            new Uri("/api/demo/incident", UriKind.Relative), Json);

        Assert.NotNull(demo);
        Assert.Equal(4, demo!.ExampleProbes.Count);

        foreach (var example in demo.ExampleProbes)
        {
            var probe = await ProbeAsync(example.Narrative);
            var shouldPass = example.Label.Contains("passes", StringComparison.OrdinalIgnoreCase);

            Assert.Equal(shouldPass, probe.Accepted);
            Assert.NotEmpty(example.Expectation);
        }
    }
}
