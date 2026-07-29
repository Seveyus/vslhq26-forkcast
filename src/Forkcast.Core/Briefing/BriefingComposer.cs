using System.Globalization;
using Forkcast.Core.Decisions;
using Forkcast.Core.Incidents;
using Forkcast.Core.Verification;

namespace Forkcast.Core.Briefing;

/// <summary>
/// Turns a finished decision into a brief a renderer can animate.
/// </summary>
/// <remarks>
/// Every caption is assembled from claim display values, incident facts and the domain's own
/// vocabulary. Nothing here writes a number of its own, which is what lets the briefing be exported
/// and rendered without a second verification pass: it cannot contain a figure the claim set does
/// not already carry.
/// </remarks>
public sealed class BriefingComposer
{
    public DecisionBriefing Compose(DecisionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var words = result.Incident.Vocabulary;
        var claims = result.Verification.Claims.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var baseline = result.Comparison.Baseline;
        var alternative = result.Comparison.Alternative;
        var recommended = result.Comparison.Recommended;

        var beats = new List<BriefingBeat>();
        var clock = 0.0;

        BriefingBeat Beat(string id, string kind, double seconds, string heading, string caption,
            params string[] claimIds)
        {
            var beat = new BriefingBeat
            {
                Id = id,
                Kind = kind,
                StartSeconds = Math.Round(clock, 2),
                DurationSeconds = seconds,
                Heading = heading,
                Caption = caption,
                ClaimIds = claimIds
            };
            clock += seconds;
            return beat;
        }

        var situation = string.Create(
            CultureInfo.InvariantCulture,
            $"{result.Incident.VehicleCount} {words.UnitPlural} need "
            + $"{result.Incident.OperationalChargePointCount} {words.Resources(result.Incident.OperationalChargePointCount)} "
            + $"before {result.Incident.DepartureDeadline:HH\\:mm}. "
            + $"{result.Incident.FailedChargePointCount} of them just failed.");

        beats.Add(Beat(
            "situation", "situation", 12,
            result.Incident.Title,
            situation));

        beats.Add(Beat(
            "futures", "futures", 26,
            "Two futures",
            Compose2(claims, "baseline-on-time", "alternative-on-time",
                (a, b) => $"{baseline.PlanName}: {a}. {alternative.PlanName}: {b}."),
            "baseline-on-time", "alternative-on-time", "on-time-improvement"));

        beats.Add(Beat(
            "risk", "futures", 16,
            $"{Capitalise(words.UnitPlural)} at risk",
            Compose2(claims, "baseline-at-risk", "alternative-at-risk",
                (a, b) => $"{a} under the baseline, {b} if you act."),
            "baseline-at-risk", "alternative-at-risk"));

        beats.Add(Beat(
            "recommendation", "recommendation", 18,
            "Decision brief",
            result.Recommendation.Headline,
            "additional-cost"));

        // The claim and rejection counts travel on the briefing itself, so the caption states them
        // in words. A caption may only carry figures the claim set or the incident already backs,
        // and the verifier's own tallies are neither.
        beats.Add(Beat(
            "evidence", "evidence", 20,
            "Evidence ledger",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Every figure above traces to a simulation field, reproducible at seed {result.Seed}."),
            [.. claims.Keys]));

        if (result.Delta is { } delta && result.Assumption is { Recognised: true } assumption)
        {
            beats.Add(Beat(
                "counterfactual", "counterfactual", 20,
                "Counterfactual test",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{assumption.Label}: {words.OnTimeMetricLabel} "
                    + $"{delta.PreviousOnTimeDeparturePct:0.#}% to {delta.OnTimeDeparturePct:0.#}%, "
                    + $"{words.UnitPlural} at risk {delta.PreviousVehiclesAtRisk} to {delta.VehiclesAtRisk}."),
                "alternative-on-time", "alternative-at-risk"));
        }

        beats.Add(Beat(
            "close", "close", 8,
            "Forkcast",
            "See both futures before you decide."));

        return new DecisionBriefing
        {
            DomainKey = words.DomainKey,
            DomainLabel = words.DomainLabel,
            Title = result.Incident.Title,
            Situation = situation,
            RecommendedPlanId = result.Comparison.RecommendedPlanId,
            Headline = result.Recommendation.Headline,
            Seed = result.Seed,
            TrialCount = result.TrialCount,
            VerifiedClaims = result.Verification.VerifiedClaims,
            UnsupportedNumbers = result.Verification.UnsupportedNumbers,
            Claims = result.Verification.Claims,
            Beats = beats,
            Resources = result.Incident.ChargePoints
                .Select(point => new CanvasResource
                {
                    Id = point.Id,
                    Kind = point.Kind.ToString(),
                    Rate = point.RatedPowerKw,
                    Operational = point.IsOperational,
                    FaultCode = point.FaultCode
                })
                .ToList(),
            Plans = result.Outcomes
                .Select(outcome => new CanvasPlan
                {
                    PlanId = outcome.PlanId,
                    PlanName = outcome.PlanName,
                    Recommended = outcome.PlanId == result.Comparison.RecommendedPlanId,
                    OnTimePct = outcome.OnTimeDeparturePct,
                    AtRiskCount = outcome.VehiclesAtRisk,
                    RiskLevel = outcome.RiskLevel.ToString(),
                    Units = outcome.Vehicles
                        .Select(v => new CanvasUnit
                        {
                            Id = v.VehicleId,
                            Label = v.Route,
                            IsPriority = v.IsPriorityRoute,
                            OnTimeProbability = v.OnTimeProbability,
                            ShortfallLevel = v.ExpectedShortfallKwh,
                            SlackMinutes = v.ExpectedSlackMinutes,
                            AtRisk = v.IsAtRisk
                        })
                        .ToList()
                })
                .ToList(),
            CounterfactualLabel = result.Assumption is { Recognised: true } a ? a.Label : null
        };
    }

    private static string Compose2(
        IReadOnlyDictionary<string, Claim> claims,
        string firstId,
        string secondId,
        Func<string, string, string> format) =>
        claims.TryGetValue(firstId, out var first) && claims.TryGetValue(secondId, out var second)
            ? format(first.DisplayValue, second.DisplayValue)
            : string.Empty;

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
