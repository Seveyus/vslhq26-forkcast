using Forkcast.Core.Demo;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;
using Forkcast.Core.Simulation;

namespace Forkcast.Tests;

public class SimulationEngineTests
{
    private readonly SimulationEngine _engine = new();

    [Fact]
    public void Same_seed_produces_identical_results()
    {
        var first = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);
        var second = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);

        Assert.Equal(first.OnTimeDeparturePct, second.OnTimeDeparturePct);
        Assert.Equal(first.OnTimeDeparturePctP5, second.OnTimeDeparturePctP5);
        Assert.Equal(first.OnTimeDeparturePctP95, second.OnTimeDeparturePctP95);
        Assert.Equal(first.VehiclesAtRisk, second.VehiclesAtRisk);
        Assert.Equal(first.ExpectedUnmetEnergyKwh, second.ExpectedUnmetEnergyKwh);
        Assert.Equal(first.ExpectedOperationalCostGbp, second.ExpectedOperationalCostGbp);
        Assert.Equal(first.RiskLevel, second.RiskLevel);
        Assert.Equal(first.CriticalConstraint, second.CriticalConstraint);

        foreach (var (a, b) in first.Vehicles.Zip(second.Vehicles))
        {
            Assert.Equal(a.VehicleId, b.VehicleId);
            Assert.Equal(a.OnTimeProbability, b.OnTimeProbability);
            Assert.Equal(a.ExpectedShortfallKwh, b.ExpectedShortfallKwh);
        }
    }

    [Fact]
    public void Different_seeds_produce_a_different_trial_distribution()
    {
        var published = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);
        var alternative = _engine.Run(
            DemoScenario.Incident, DemoScenario.PlanA, new SimulationOptions { Seed = 424242 });

        var perVehicleDiffers = published.Vehicles
            .Zip(alternative.Vehicles)
            .Any(pair => Math.Abs(pair.First.OnTimeProbability - pair.Second.OnTimeProbability) > 1e-9);

        Assert.True(perVehicleDiffers, "A different seed should sample a different set of nights.");

        // The scenario is the same, so the summary statistic should still land nearby.
        Assert.InRange(
            Math.Abs(published.OnTimeDeparturePct - alternative.OnTimeDeparturePct), 0.0, 8.0);
    }

    [Fact]
    public void Acting_beats_doing_nothing_in_the_demo_scenario()
    {
        var baseline = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);
        var alternative = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);

        Assert.True(
            alternative.OnTimeDeparturePct > baseline.OnTimeDeparturePct + 20.0,
            $"Expected a decisive improvement, got {baseline.OnTimeDeparturePct} to {alternative.OnTimeDeparturePct}.");
        Assert.True(alternative.VehiclesAtRisk < baseline.VehiclesAtRisk);
        Assert.True(alternative.ExpectedUnmetEnergyKwh < baseline.ExpectedUnmetEnergyKwh);
        Assert.True(alternative.ExpectedInterventionCostGbp > baseline.ExpectedInterventionCostGbp);
    }

    /// <summary>
    /// Pins the published demo figures. Tuning the model is allowed; changing the numbers on the
    /// README and in the demo video without noticing is not.
    /// </summary>
    [Fact]
    public void Published_demo_figures_hold()
    {
        var baseline = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);
        var alternative = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);

        Assert.InRange(baseline.OnTimeDeparturePct, 58.0, 64.0);
        Assert.Equal(9, baseline.VehiclesAtRisk);
        Assert.Equal(RiskLevel.High, baseline.RiskLevel);
        Assert.Equal(0.0, baseline.ExpectedInterventionCostGbp);

        Assert.InRange(alternative.OnTimeDeparturePct, 95.0, 99.0);
        Assert.Equal(1, alternative.VehiclesAtRisk);
        Assert.Equal(RiskLevel.Low, alternative.RiskLevel);
        Assert.InRange(alternative.ExpectedInterventionCostGbp, 360.0, 395.0);
    }

    [Fact]
    public void Priority_routes_are_fully_covered_by_the_recommended_plan()
    {
        var alternative = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);

        Assert.Equal(100.0, alternative.PriorityOnTimeDeparturePct);
        Assert.DoesNotContain(alternative.Vehicles, v => v.IsPriorityRoute && v.IsAtRisk);
    }

    [Fact]
    public void Only_the_buffer_plan_draws_buffer_energy()
    {
        var baseline = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);
        var alternative = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);

        Assert.Equal(0.0, baseline.ExpectedBufferEnergyKwh);
        Assert.True(alternative.ExpectedBufferEnergyKwh > 0.0);
        Assert.True(
            alternative.ExpectedBufferEnergyKwh <= DemoScenario.PlanB.MobileBuffer!.StoredEnergyKwh,
            "The buffer cannot deliver more than it stores.");
    }

    [Fact]
    public void At_risk_count_matches_the_stated_threshold()
    {
        var options = new SimulationOptions { AtRiskProbabilityThreshold = 0.9 };
        var outcome = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA, options);

        var expected = outcome.Vehicles.Count(v => v.OnTimeProbability < 0.9);

        Assert.Equal(expected, outcome.VehiclesAtRisk);
        Assert.All(outcome.Vehicles, v => Assert.Equal(v.OnTimeProbability < 0.9, v.IsAtRisk));
    }

    [Fact]
    public void Every_vehicle_probability_is_a_probability()
    {
        var outcome = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);

        Assert.Equal(DemoScenario.Incident.VehicleCount, outcome.Vehicles.Count);
        Assert.All(outcome.Vehicles, v => Assert.InRange(v.OnTimeProbability, 0.0, 1.0));
        Assert.All(outcome.Vehicles, v => Assert.True(v.ExpectedShortfallKwh >= 0.0));
    }

    [Fact]
    public void Confidence_bounds_bracket_the_mean()
    {
        var outcome = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);

        Assert.True(outcome.OnTimeDeparturePctP5 <= outcome.OnTimeDeparturePct);
        Assert.True(outcome.OnTimeDeparturePct <= outcome.OnTimeDeparturePctP95);
    }

    [Fact]
    public void Load_curve_spans_the_charging_window()
    {
        var outcome = _engine.Run(DemoScenario.Incident, DemoScenario.PlanB);
        var incident = DemoScenario.Incident;

        Assert.NotEmpty(outcome.LoadCurve);
        Assert.Equal(incident.DetectedAt, outcome.LoadCurve[0].At);
        Assert.True(outcome.LoadCurve[^1].At <= incident.DepartureDeadline);
        Assert.True(outcome.LoadCurve[^1].At > incident.DepartureDeadline.AddMinutes(-30));
        Assert.All(outcome.LoadCurve, s => Assert.True(s.GridPowerKw >= 0.0));
    }

    [Fact]
    public void Losing_more_connectors_never_improves_the_outcome()
    {
        var incident = DemoScenario.Incident;
        var degraded = incident with
        {
            ChargePoints = incident.ChargePoints
                .Select((c, i) => i >= 6 ? c with { IsOperational = false } : c)
                .ToList()
        };

        var healthy = _engine.Run(incident, DemoScenario.PlanA);
        var broken = _engine.Run(degraded, DemoScenario.PlanA);

        Assert.True(broken.OnTimeDeparturePct <= healthy.OnTimeDeparturePct);
    }

    [Fact]
    public void A_fleet_that_needs_no_charge_all_departs_on_time()
    {
        var incident = DemoScenario.Incident;
        var charged = incident with
        {
            Fleet = incident.Fleet
                .Select(v => v with { InitialStateOfChargePct = v.RequiredStateOfChargePct })
                .ToList()
        };

        var outcome = _engine.Run(charged, DemoScenario.PlanA);

        Assert.Equal(100.0, outcome.OnTimeDeparturePct);
        Assert.Equal(0, outcome.VehiclesAtRisk);
        Assert.Equal(0.0, outcome.ExpectedUnmetEnergyKwh);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50_000)]
    public void Unusable_trial_counts_are_rejected(int trialCount)
    {
        var options = new SimulationOptions { TrialCount = trialCount };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _engine.Run(DemoScenario.Incident, DemoScenario.PlanA, options));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Run(null!, DemoScenario.PlanA));
        Assert.Throws<ArgumentNullException>(() => _engine.Run(DemoScenario.Incident, null!));
    }

    [Fact]
    public void Seed_and_trial_count_are_reported_on_the_outcome()
    {
        var options = new SimulationOptions { Seed = 123, TrialCount = 64 };
        var outcome = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA, options);

        Assert.Equal(123, outcome.Seed);
        Assert.Equal(64, outcome.TrialCount);
    }

    [Fact]
    public void Charging_to_full_wastes_connector_time_that_charging_to_requirement_does_not()
    {
        var thrifty = DemoScenario.PlanA with
        {
            ChargeTargetPolicy = ChargeTargetPolicy.RouteRequirementPlusMargin,
            ChargeMarginPct = 3.0
        };

        var wasteful = _engine.Run(DemoScenario.Incident, DemoScenario.PlanA);
        var careful = _engine.Run(DemoScenario.Incident, thrifty);

        Assert.True(
            careful.OnTimeDeparturePct > wasteful.OnTimeDeparturePct,
            "Stopping at the route requirement should free connectors for the vehicles behind.");
    }

    [Fact]
    public void The_demo_scenario_is_internally_consistent()
    {
        var incident = DemoScenario.Incident;

        Assert.Equal(20, incident.VehicleCount);
        Assert.Equal(8, incident.OperationalChargePointCount);
        Assert.Equal(1, incident.FailedChargePointCount);
        Assert.Equal(6, incident.PriorityVehicleCount);
        Assert.Equal(24.0, incident.Fleet.Min(v => v.InitialStateOfChargePct));
        Assert.Equal(71.0, incident.Fleet.Max(v => v.InitialStateOfChargePct));
        Assert.Equal(78.0, incident.Fleet.Min(v => v.RequiredStateOfChargePct));
        Assert.Equal(92.0, incident.Fleet.Max(v => v.RequiredStateOfChargePct));
        Assert.True(incident.DepartureDeadline > incident.DetectedAt);
        Assert.All(incident.Fleet, v => Assert.True(v.ScheduledDeparture <= incident.DepartureDeadline));
        Assert.All(
            incident.Fleet,
            v => Assert.Contains(incident.ChargePoints, c => c.Id == v.RosteredChargePointId));

        // The tariff must cover the whole charging window with no gaps.
        Assert.Equal(incident.DetectedAt, incident.Tariff[0].From);
        Assert.Equal(incident.DepartureDeadline, incident.Tariff[^1].To);
        foreach (var (earlier, later) in incident.Tariff.Zip(incident.Tariff.Skip(1)))
        {
            Assert.Equal(earlier.To, later.From);
        }
    }

    [Fact]
    public void Tariff_pricing_is_time_weighted_across_bands()
    {
        var incident = DemoScenario.Incident;
        var overnight = incident.Tariff.Single(t => t.Label == "Overnight");
        var shoulder = incident.Tariff.Single(t => t.Label == "Shoulder");

        var midOvernight = incident.AveragePricePerKwh(
            overnight.From.AddMinutes(30), overnight.From.AddMinutes(90));
        Assert.Equal(overnight.PricePerKwhGbp, midOvernight, 6);

        var straddling = incident.AveragePricePerKwh(
            shoulder.To.AddMinutes(-30), overnight.From.AddMinutes(30));
        Assert.InRange(straddling, overnight.PricePerKwhGbp, shoulder.PricePerKwhGbp);
    }
}
