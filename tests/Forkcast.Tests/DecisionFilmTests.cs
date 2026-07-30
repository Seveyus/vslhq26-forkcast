using System.Net.Http.Json;
using System.Text.Json;
using Forkcast.Api.Contracts;
using Forkcast.Core.Demo;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Forkcast.Tests;

/// <summary>
/// The film is a projection of verified state. These tests hold the two properties that claim
/// depends on: a beat may only reference claims the payload carries, and no caption may contain a
/// figure the verifier would reject.
/// </summary>
public class DecisionFilmTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<BriefingResponse> BriefAsync(string query = "") =>
        (await _client.GetFromJsonAsync<BriefingResponse>(
            new Uri($"/api/briefing/export{query}", UriKind.Relative), Json))!;

    private static Claim Rehydrate(ClaimDto dto) => new()
    {
        Id = dto.Id,
        Label = dto.Label,
        Value = dto.Value,
        Unit = dto.Unit,
        SourceField = dto.SourceField,
        CalculationMethod = dto.CalculationMethod,
        SimulationSeed = dto.SimulationSeed,
        TrialCount = dto.TrialCount,
        Verified = dto.Verified
    };

    [Theory]
    [InlineData("", "fleet")]
    [InlineData("?scenario=compute", "compute")]
    public async Task A_beat_can_only_reference_a_claim_the_payload_carries(string query, string domain)
    {
        var brief = await BriefAsync(query);
        var known = brief.Claims.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(domain, brief.DomainKey);
        Assert.NotEmpty(brief.Claims);

        foreach (var beat in brief.Beats)
        {
            foreach (var id in beat.ClaimIds)
            {
                Assert.Contains(id, known);
            }
        }
    }

    [Fact]
    public async Task The_payload_carries_the_claims_it_references()
    {
        var brief = await BriefAsync();

        // Self-containment is the point: a renderer resolves every reference without a second call.
        Assert.All(brief.Claims, claim => Assert.False(string.IsNullOrWhiteSpace(claim.DisplayValue)));
        Assert.All(brief.Claims, claim => Assert.True(claim.Verified));
        Assert.Equal(brief.VerifiedClaims, brief.Claims.Count(c => c.Verified));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?scenario=compute")]
    public async Task No_caption_carries_a_figure_the_verifier_would_reject(string query)
    {
        var brief = await BriefAsync(query);
        var claims = brief.Claims.Select(Rehydrate).ToList();
        var incident = brief.DomainKey == "compute" ? ComputeScenario.Incident : DemoScenario.Incident;
        var context = VerificationContext.FromIncident(incident, SimulationOptions.Default);
        var verifier = new ClaimVerifier();

        foreach (var beat in brief.Beats)
        {
            var findings = verifier.FindUnsupportedNumbers(beat.Caption, claims, context);
            Assert.True(
                findings.Count == 0,
                $"Beat '{beat.Id}' caption carries unsupported "
                + $"{string.Join(", ", findings.Select(f => f.Token))}: {beat.Caption}");
        }
    }

    [Fact]
    public async Task A_counterfactual_brief_differs_from_the_baseline_where_it_should()
    {
        var baseline = await BriefAsync();
        var challenged = await BriefAsync(
            "?question=What%20happens%20if%20the%20temporary%20battery%20arrives%20one%20hour%20late%3F");

        Assert.Null(baseline.CounterfactualLabel);
        Assert.NotNull(challenged.CounterfactualLabel);

        Assert.DoesNotContain(baseline.Beats, b => b.Kind == "counterfactual");
        Assert.Contains(challenged.Beats, b => b.Kind == "counterfactual");

        // The situation is unchanged; the recommended plan's numbers are not.
        Assert.Equal(baseline.Situation, challenged.Situation);
        var before = baseline.Plans.Single(p => p.Recommended);
        var after = challenged.Plans.Single(p => p.Recommended);
        Assert.True(after.OnTimePct < before.OnTimePct);
        Assert.True(after.AtRiskCount > before.AtRiskCount);
    }

    [Fact]
    public async Task Beats_tile_the_timeline_with_no_gap_or_overlap()
    {
        var brief = await BriefAsync();

        var clock = 0.0;
        foreach (var beat in brief.Beats)
        {
            Assert.Equal(clock, beat.StartSeconds, 2);
            Assert.True(beat.DurationSeconds > 0, $"Beat '{beat.Id}' has no duration.");
            clock += beat.DurationSeconds;
        }

        Assert.Equal(clock, brief.TotalSeconds, 2);
    }

    [Fact]
    public async Task The_canvas_state_matches_the_incident_it_was_briefed_from()
    {
        var brief = await BriefAsync("?scenario=compute");

        Assert.Equal(ComputeScenario.Incident.ChargePoints.Count, brief.Resources.Count);
        Assert.Equal(
            ComputeScenario.Incident.FailedChargePointCount,
            brief.Resources.Count(r => !r.Operational));
        Assert.All(brief.Plans, plan => Assert.Equal(ComputeScenario.Incident.VehicleCount, plan.Units.Count));
        Assert.All(
            brief.Plans,
            plan => Assert.Equal(
                ComputeScenario.Incident.PriorityVehicleCount,
                plan.Units.Count(u => u.IsPriority)));
        Assert.All(brief.Plans, plan => Assert.All(plan.Units, u => Assert.InRange(u.OnTimeProbability, 0, 1)));
    }

    [Fact]
    public async Task The_brief_carries_no_credential_or_external_endpoint()
    {
        var raw = await _client.GetStringAsync(new Uri("/api/briefing/export", UriKind.Relative));

        Assert.DoesNotContain("api-key", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openai.azure.com", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", raw, StringComparison.OrdinalIgnoreCase);
    }
}
