using System.Net.Http.Json;
using System.Text.Json;
using Forkcast.Api.Contracts;
using Forkcast.Core.Demo;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Forkcast.Tests;

/// <summary>
/// The decision brief is a projection of verified state, not a template with numbers poured in.
/// </summary>
public class BriefingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    private Task<BriefingResponse?> ExportAsync(string query) =>
        _client.GetFromJsonAsync<BriefingResponse>(new Uri(query, UriKind.Relative), Json);

    [Fact]
    public async Task A_brief_carries_timed_beats_and_the_canvas_state()
    {
        var brief = await ExportAsync("/api/briefing/export");

        Assert.NotNull(brief);
        Assert.Equal("fleet", brief!.DomainKey);
        Assert.Equal(8, brief.VerifiedClaims);
        Assert.Equal(0, brief.UnsupportedNumbers);
        Assert.True(brief.TotalSeconds > 60);
        Assert.Null(brief.CounterfactualLabel);

        // Beats run back to back with no gap and no overlap.
        var clock = 0.0;
        foreach (var beat in brief.Beats)
        {
            Assert.Equal(clock, beat.StartSeconds, 2);
            Assert.True(beat.DurationSeconds > 0);
            clock += beat.DurationSeconds;
        }

        Assert.Equal(2, brief.Plans.Count);
        Assert.All(brief.Plans, plan => Assert.Equal(20, plan.Units.Count));
        Assert.Contains(brief.Resources, r => !r.Operational);
    }

    /// <summary>
    /// The point of the export: it cannot introduce a figure the claim set does not carry.
    /// </summary>
    [Fact]
    public async Task Every_number_in_every_caption_survives_the_verifier()
    {
        var brief = await ExportAsync("/api/briefing/export");
        var decision = await _client.GetFromJsonAsync<DecisionResponse>(
            new Uri("/api/demo/result", UriKind.Relative), Json);

        Assert.NotNull(brief);
        Assert.NotNull(decision);

        var claims = decision!.Verification.Claims
            .Select(c => new Core.Verification.Claim
            {
                Id = c.Id,
                Label = c.Label,
                Value = c.Value,
                Unit = c.Unit,
                SourceField = c.SourceField,
                CalculationMethod = c.CalculationMethod,
                SimulationSeed = c.SimulationSeed,
                TrialCount = c.TrialCount,
                Verified = c.Verified
            })
            .ToList();

        var verifier = new Core.Verification.ClaimVerifier();
        var context = Core.Verification.VerificationContext.FromIncident(
            DemoScenario.Incident, Core.Simulation.SimulationOptions.Default);

        foreach (var beat in brief!.Beats)
        {
            var findings = verifier.FindUnsupportedNumbers(beat.Caption, claims, context);
            Assert.Empty(findings);
        }
    }

    [Fact]
    public async Task A_brief_rewords_itself_for_the_other_domain()
    {
        var fleet = await ExportAsync("/api/briefing/export?scenario=fleet");
        var compute = await ExportAsync("/api/briefing/export?scenario=compute");

        Assert.NotNull(fleet);
        Assert.NotNull(compute);
        Assert.Equal("compute", compute!.DomainKey);
        Assert.NotEqual(fleet!.Title, compute.Title);
        Assert.NotEqual(fleet.Situation, compute.Situation);

        Assert.Contains("jobs", compute.Situation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vehicle", compute.Situation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(24, compute.Plans[0].Units.Count);
    }

    [Fact]
    public async Task A_counterfactual_adds_its_own_beat_with_the_real_before_and_after()
    {
        var brief = await ExportAsync(
            "/api/briefing/export?question=What%20happens%20if%20the%20temporary%20battery%20arrives%20one%20hour%20late%3F");

        Assert.NotNull(brief);
        Assert.NotNull(brief!.CounterfactualLabel);

        var beat = brief.Beats.Single(b => b.Kind == "counterfactual");
        Assert.Contains("86.7%", beat.Caption, StringComparison.Ordinal);
        Assert.Contains("97.2%", beat.Caption, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unusable_question_is_refused_rather_than_briefed()
    {
        var response = await _client.GetAsync(
            new Uri("/api/briefing/export?question=" + new string('x', 600), UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
