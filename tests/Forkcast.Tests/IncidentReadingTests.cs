using Forkcast.Core.Ai;
using Forkcast.Core.Challenges;
using Forkcast.Core.Demo;
using Forkcast.Core.Incidents;
using Forkcast.Core.Simulation;

namespace Forkcast.Tests;

public class IncidentReadingTests
{
    private readonly IncidentComposer _composer = new();

    private static DeterministicIntelligence Reader() => new(new ChallengeService());

    [Fact]
    public async Task The_demo_narrative_is_read_into_the_expected_facts()
    {
        var extraction = await Reader().ExtractAsync(DemoScenario.NarrativeText);

        Assert.Equal("deterministic", extraction.Source);
        Assert.Equal(20, extraction.Draft.VehicleCount);
        Assert.Equal(8, extraction.Draft.OperationalChargePointCount);
        Assert.Equal(6, extraction.Draft.PriorityVehicleCount);
        Assert.Equal(24, extraction.Draft.MinInitialStateOfChargePct);
        Assert.Equal(71, extraction.Draft.MaxInitialStateOfChargePct);
        Assert.Equal("18:40", extraction.Draft.DetectedAtLocalTime);
        Assert.Equal("06:00", extraction.Draft.DeadlineLocalTime);
        Assert.NotEmpty(extraction.Notes);
    }

    [Fact]
    public async Task Reading_the_demo_narrative_reproduces_the_published_scenario_exactly()
    {
        var extraction = await Reader().ExtractAsync(DemoScenario.NarrativeText);
        var (incident, adjustments) = _composer.Compose(
            extraction.Draft, DemoScenario.Incident, DemoScenario.NarrativeText);

        Assert.Empty(adjustments);
        Assert.Equal(DemoScenario.Incident.VehicleCount, incident.VehicleCount);
        Assert.Equal(DemoScenario.Incident.DetectedAt, incident.DetectedAt);
        Assert.Equal(DemoScenario.Incident.DepartureDeadline, incident.DepartureDeadline);
        Assert.Same(DemoScenario.Incident.Fleet, incident.Fleet);

        // The published numbers have to survive the round trip through the reader.
        var engine = new SimulationEngine();
        Assert.Equal(
            engine.Run(DemoScenario.Incident, DemoScenario.PlanB).OnTimeDeparturePct,
            engine.Run(incident, DemoScenario.PlanB).OnTimeDeparturePct);
    }

    [Fact]
    public void An_empty_draft_falls_back_to_the_site_template()
    {
        var (incident, adjustments) = _composer.Compose(
            IncidentDraft.Empty, DemoScenario.Incident, string.Empty);

        Assert.Empty(adjustments);
        Assert.Equal(DemoScenario.Incident.VehicleCount, incident.VehicleCount);
        Assert.Equal(DemoScenario.Incident.Narrative, incident.Narrative);
    }

    [Fact]
    public void A_larger_depot_composes_into_a_runnable_incident()
    {
        var draft = new IncidentDraft
        {
            VehicleCount = 32,
            OperationalChargePointCount = 12,
            FailedChargePointCount = 2,
            PriorityVehicleCount = 9,
            MinInitialStateOfChargePct = 18,
            MaxInitialStateOfChargePct = 64
        };

        var (incident, adjustments) = _composer.Compose(draft, DemoScenario.Incident, "Bigger site.");

        Assert.Empty(adjustments);
        Assert.Equal(32, incident.VehicleCount);
        Assert.Equal(12, incident.OperationalChargePointCount);
        Assert.Equal(2, incident.FailedChargePointCount);
        Assert.Equal(9, incident.PriorityVehicleCount);
        Assert.Equal(18, incident.Fleet.Min(v => v.InitialStateOfChargePct));
        Assert.Equal(64, incident.Fleet.Max(v => v.InitialStateOfChargePct));
        Assert.All(
            incident.Fleet,
            v => Assert.Contains(incident.ChargePoints, c => c.Id == v.RosteredChargePointId));
        Assert.All(
            incident.Fleet,
            v => Assert.True(v.ScheduledDeparture <= incident.DepartureDeadline));

        var outcome = new SimulationEngine().Run(incident, DemoScenario.PlanA);
        Assert.InRange(outcome.OnTimeDeparturePct, 0.0, 100.0);
        Assert.Equal(32, outcome.Vehicles.Count);
    }

