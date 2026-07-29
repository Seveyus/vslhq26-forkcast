using System.Globalization;
using Forkcast.Core.Incidents;

namespace Forkcast.Core.Challenges;

/// <summary>
/// Puts the human wording on a challenged assumption.
/// </summary>
/// <remarks>
/// Deliberately separate from the classifiers. Whether a question was read by a language model or
/// by pattern matching, the kind and the value are all either of them is trusted to produce; the
/// sentence a user reads is composed here, from the incident's own vocabulary. That is why a
/// what-if reads correctly in a domain the classifier has never been told about.
/// </remarks>
public static class AssumptionLabeller
{
    /// <summary>Neutral nouns for callers with no incident in hand.</summary>
    public static IncidentVocabulary Generic { get; } = new()
    {
        DomainKey = "generic",
        DomainLabel = "Operational site",
        UnitSingular = "unit",
        UnitPlural = "units",
        ResourceSingular = "resource",
        ResourcePlural = "resources",
        ConnectorNoun = "connector",
        LevelUnit = "units",
        RateUnit = "units per hour",
        DeadlineNoun = "deadline",
        OnTimeMetricLabel = "on-time completions",
        PriorityLabelSingular = "priority unit",
        PriorityLabelPlural = "priority units",
        CapacityPoolLabel = "shared capacity",
        BufferLabel = "temporary buffer",
        ShortfallLabel = "unmet demand"
    };

    public static string Describe(AssumptionKind kind, double value, IncidentVocabulary? words = null)
    {
        var w = words ?? Generic;

        return kind switch
        {
            AssumptionKind.BufferArrivalDelayMinutes =>
                $"The {w.BufferLabel} arrives {Duration(value)} late",
            AssumptionKind.BufferUnavailable =>
                $"The {w.BufferLabel} cannot be sourced",
            AssumptionKind.AdditionalChargePointOutage =>
                $"A further {value:0} {w.Resources(value)} goes offline",
            AssumptionKind.DeadlineEarlierMinutes =>
                $"Every {w.DeadlineNoun} is brought forward by {Duration(value)}",
            AssumptionKind.FastChargerRepaired =>
                $"The failed {w.ResourceSingular} is restored during the window",
            _ => "No supported assumption was recognised in this question"
        };
    }

    /// <summary>Re-words an assumption for the incident it will actually be applied to.</summary>
    public static AssumptionOverride Relabel(this AssumptionOverride assumption, IncidentVocabulary words)
    {
        ArgumentNullException.ThrowIfNull(assumption);
        ArgumentNullException.ThrowIfNull(words);

        return assumption.Recognised
            ? assumption with { Label = Describe(assumption.Kind, assumption.Value, words) }
            : assumption;
    }

    private static string Duration(double minutes) => minutes switch
    {
        60 => "one hour",
        120 => "two hours",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{minutes:0} minutes"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60.0:0.#} hours")
    };
}
