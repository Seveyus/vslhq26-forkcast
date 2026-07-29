using Forkcast.Core.Comparison;
using Forkcast.Core.Incidents;
using Forkcast.Core.Simulation;

namespace Forkcast.Core.Verification;

/// <summary>
/// Builds the claim set that backs the recommendation, and proves each claim still matches the
/// simulation output it says it came from.
/// </summary>
public sealed class ClaimSetBuilder
{
    /// <summary>
    /// Reads a dotted source-field path back out of the outcomes. This is what makes a claim
    /// verifiable rather than merely labelled: the value on the claim has to agree with the
    /// value this resolver returns.
    /// </summary>
    public static double? Resolve(string sourceField, PlanComparison comparison)
    {
        var parts = sourceField.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        if (parts[0] == "comparison")
        {
            return parts[1] switch
            {
                "onTimeImprovementPp" => comparison.OnTimeImprovementPp,
                "vehiclesAtRiskAvoided" => comparison.VehiclesAtRiskAvoided,
                "unmetEnergyAvoidedKwh" => comparison.UnmetEnergyAvoidedKwh,
                "additionalCostGbp" => comparison.AdditionalCostGbp,
                "costPerDepartureSecuredGbp" => comparison.CostPerDepartureSecuredGbp,
                _ => null
            };
        }

        var outcome = parts[0] switch
        {
            "baseline" => comparison.Baseline,
            "alternative" => comparison.Alternative,
            _ => null
        };

        if (outcome is null)
        {
            return null;
        }

        return parts[1] switch
        {
            "onTimeDeparturePct" => outcome.OnTimeDeparturePct,
            "onTimeDeparturePctP5" => outcome.OnTimeDeparturePctP5,
            "onTimeDeparturePctP95" => outcome.OnTimeDeparturePctP95,
            "priorityOnTimeDeparturePct" => outcome.PriorityOnTimeDeparturePct,
            "vehiclesAtRisk" => outcome.VehiclesAtRisk,
            "expectedLateVehicles" => outcome.ExpectedLateVehicles,
            "expectedUnmetEnergyKwh" => outcome.ExpectedUnmetEnergyKwh,
            "expectedEnergyCostGbp" => outcome.ExpectedEnergyCostGbp,
            "expectedInterventionCostGbp" => outcome.ExpectedInterventionCostGbp,
            "expectedOperationalCostGbp" => outcome.ExpectedOperationalCostGbp,
            "expectedBufferEnergyKwh" => outcome.ExpectedBufferEnergyKwh,
            "chargePointUtilisationPct" => outcome.ChargePointUtilisationPct,
            _ => null
        };
    }

    public IReadOnlyList<Claim> Build(PlanComparison comparison, IncidentVocabulary words)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(words);

        var baseline = comparison.Baseline;
        var alternative = comparison.Alternative;
        var seed = alternative.Seed;
        var trials = alternative.TrialCount;

        var drafts = new (string Id, string Label, string Unit, string Source, string Method)[]
        {
            ("baseline-on-time",
                $"{baseline.PlanName}: {words.OnTimeMetricLabel}",
                "%",
                "baseline.onTimeDeparturePct",
                $"Mean share of {words.UnitPlural} reaching their required level before their "
                + "ready-by time, averaged over every trial"),

            ("alternative-on-time",
                $"{alternative.PlanName}: {words.OnTimeMetricLabel}",
                "%",
                "alternative.onTimeDeparturePct",
                $"Mean share of {words.UnitPlural} reaching their required level before their "
                + "ready-by time, averaged over every trial"),

            ("on-time-improvement",
                $"Improvement in {words.OnTimeMetricLabel}",
                "percentage points",
                "comparison.onTimeImprovementPp",
                "Alternative on-time percentage minus baseline on-time percentage"),

            ("baseline-at-risk",
                $"{baseline.PlanName}: {words.UnitPlural} at risk",
                "count",
                "baseline.vehiclesAtRisk",
                $"Count of {words.UnitPlural} whose on-time probability across all trials falls below 90 percent"),

            ("alternative-at-risk",
                $"{alternative.PlanName}: {words.UnitPlural} at risk",
                "count",
                "alternative.vehiclesAtRisk",
                $"Count of {words.UnitPlural} whose on-time probability across all trials falls below 90 percent"),

            ("additional-cost",
                "Net additional operational cost of acting",
                "GBP",
                "comparison.additionalCostGbp",
                "Alternative total operational cost minus baseline total operational cost, where "
                + "total cost is metered throughput priced against the time-of-use tariff plus any "
                + $"call-out and {words.BufferLabel} charges. Net, so it is lower than the "
                + $"intervention cost alone by the metered cost the {words.BufferLabel} displaces"),

            ("baseline-unmet-energy",
                $"{baseline.PlanName}: {words.ShortfallLabel} at {words.DeadlineNoun}",
                words.LevelUnit,
                "baseline.expectedUnmetEnergyKwh",
                $"Mean total {words.ShortfallLabel} across all {words.UnitPlural} at ready-by time"),

            ("alternative-unmet-energy",
                $"{alternative.PlanName}: {words.ShortfallLabel} at {words.DeadlineNoun}",
                words.LevelUnit,
                "alternative.expectedUnmetEnergyKwh",
                $"Mean total {words.ShortfallLabel} across all {words.UnitPlural} at ready-by time")
        };

        var claims = new List<Claim>(drafts.Length);
        foreach (var draft in drafts)
        {
            var resolved = Resolve(draft.Source, comparison);
            claims.Add(new Claim
            {
                Id = draft.Id,
                Label = draft.Label,
                Value = resolved ?? double.NaN,
                Unit = draft.Unit,
                SourceField = draft.Source,
                CalculationMethod = draft.Method,
                SimulationSeed = seed,
                TrialCount = trials,
                Verified = resolved is not null && !double.IsNaN(resolved.Value)
            });
        }

        return claims;
    }
}