    [Fact]
    public void Composition_is_reproducible_for_the_same_draft()
    {
        var draft = new IncidentDraft { VehicleCount = 26, PriorityVehicleCount = 7 };

        var (first, _) = _composer.Compose(draft, DemoScenario.Incident, "x");
        var (second, _) = _composer.Compose(draft, DemoScenario.Incident, "x");

        Assert.Equal(
            first.Fleet.Select(v => v.InitialStateOfChargePct),
            second.Fleet.Select(v => v.InitialStateOfChargePct));
    }

    [Fact]
    public void Impossible_counts_are_clamped_and_the_clamp_is_reported()
    {
        var draft = new IncidentDraft { VehicleCount = 5000, OperationalChargePointCount = 0 };

        var (incident, adjustments) = _composer.Compose(draft, DemoScenario.Incident, "x");

        Assert.Contains(adjustments, a => a.Field == "vehicleCount");
        Assert.Contains(adjustments, a => a.Field == "operationalChargePointCount");
        Assert.InRange(incident.VehicleCount, 4, 60);
        Assert.True(incident.OperationalChargePointCount >= 1);
    }

    [Fact]
    public void A_reversed_battery_range_is_corrected_and_reported()
    {
        var draft = new IncidentDraft
        {
            MinInitialStateOfChargePct = 80,
            MaxInitialStateOfChargePct = 20
        };

        var (incident, adjustments) = _composer.Compose(draft, DemoScenario.Incident, "x");

        Assert.Contains(adjustments, a => a.Field == "initialStateOfCharge");
        Assert.True(
            incident.Fleet.Min(v => v.InitialStateOfChargePct)
            <= incident.Fleet.Max(v => v.InitialStateOfChargePct));
    }

    [Fact]
    public void A_deadline_before_the_incident_rolls_to_the_next_day()
    {
        var draft = new IncidentDraft
        {
            DetectedAtLocalTime = "22:00",
            DeadlineLocalTime = "05:00"
        };

        var (incident, _) = _composer.Compose(draft, DemoScenario.Incident, "x");

        Assert.True(incident.DepartureDeadline > incident.DetectedAt);
        Assert.Equal(7.0, (incident.DepartureDeadline - incident.DetectedAt).TotalHours, 3);
        Assert.Equal(incident.DetectedAt, incident.Tariff[0].From);
        Assert.Equal(incident.DepartureDeadline, incident.Tariff[^1].To);
    }

    [Theory]
    [InlineData("twenty vehicles", 20)]
    [InlineData("we have 14 vans", 14)]
    [InlineData("eight delivery trucks", 8)]
    public void Counts_are_read_as_digits_or_words(string text, int expected)
    {
        Assert.Equal(expected, TextFacts.CountBefore(text, "vehicles", "vans", "trucks"));
    }

    [Fact]
    public async Task A_narrative_with_no_facts_reports_what_it_could_not_find()
    {
        var extraction = await Reader().ExtractAsync("Something went wrong at the depot.");

        Assert.Null(extraction.Draft.VehicleCount);
        Assert.Contains(extraction.Notes, n => n.Contains("vehicle count", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_deterministic_reader_declines_to_write_prose()
    {
        var summary = await Reader().WriteExecutiveSummaryAsync(new ExecutiveSummaryRequest
        {
            IncidentTitle = "t",
            RecommendedPlanName = "p",
            RecommendedHeadline = "h",
            DecisionRule = "r",
            CriticalConstraint = "c",
            Claims = [],
            Seed = 1,
            TrialCount = 1
        });

        Assert.Null(summary);
    }
}
