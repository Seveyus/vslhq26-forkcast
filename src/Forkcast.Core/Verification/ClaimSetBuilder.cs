using Forkcast.Core.Comparison;
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

    public IReadOnlyList<Claim> Build(PlanComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        var baseline = comparison.Baseline;
        var alternative = comparison.Alternative;
        var seed = alternative.Seed;
        var trials = alternative.TrialCount;

        var drafts = new (string Id, string Label, string Unit, string Source, string Method)[]
        {
            ("baseline-on-time",
                $"{baseline.PlanName}: on-time departures",
                "%",
                "baseline.onTimeDeparturePct",
                "Mean share of vehicles reaching their route state of charge before their "
                + "ready-by time, averaged over every trial"),

            ("alternative-on-time",
                $"{alternative.PlanName}: on-time departures",
                "%",
                "alternative.onTimeDeparturePct",
                "Mean share of vehicles reaching their route state of charge before their "
                + "ready-by time, averaged over every trial"),

            ("on-time-improvement",
                "Improvement in on-time departures",
                "percentage points",
                "comparison.onTimeImprovementPp",
                "Alternative on-time percentage minus baseline on-time percentage"),

            ("baseline-at-risk",
                $"{baseline.PlanName}: vehicles at risk",
                "vehicles",
                "baseline.vehiclesAtRisk",
                "Count of vehicles whose on-time probability across all trials falls below 90 percent"),

            ("alternative-at-risk",
                $"{alternative.PlanName}: vehicles at risk",
                "vehicles",
                "alternative.vehiclesAtRisk",
                "Count of vehicles whose on-time probability across all trials falls below 90 percent"),

            ("additional-cost",
                "Net additional operational cost of acting",
                "GBP",
                "comparison.additionalCostGbp",
                "Alternative total operational cost minus baseline total operational cost, where "
                + "total cost is metered energy priced against the time-of-use tariff plus any "
                + "call-out and buffer energy charges. Net, so it is lower than the intervention "
                + "cost alone by the metered energy the towed battery displaces"),

            ("baseline-unmet-energy",
                $"{baseline.PlanName}: unmet energy at departure",
                "kWh",
                "baseline.expectedUnmetEnergyKwh",
                "Mean total energy still missing across the fleet at ready-by time"),

            ("alternative-unmet-energy",
                $"{alternative.PlanName}: unmet energy at departure",
                "kWh",
                "alternative.expectedUnmetEnergyKwh",
                "Mean total energy still missing across the fleet at ready-by time")
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
