using Forkcast.Core.Challenges;
using Forkcast.Core.Demo;

namespace Forkcast.Tests;

public class ChallengeServiceTests
{
    private readonly ChallengeService _challenges = new();

    [Theory]
    [InlineData("What happens if the temporary battery arrives one hour late?", 60)]
    [InlineData("What if the buffer is delayed by 2 hours?", 120)]
    [InlineData("Suppose the towed battery unit slips 90 minutes", 90)]
    [InlineData("battery buffer late", 60)]
    public void Buffer_delays_are_read_with_their_duration(string question, double expectedMinutes)
    {
        var assumption = _challenges.Interpret(question);

        Assert.Equal(AssumptionKind.BufferArrivalDelayMinutes, assumption.Kind);
        Assert.Equal(expectedMinutes, assumption.Value);
        Assert.True(assumption.Recognised);
        Assert.Equal(question, assumption.Question);
    }

    [Theory]
    [InlineData("What if the battery unit is unavailable?")]
    [InlineData("What happens if the buffer never arrives?")]
    [InlineData("Suppose we cannot source the temporary battery")]
    public void An_unavailable_buffer_is_recognised(string question)
    {
        Assert.Equal(AssumptionKind.BufferUnavailable, _challenges.Interpret(question).Kind);
    }

    [Theory]
    [InlineData("What if another charge point goes down?", 1)]
    [InlineData("What happens if two more chargers also fail?", 2)]
    public void Further_outages_are_read_with_their_count(string question, double expected)
    {
        var assumption = _challenges.Interpret(question);

        Assert.Equal(AssumptionKind.AdditionalChargePointOutage, assumption.Kind);
        Assert.Equal(expected, assumption.Value);
    }

    [Fact]
    public void A_repair_of_the_fast_charger_is_recognised()
    {
        Assert.Equal(
            AssumptionKind.FastChargerRepaired,
            _challenges.Interpret("What if the fast charger is repaired overnight?").Kind);
    }

    [Fact]
    public void Earlier_departures_are_recognised()
    {
        var assumption = _challenges.Interpret("What if every route has to depart 45 minutes earlier?");

        Assert.Equal(AssumptionKind.DeadlineEarlierMinutes, assumption.Kind);
        Assert.Equal(45, assumption.Value);
    }

    [Theory]
    [InlineData("Is the moon made of cheese?")]
    [InlineData("Tell me a joke")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unsupported_questions_are_reported_rather_than_guessed(string question)
    {
        var assumption = _challenges.Interpret(question);

        Assert.Equal(AssumptionKind.None, assumption.Kind);
        Assert.False(assumption.Recognised);
        Assert.NotEmpty(assumption.Label);
    }

    [Fact]
    public void Delaying_the_buffer_moves_only_the_arrival_time()
    {
        var assumption = _challenges.Interpret("What if the buffer arrives one hour late?");
        var (incident, plans) = _challenges.Apply(
            DemoScenario.Incident, DemoScenario.Plans, assumption);

        var original = DemoScenario.PlanB.MobileBuffer!;
        var changed = plans.Single(p => p.Id == "plan-b").MobileBuffer!;

        Assert.Same(DemoScenario.Incident, incident);
        Assert.Equal(original.PlannedArrival.AddHours(1), changed.PlannedArrival);
        Assert.Equal(original.StoredEnergyKwh, changed.StoredEnergyKwh);
        Assert.Equal(original.Outlets, changed.Outlets);
        Assert.Null(plans.Single(p => p.Id == "plan-a").MobileBuffer);
    }

    [Fact]
    public void Removing_the_buffer_strips_it_from_every_plan()
    {
        var assumption = _challenges.Interpret("What if the buffer is unavailable?");
        var (_, plans) = _challenges.Apply(DemoScenario.Incident, DemoScenario.Plans, assumption);

        Assert.All(plans, p => Assert.Null(p.MobileBuffer));
    }

    [Fact]
    public void Taking_connectors_offline_reduces_the_operational_count()
    {
        var assumption = _challenges.Interpret("What if two more chargers also fail?");
        var (incident, _) = _challenges.Apply(DemoScenario.Incident, DemoScenario.Plans, assumption);

        Assert.Equal(
            DemoScenario.Incident.OperationalChargePointCount - 2,
            incident.OperationalChargePointCount);
        Assert.Equal(DemoScenario.Incident.ChargePoints.Count, incident.ChargePoints.Count);
        Assert.Contains(incident.ChargePoints, c => c.FaultCode == "WHAT-IF");

        // The connectors the rota names by id must keep their identity.
        Assert.Equal(
            DemoScenario.Incident.ChargePoints.Select(c => c.Id),
            incident.ChargePoints.Select(c => c.Id));
    }

    [Fact]
    public void Bringing_departures_forward_moves_the_whole_fleet_and_the_deadline()
    {
        var assumption = _challenges.Interpret("What if every route has to depart 45 minutes earlier?");
        var (incident, _) = _challenges.Apply(DemoScenario.Incident, DemoScenario.Plans, assumption);

        Assert.Equal(DemoScenario.Incident.DepartureDeadline.AddMinutes(-45), incident.DepartureDeadline);
        foreach (var (before, after) in DemoScenario.Incident.Fleet.Zip(incident.Fleet))
        {
            Assert.Equal(before.ScheduledDeparture.AddMinutes(-45), after.ScheduledDeparture);
        }
    }

    [Fact]
    public void Repairing_the_fast_charger_makes_recovery_certain()
    {
        var assumption = _challenges.Interpret("What if the fast charger is repaired overnight?");
        var (incident, _) = _challenges.Apply(DemoScenario.Incident, DemoScenario.Plans, assumption);

        Assert.Equal(1.0, incident.Constraints.FaultRecoveryProbability);
    }

    [Fact]
    public void An_unrecognised_question_changes_nothing()
    {
        var assumption = _challenges.Interpret("Is the moon made of cheese?");
        var (incident, plans) = _challenges.Apply(
            DemoScenario.Incident, DemoScenario.Plans, assumption);

        Assert.Same(DemoScenario.Incident, incident);
        Assert.Same(DemoScenario.Plans, plans);
    }
}
