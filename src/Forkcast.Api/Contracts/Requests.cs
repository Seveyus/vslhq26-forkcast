using System.ComponentModel;

namespace Forkcast.Api.Contracts;

/// <summary>Shared knobs accepted by every endpoint that runs the simulation.</summary>
public abstract record SimulationRequest
{
    /// <summary>Seed for the run. Omit to use the published demo seed.</summary>
    [Description("Simulation seed. Omit to use the published demo seed, 20260728.")]
    public long? Seed { get; init; }

    /// <summary>Number of Monte Carlo trials. Omit to use 500.</summary>
    [Description("Number of Monte Carlo trials between 1 and 2000. Omit to use 500.")]
    public int? TrialCount { get; init; }

    /// <summary>Which shipped scenario to run: "fleet" or "compute". Omit for "fleet".</summary>
    [Description("Scenario key: \"fleet\" or \"compute\". Omit to use the fleet scenario.")]
    public string? Scenario { get; init; }
}

public sealed record ParseIncidentRequest
{
    /// <summary>The incident described in the operator's own words.</summary>
    [Description("The incident described in plain language.")]
    public string Narrative { get; init; } = string.Empty;

    /// <summary>Which site template to read it against: "fleet" or "compute".</summary>
    [Description("Scenario key whose site template fills anything the text does not state.")]
    public string? Scenario { get; init; }
}

public sealed record RunSimulationRequest : SimulationRequest
{
    /// <summary>Incident text. Omit to run the preloaded demonstration incident.</summary>
    [Description("Incident text. Omit to run the preloaded demonstration incident.")]
    public string? Narrative { get; init; }
}

public sealed record VerificationProbeRequest : SimulationRequest
{
    [Description("Incident text. Omit to probe against the preloaded demonstration incident.")]
    public string? Narrative { get; init; }

    /// <summary>The paragraph to submit to the verifier.</summary>
    [Description("A paragraph of prose to submit to the claim verifier.")]
    public string Submitted { get; init; } = string.Empty;
}

public sealed record ChallengeRequest : SimulationRequest
{
    [Description("Incident text. Omit to run the preloaded demonstration incident.")]
    public string? Narrative { get; init; }

    /// <summary>The assumption being challenged, e.g. "what if the battery arrives an hour late".</summary>
    [Description("The assumption to challenge in plain language.")]
    public string Question { get; init; } = string.Empty;
}
