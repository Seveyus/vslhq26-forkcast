using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;

namespace Forkcast.Core.Demo;

/// <summary>One worked incident: its text, its structured form, and the two responses to compare.</summary>
public sealed record Scenario
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    /// <summary>One line naming the operational setting, for a scenario picker.</summary>
    public required string DomainLabel { get; init; }

    public required string Narrative { get; init; }

    public required Incident Incident { get; init; }

    public required IReadOnlyList<ResponsePlan> Plans { get; init; }

    public required string SuggestedChallenge { get; init; }

    public required IReadOnlyList<string> ExampleChallenges { get; init; }

    /// <summary>Rebuilds the plans against a modified incident, preserving call-out lead times.</summary>
    public required Func<Incident, IReadOnlyList<ResponsePlan>> PlansFor { get; init; }
}

/// <summary>
/// The scenarios Forkcast ships with.
/// </summary>
/// <remarks>
/// Two domains, one engine. An electric delivery depot and a GPU compute hall share no vocabulary,
/// no units and no failure mode, and neither required a line of change in the simulation, the
/// comparison, the claim layer or the recommendation. That is the whole argument for the engine
/// being general rather than fleet-shaped, and it is why this catalog has more than one entry.
/// </remarks>
public static class ScenarioCatalog
{
    public static Scenario Fleet { get; } = new()
    {
        Key = "fleet",
        Title = DemoScenario.Incident.Title,
        DomainLabel = DemoScenario.Vocabulary.DomainLabel,
        Narrative = DemoScenario.NarrativeText,
        Incident = DemoScenario.Incident,
        Plans = DemoScenario.Plans,
        SuggestedChallenge = "What happens if the temporary battery arrives one hour late?",
        ExampleChallenges =
        [
            "What happens if the temporary battery arrives one hour late?",
            "What if the battery unit cannot be sourced at all?",
            "What if another charge point goes down?",
            "What if the fast charger is repaired overnight?",
            "What if every route has to depart 45 minutes earlier?"
        ],
        PlansFor = DemoScenario.PlansFor
    };

    public static Scenario Compute { get; } = new()
    {
        Key = "compute",
        Title = ComputeScenario.Incident.Title,
        DomainLabel = ComputeScenario.Vocabulary.DomainLabel,
        Narrative = ComputeScenario.NarrativeText,
        Incident = ComputeScenario.Incident,
        Plans = ComputeScenario.Plans,
        SuggestedChallenge = "What happens if the burst capacity comes online an hour late?",
        ExampleChallenges =
        [
            "What happens if the burst capacity comes online an hour late?",
            "What if the burst capacity cannot be sourced at all?",
            "What if another GPU node goes down?",
            "What if the failed racks are restored overnight?",
            "What if the reporting cut-off is brought forward 45 minutes?"
        ],
        PlansFor = ComputeScenario.PlansFor
    };

    public static IReadOnlyList<Scenario> All { get; } = [Fleet, Compute];

    public const string DefaultKey = "fleet";

    /// <summary>Resolves a scenario key, falling back to the default rather than failing.</summary>
    public static Scenario Resolve(string? key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? Fleet;

    public static bool IsKnown(string? key) =>
        string.IsNullOrWhiteSpace(key)
        || All.Any(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
}
