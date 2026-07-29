using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Forkcast.Api.Contracts;
using Forkcast.Core.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Forkcast.Tests;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _client = factory.CreateClient();
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _client.GetAsync(new Uri(url, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    [Fact]
    public async Task Health_reports_the_provider_and_the_published_defaults()
    {
        var health = await GetAsync<HealthResponse>("/api/health");

        Assert.Equal("healthy", health.Status);
        Assert.Equal("Forkcast", health.Service);
        Assert.Equal(SimulationOptions.DefaultSeed, health.DefaultSeed);
        Assert.Equal(SimulationOptions.DefaultTrialCount, health.DefaultTrialCount);
        Assert.False(string.IsNullOrWhiteSpace(health.IntelligenceProvider));
    }

    [Fact]
    public async Task The_demo_incident_carries_everything_the_interface_needs()
    {
        var demo = await GetAsync<DemoIncidentResponse>("/api/demo/incident");

        Assert.Equal(20, demo.Incident.VehicleCount);
        Assert.Equal(8, demo.Incident.OperationalChargePointCount);
        Assert.Equal(1, demo.Incident.FailedChargePointCount);
        Assert.Equal(6, demo.Incident.PriorityVehicleCount);
        Assert.Equal(20, demo.Incident.Fleet.Count);
        Assert.Equal(9, demo.Incident.ChargePoints.Count);
        Assert.NotEmpty(demo.Incident.Failures);
        Assert.NotEmpty(demo.Incident.Tariff);
        Assert.Equal(2, demo.Plans.Count);
        Assert.NotEmpty(demo.Narrative);
        Assert.NotEmpty(demo.SuggestedChallenge);
        Assert.Contains(demo.SuggestedChallenge, demo.ExampleChallenges);
    }

    [Fact]
    public async Task The_demo_result_is_fully_verified()
    {
        var result = await GetAsync<DecisionResponse>("/api/demo/result");

        Assert.Equal("plan-b", result.Recommendation.RecommendedPlanId);
        Assert.Equal(8, result.Verification.TotalClaims);
        Assert.Equal(8, result.Verification.VerifiedClaims);
        Assert.Equal(0, result.Verification.UnsupportedNumbers);
        Assert.True(result.Verification.AllClaimsVerified);
        Assert.Equal(SimulationOptions.DefaultSeed, result.Seed);
        Assert.Equal(2, result.Outcomes.Count);
        Assert.NotEmpty(result.ExecutiveSummary);
        Assert.NotEmpty(result.Outcomes[0].LoadCurve);
        Assert.Equal(20, result.Outcomes[0].Vehicles.Count);
        Assert.All(result.Verification.Claims, c => Assert.True(c.Verified));
        Assert.All(result.Verification.Claims, c => Assert.NotEmpty(c.DisplayValue));
    }

    [Fact]
    public async Task Enum_values_travel_as_names_not_numbers()
    {
        var response = await _client.GetAsync(new Uri("/api/demo/result", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"riskLevel\":\"High\"", body, StringComparison.Ordinal);
        Assert.Contains("\"riskLevel\":\"Low\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Running_without_a_narrative_reproduces_the_demo_result()
    {
        var demo = await GetAsync<DecisionResponse>("/api/demo/result");
        var run = await PostAsync<DecisionResponse>("/api/simulations/run", new { });

        Assert.Equal(demo.Outcomes[0].OnTimeDeparturePct, run.Outcomes[0].OnTimeDeparturePct);
        Assert.Equal(demo.Outcomes[1].OnTimeDeparturePct, run.Outcomes[1].OnTimeDeparturePct);
        Assert.Equal(demo.ExecutiveSummary, run.ExecutiveSummary);
    }

    [Fact]
    public async Task A_different_seed_changes_the_run_and_is_reported_on_every_claim()
    {
        var run = await PostAsync<DecisionResponse>("/api/simulations/run", new { seed = 777, trialCount = 200 });

        Assert.Equal(777, run.Seed);
        Assert.Equal(200, run.TrialCount);
        Assert.All(run.Verification.Claims, c => Assert.Equal(777, c.SimulationSeed));
        Assert.All(run.Verification.Claims, c => Assert.Equal(200, c.TrialCount));
    }

    [Fact]
    public async Task A_different_depot_is_read_from_the_narrative_and_simulated()
    {
        const string narrative =
            "At 19:15 two chargers failed at our Leeds hub. 28 vans must leave by 05:30. "
            + "Ten charge points remain available. Batteries range from 18% to 66%. "
            + "Nine vehicles are on priority routes.";

        var parsed = await PostAsync<ParseIncidentResponse>(
            "/api/incidents/parse", new { narrative });

        Assert.Equal(28, parsed.Incident.VehicleCount);
        Assert.Equal(10, parsed.Incident.OperationalChargePointCount);
        Assert.Equal(2, parsed.Incident.FailedChargePointCount);
        Assert.Equal(9, parsed.Incident.PriorityVehicleCount);
        Assert.Equal(18, parsed.Incident.Fleet.Min(v => v.InitialStateOfChargePct));
        Assert.Equal(66, parsed.Incident.Fleet.Max(v => v.InitialStateOfChargePct));
        Assert.Equal(10.25, parsed.Incident.ChargingWindowHours, 2);

        var run = await PostAsync<DecisionResponse>("/api/simulations/run", new { narrative });

        Assert.Equal(28, run.Incident.VehicleCount);
        Assert.Equal(8, run.Verification.VerifiedClaims);
        Assert.Equal(0, run.Verification.UnsupportedNumbers);
    }

    [Fact]
    public async Task The_headline_challenge_returns_a_worse_verified_future()
    {
        var challenge = await PostAsync<DecisionResponse>(
            "/api/simulations/challenge",
            new { question = "What happens if the temporary battery arrives one hour late?" });

        Assert.NotNull(challenge.Assumption);
        Assert.Equal("BufferArrivalDelayMinutes", challenge.Assumption!.Kind);
        Assert.True(challenge.Assumption.Recognised);

        Assert.NotNull(challenge.Delta);
        Assert.True(challenge.Delta!.OnTimeChangePp < 0);
        Assert.True(challenge.Delta.VehiclesAtRisk > challenge.Delta.PreviousVehiclesAtRisk);
        Assert.NotEmpty(challenge.Delta.Summary);
        Assert.Equal(0, challenge.Verification.UnsupportedNumbers);
    }

    [Fact]
    public async Task An_unrecognised_challenge_reports_that_nothing_was_changed()
    {
        var challenge = await PostAsync<DecisionResponse>(
            "/api/simulations/challenge", new { question = "Is the moon made of cheese?" });

        Assert.NotNull(challenge.Assumption);
        Assert.False(challenge.Assumption!.Recognised);
        Assert.Equal("None", challenge.Assumption.Kind);
        Assert.Null(challenge.Delta);
    }

    [Theory]
    [InlineData("/api/simulations/challenge", "{\"question\":\"\"}")]
    [InlineData("/api/simulations/run", "{\"trialCount\":0}")]
    [InlineData("/api/simulations/run", "{\"trialCount\":99999}")]
    [InlineData("/api/incidents/parse", "{\"narrative\":\"   \"}")]
    public async Task Bad_input_returns_problem_details_rather_than_a_stack_trace(string url, string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(new Uri(url, UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("at Forkcast.", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_overlong_narrative_is_refused()
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/simulations/run", UriKind.Relative),
            new { narrative = new string('x', 5000) },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_api_describes_itself()
    {
        var response = await _client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var document = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var paths = document.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/health", out _));
        Assert.True(paths.TryGetProperty("/api/demo/incident", out _));
        Assert.True(paths.TryGetProperty("/api/demo/result", out _));
        Assert.True(paths.TryGetProperty("/api/incidents/parse", out _));
        Assert.True(paths.TryGetProperty("/api/simulations/run", out _));
        Assert.True(paths.TryGetProperty("/api/simulations/challenge", out _));
    }
}
