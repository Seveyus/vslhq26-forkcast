using Forkcast.Core.Simulation;

namespace Forkcast.Core.Comparison;

/// <summary>
/// Turns two simulated outcomes into a single ranked comparison.
/// </summary>
/// <remarks>
/// The decision rule is intentionally small and stated in the output. A duty manager should be
/// able to read the rule, check the two numbers it uses and agree or disagree, without trusting
/// anything opaque.
/// </remarks>
public sealed class ComparisonService
{
    /// <summary>On-time differences smaller than this are treated as a tie.</summary>
    public const double OnTimeTieThresholdPp = 1.0;

    private static string Threshold =>
        OnTimeTieThresholdPp == 1.0
            ? "1 percentage point"
            : $"{OnTimeTieThresholdPp:0.#} percentage point";

    public PlanComparison Compare(PlanOutcome baseline, PlanOutcome alternative)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(alternative);

        var onTimeGap = alternative.OnTimeDeparturePct - baseline.OnTimeDeparturePct;
        var additionalCost = alternative.ExpectedOperationalCostGbp - baseline.ExpectedOperationalCostGbp;

        string recommendedId;
        string rule;

        if (Math.Abs(onTimeGap) < OnTimeTieThresholdPp)
        {
            var cheaper = alternative.ExpectedOperationalCostGbp <= baseline.ExpectedOperationalCostGbp
                ? alternative
                : baseline;
            recommendedId = cheaper.PlanId;
            rule = $"On-time departures differ by less than the {Threshold} tie threshold, "
                   + "so the lower total operational cost decides.";
        }
        else
        {
            var better = onTimeGap > 0 ? alternative : baseline;
            recommendedId = better.PlanId;
            rule = "Highest expected on-time departures wins, the gap being larger than the "
                   + $"{Threshold} tie threshold.";
        }

        var departuresSecured = baseline.Vehicles.Count * (onTimeGap / 100.0);

        return new PlanComparison
        {
            Baseline = baseline,
            Alternative = alternative,
            RecommendedPlanId = recommendedId,
            DecisionRule = rule,
            OnTimeImprovementPp = Math.Round(onTimeGap, 1, MidpointRounding.AwayFromZero),
            VehiclesAtRiskAvoided = baseline.VehiclesAtRisk - alternative.VehiclesAtRisk,
            UnmetEnergyAvoidedKwh = Math.Round(
                baseline.ExpectedUnmetEnergyKwh - alternative.ExpectedUnmetEnergyKwh,
                1,
                MidpointRounding.AwayFromZero),
            AdditionalCostGbp = Math.Round(additionalCost, 2, MidpointRounding.AwayFromZero),
            CostPerDepartureSecuredGbp = departuresSecured > 0.01
                ? Math.Round(additionalCost / departuresSecured, 2, MidpointRounding.AwayFromZero)
                : 0.0
        };
    }
}
