using System.Text.RegularExpressions;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;

namespace Forkcast.Core.Challenges;

/// <summary>
/// Reads a plain-language what-if question into one of the supported assumption levers, and
/// applies it to the incident and the response plans.
/// </summary>
/// <remarks>
/// The pattern matching here is the deterministic fallback. When Azure OpenAI is configured it
/// classifies the question instead, but it returns the same closed set of levers: the model
/// chooses which assumption is being challenged, never what the consequence is.
/// </remarks>
public sealed partial class ChallengeService
{
    /// <summary>Words too common to identify anything, dropped when learning a domain's nouns.</summary>
    private static readonly string[] Stopwords =
        ["the", "and", "for", "with", "from", "into", "unit", "units", "point", "points"];

    /// <summary>
    /// Reads a what-if question into one of the supported levers.
    /// </summary>
    /// <param name="question">The question, in the user's own words.</param>
    /// <param name="words">
    /// The incident's vocabulary, when one is known. Supplying it teaches the matcher the domain's
    /// own nouns, so "what if the burst capacity is late" is recognised in a compute hall for the
    /// same reason "what if the battery unit is late" is recognised at a depot. Without it the
    /// matcher falls back to the terms it ships with.
    /// </param>
    public AssumptionOverride Interpret(string question, IncidentVocabulary? words = null)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return AssumptionOverride.Unrecognised(question ?? string.Empty);
        }

        var text = question.Trim();
        var lower = text.ToLowerInvariant();

        var bufferTerms = new List<string>
        {
            "buffer", "battery unit", "temporary battery", "towed", "burst capacity",
            "extra capacity", "spare capacity", "standby capacity"
        };
        var resourceTerms = new List<string>
        {
            "charger", "charge point", "connector", "node", "worker", "slot", "rack"
        };

        if (words is not null)
        {
            bufferTerms.AddRange(Significant(words.BufferLabel));
            resourceTerms.AddRange(Significant(words.ResourceSingular));
            resourceTerms.AddRange(Significant(words.ResourcePlural));
            resourceTerms.AddRange(Significant(words.ConnectorNoun));
        }

        var mentionsBuffer = bufferTerms.Exists(term => lower.Contains(term, StringComparison.Ordinal));
        var mentionsResource = resourceTerms.Exists(term => lower.Contains(term, StringComparison.Ordinal));

        if (mentionsBuffer
            && (lower.Contains("never") || lower.Contains("unavailable")
                || lower.Contains("cannot") || lower.Contains("can't")
                || lower.Contains("no buffer") || lower.Contains("does not arrive")
                || lower.Contains("doesn't arrive")))
        {
            return new AssumptionOverride
            {
                Kind = AssumptionKind.BufferUnavailable,
                Value = 0,
                Label = AssumptionLabeller.Describe(AssumptionKind.BufferUnavailable, 0),
                Question = text
            };
        }

        if (mentionsBuffer
            && (lower.Contains("late") || lower.Contains("delay") || lower.Contains("slip")
                || lower.Contains("behind schedule")))
        {
            var minutes = ExtractDurationMinutes(lower) ?? 60.0;
            return new AssumptionOverride
            {
                Kind = AssumptionKind.BufferArrivalDelayMinutes,
                Value = minutes,
                Label = AssumptionLabeller.Describe(AssumptionKind.BufferArrivalDelayMinutes, minutes),
                Question = text
            };
        }

        if (mentionsResource
            && (lower.Contains("also fail") || lower.Contains("another") || lower.Contains("second")
                || lower.Contains("more fail") || lower.Contains("goes down") || lower.Contains("go down")
                || lower.Contains("drops out") || lower.Contains("is lost")))
        {
            var count = ExtractLeadingCount(lower) ?? 1;
            return new AssumptionOverride
            {
                Kind = AssumptionKind.AdditionalChargePointOutage,
                Value = count,
                Label = AssumptionLabeller.Describe(AssumptionKind.AdditionalChargePointOutage, count),
                Question = text
            };
        }

        if ((lower.Contains("repair") || lower.Contains("fixed") || lower.Contains("back online")
             || lower.Contains("restored") || lower.Contains("recovered"))
            && (mentionsResource || lower.Contains("fast charger") || lower.Contains("dc")))
        {
            return new AssumptionOverride
            {
                Kind = AssumptionKind.FastChargerRepaired,
                Value = 1,
                Label = AssumptionLabeller.Describe(AssumptionKind.FastChargerRepaired, 1),
                Question = text
            };
        }

        if ((lower.Contains("earlier") || lower.Contains("brought forward") || lower.Contains("sooner")
             || lower.Contains("bring forward"))
            && (lower.Contains("depart") || lower.Contains("deadline") || lower.Contains("leave")
                || lower.Contains("route") || lower.Contains("cut-off") || lower.Contains("cutoff")
                || lower.Contains("complete")))
        {
            var minutes = ExtractDurationMinutes(lower) ?? 60.0;
            return new AssumptionOverride
            {
                Kind = AssumptionKind.DeadlineEarlierMinutes,
                Value = minutes,
                Label = AssumptionLabeller.Describe(AssumptionKind.DeadlineEarlierMinutes, minutes),
                Question = text
            };
        }

        return AssumptionOverride.Unrecognised(text);
    }

    /// <summary>
    /// Applies the override. Returns the modified incident and plans, ready to be simulated
    /// exactly as the original was.
    /// </summary>
    public (Incident Incident, IReadOnlyList<ResponsePlan> Plans) Apply(
        Incident incident,
        IReadOnlyList<ResponsePlan> plans,
        AssumptionOverride assumption)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(assumption);

        return assumption.Kind switch
        {
            AssumptionKind.BufferArrivalDelayMinutes => (
                incident,
                MapBuffer(plans, b => b.DelayedBy(TimeSpan.FromMinutes(assumption.Value)))),

            AssumptionKind.BufferUnavailable => (
                incident,
                plans.Select(p => p.MobileBuffer is null ? p : p with { MobileBuffer = null }).ToList()),

            AssumptionKind.AdditionalChargePointOutage => (
                DisableChargePoints(incident, (int)Math.Round(assumption.Value)),
                plans),

            AssumptionKind.DeadlineEarlierMinutes => (
                BringDeparturesForward(incident, assumption.Value),
                plans),

            AssumptionKind.FastChargerRepaired => (
                incident with
                {
                    Constraints = incident.Constraints with { FaultRecoveryProbability = 1.0 }
                },
                plans),

            _ => (incident, plans)
        };
    }

    private static IReadOnlyList<ResponsePlan> MapBuffer(
        IReadOnlyList<ResponsePlan> plans,
        Func<MobileBufferOption, MobileBufferOption> transform) =>
        plans
            .Select(p => p.MobileBuffer is { } buffer ? p with { MobileBuffer = transform(buffer) } : p)
            .ToList();

    private static Incident DisableChargePoints(Incident incident, int count)
    {
        if (count <= 0)
        {
            return incident;
        }

        var remaining = count;
        var updated = new List<ChargePoint>(incident.ChargePoints.Count);

        // Take them off the end of the operational list so the earlier connectors, which the
        // rota references by name, keep their identity.
        foreach (var point in incident.ChargePoints.AsEnumerable().Reverse())
        {
            if (remaining > 0 && point.IsOperational)
            {
                remaining--;
                updated.Add(point with
                {
                    IsOperational = false,
                    FaultCode = "WHAT-IF",
                    FaultSummary = "Taken offline by a challenged assumption"
                });
            }
            else
            {
                updated.Add(point);
            }
        }

        updated.Reverse();
        return incident with { ChargePoints = updated };
    }

    private static Incident BringDeparturesForward(Incident incident, double minutes)
    {
        var shift = TimeSpan.FromMinutes(-Math.Abs(minutes));
        return incident with
        {
            DepartureDeadline = incident.DepartureDeadline + shift,
            Fleet = incident.Fleet
                .Select(v => v with { ScheduledDeparture = v.ScheduledDeparture + shift })
                .ToList()
        };
    }

    private static double? ExtractDurationMinutes(string lower)
    {
        var match = DurationPattern().Match(lower);
        if (!match.Success)
        {
            return null;
        }

        var amount = match.Groups["amount"].Value switch
        {
            "an" or "a" or "one" => 1.0,
            "two" => 2.0,
            "three" => 3.0,
            "half" => 0.5,
            var n => double.TryParse(n, out var parsed) ? parsed : 1.0
        };

        return match.Groups["unit"].Value.StartsWith("hour", StringComparison.Ordinal)
            ? amount * 60.0
            : amount;
    }

    private static int? ExtractLeadingCount(string lower)
    {
        var match = CountPattern().Match(lower);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups["n"].Value switch
        {
            "a" or "an" or "one" or "another" => 1,
            "two" => 2,
            "three" => 3,
            var n => int.TryParse(n, out var parsed) ? parsed : 1
        };
    }

    /// <summary>The identifying words of a label, for keyword matching.</summary>
    private static IEnumerable<string> Significant(string label) =>
        label.ToLowerInvariant()
            .Split([' ', '-', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 4 && !Stopwords.Contains(word));

    private static string DescribeDuration(double minutes) => minutes switch
    {
        60 => "one hour",
        120 => "two hours",
        < 60 => $"{minutes:0} minutes",
        _ => $"{minutes / 60.0:0.#} hours"
    };

    [GeneratedRegex(@"(?<amount>\d+(?:\.\d+)?|an|a|one|two|three|half)\s*(?<unit>hours?|hrs?|minutes?|mins?)")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"(?<n>\d+|a|an|one|two|three|another)\s+(?:more\s+|further\s+|other\s+|additional\s+)?(?:charge\s?point|charger|connector)")]
    private static partial Regex CountPattern();
}
