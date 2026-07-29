using System.Globalization;
using System.Text;
using Forkcast.Core.Comparison;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;
using Forkcast.Core.Verification;

namespace Forkcast.Core.Recommendations;

/// <summary>
/// Assembles the recommendation and its deterministic explanation from the comparison and the
/// claim set. No language model is consulted here.
/// </summary>
public sealed class RecommendationService
{
    public Recommendation Build(
        PlanComparison comparison,
        IReadOnlyList<ResponsePlan> plans,
        IReadOnlyList<Claim> claims,
        IncidentVocabulary words)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(words);

        var recommended = comparison.Recommended;
        var plan = plans.FirstOrDefault(p => p.Id == comparison.RecommendedPlanId)
                   ?? throw new InvalidOperationException(
                       $"Recommended plan '{comparison.RecommendedPlanId}' is not in the plan set.");

        var byId = claims.ToDictionary(c => c.Id, StringComparer.Ordinal);

        var rationale = new List<RationalePoint>();

        if (byId.TryGetValue("alternative-on-time", out var altOnTime)
            && byId.TryGetValue("baseline-on-time", out var baseOnTime)
            && byId.TryGetValue("on-time-improvement", out var improvement))
        {
            rationale.Add(new RationalePoint
            {
                Text = $"Expected {words.OnTimeMetricLabel} rise from {baseOnTime.DisplayValue} to "
                       + $"{altOnTime.DisplayValue}, a gain of {improvement.DisplayValue}.",
                ClaimIds = ["baseline-on-time", "alternative-on-time", "on-time-improvement"]
            });
        }

        if (byId.TryGetValue("baseline-at-risk", out var baseRisk)
            && byId.TryGetValue("alternative-at-risk", out var altRisk))
        {
            rationale.Add(new RationalePoint
            {
                Text = $"{Capitalise(words.UnitPlural)} at risk fall from {baseRisk.DisplayValue} to {altRisk.DisplayValue}.",
                ClaimIds = ["baseline-at-risk", "alternative-at-risk"]
            });
        }

        if (byId.TryGetValue("baseline-unmet-energy", out var baseUnmet)
            && byId.TryGetValue("alternative-unmet-energy", out var altUnmet))
        {
            rationale.Add(new RationalePoint
            {
                Text = $"{Capitalise(words.ShortfallLabel)} at {words.DeadlineNoun} falls from "
                       + $"{baseUnmet.DisplayValue} to {altUnmet.DisplayValue}.",
                ClaimIds = ["baseline-unmet-energy", "alternative-unmet-energy"]
            });
        }

        if (byId.TryGetValue("additional-cost", out var cost))
        {
            rationale.Add(new RationalePoint
            {
                Text = $"Acting costs {cost.DisplayValue} more than doing nothing.",
                ClaimIds = ["additional-cost"]
            });
        }

        return new Recommendation
        {
            RecommendedPlanId = plan.Id,
            RecommendedPlanName = plan.Name,
            Headline = plan.Headline,
            Actions = plan.Actions,
            Rationale = rationale,
            DecisionRule = comparison.DecisionRule,
            ResidualRisk = recommended.RiskLevel,
            CriticalConstraint = recommended.CriticalConstraint,
            DeterministicSummary = BuildDeterministicSummary(comparison, plan, byId, words)
        };
    }

    /// <summary>
    /// Builds a summary using only claim display values, the seed and the trial count, so it is
    /// safe to show without further checking.
    /// </summary>
    private static string BuildDeterministicSummary(
        PlanComparison comparison,
        ResponsePlan plan,
        IReadOnlyDictionary<string, Claim> byId,
        IncidentVocabulary words)
    {
        var seed = comparison.Alternative.Seed.ToString(CultureInfo.InvariantCulture);
        var trials = comparison.Alternative.TrialCount.ToString(CultureInfo.InvariantCulture);

        var text = new StringBuilder();
        text.Append(plan.Headline).Append(' ');
        text.Append("Across ").Append(trials).Append(" simulated nights seeded with ").Append(seed)
            .Append(", this response is expected to deliver ");

        if (byId.TryGetValue("alternative-on-time", out var altOnTime)
            && byId.TryGetValue("baseline-on-time", out var baseOnTime)
            && byId.TryGetValue("on-time-improvement", out var improvement))
        {
            text.Append(altOnTime.DisplayValue).Append(' ').Append(words.OnTimeMetricLabel).Append(" against ")
                .Append(baseOnTime.DisplayValue).Append(" for the baseline, a gain of ")
                .Append(improvement.DisplayValue).Append(". ");
        }

        if (byId.TryGetValue("baseline-at-risk", out var baseRisk)
            && byId.TryGetValue("alternative-at-risk", out var altRisk))
        {
            text.Append(Capitalise(words.UnitPlural)).Append(" at risk fall from ").Append(baseRisk.DisplayValue)
                .Append(" to ").Append(altRisk.DisplayValue).Append(". ");
        }

        if (byId.TryGetValue("baseline-unmet-energy", out var baseUnmet)
            && byId.TryGetValue("alternative-unmet-energy", out var altUnmet))
        {
            text.Append(Capitalise(words.ShortfallLabel)).Append(" at ").Append(words.DeadlineNoun)
                .Append(" falls from ").Append(baseUnmet.DisplayValue)
                .Append(" to ").Append(altUnmet.DisplayValue).Append(". ");
        }

        if (byId.TryGetValue("additional-cost", out var cost))
        {
            text.Append("The additional operational cost is ").Append(cost.DisplayValue).Append(". ");
        }

        text.Append("Residual risk is rated ")
            .Append(comparison.Recommended.RiskLevel.ToString().ToLowerInvariant())
            .Append('.');

        return text.ToString();
    }

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
