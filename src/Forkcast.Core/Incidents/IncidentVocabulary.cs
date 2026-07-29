namespace Forkcast.Core.Incidents;

/// <summary>
/// The words a domain uses for the things the engine reasons about.
/// </summary>
/// <remarks>
/// <para>
/// The engine models one shape: a queue of units, each needing a quantity delivered before its
/// own deadline, competing for resources whose combined throughput is capped. An electric
/// delivery depot is one instance of that shape. A compute cluster with a reporting cut-off is
/// another, and neither is more native to the engine than the other.
/// </para>
/// <para>
/// Keeping the nouns here rather than in the code is what makes that claim checkable instead of
/// aspirational: every user-visible string the engine, the claim builder and the recommendation
/// produce is composed from these labels, so adding a domain is a matter of supplying data, not
/// of editing the decision logic.
/// </para>
/// </remarks>
public sealed record IncidentVocabulary
{
    /// <summary>Stable key used to select the scenario, e.g. "fleet".</summary>
    public required string DomainKey { get; init; }

    /// <summary>Short name of the operational setting, e.g. "Electric delivery depot".</summary>
    public required string DomainLabel { get; init; }

    /// <summary>The thing that has to be ready in time: "vehicle", "job".</summary>
    public required string UnitSingular { get; init; }

    public required string UnitPlural { get; init; }

    /// <summary>What the units compete for: "charge point", "GPU node".</summary>
    public required string ResourceSingular { get; init; }

    public required string ResourcePlural { get; init; }

    /// <summary>What the resources deliver, as a connector-level noun: "connector", "worker slot".</summary>
    public required string ConnectorNoun { get; init; }

    /// <summary>Unit of the quantity being delivered: "kWh", "GPU-hours".</summary>
    public required string LevelUnit { get; init; }

    /// <summary>Unit of throughput: "kW", "GPU-hours per hour".</summary>
    public required string RateUnit { get; init; }

    /// <summary>What a unit does at its deadline: "departure", "completion".</summary>
    public required string DeadlineNoun { get; init; }

    /// <summary>Adjective phrase for meeting the deadline: "on-time departures".</summary>
    public required string OnTimeMetricLabel { get; init; }

    /// <summary>What the units that cannot wait are called: "priority route".</summary>
    public required string PriorityLabelSingular { get; init; }

    public required string PriorityLabelPlural { get; init; }

    /// <summary>The shared throughput cap: "AC array capacity".</summary>
    public required string CapacityPoolLabel { get; init; }

    /// <summary>The temporary extra capacity a plan can bring in: "towed battery unit".</summary>
    public required string BufferLabel { get; init; }

    /// <summary>The quantity that can still be missing at the deadline: "unmet energy".</summary>
    public required string ShortfallLabel { get; init; }

    public string CurrencySymbol { get; init; } = "£";

    /// <summary>Pluralises the unit noun for a count.</summary>
    public string Units(double count) => Math.Abs(count - 1) < 0.5 ? UnitSingular : UnitPlural;

    /// <summary>Pluralises the resource noun for a count.</summary>
    public string Resources(double count) => Math.Abs(count - 1) < 0.5 ? ResourceSingular : ResourcePlural;
}
