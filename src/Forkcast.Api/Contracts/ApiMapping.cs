using Forkcast.Api.Services;
using Forkcast.Core.Ai;
using Forkcast.Core.Briefing;
using Forkcast.Core.Challenges;
using Forkcast.Core.Demo;
using Forkcast.Core.Comparison;
using Forkcast.Core.Decisions;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;
using Forkcast.Core.Recommendations;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;

namespace Forkcast.Api.Contracts;

/// <summary>
/// Projects the domain onto the wire contract.
/// </summary>
/// <remarks>
/// Kept as an explicit mapping rather than serialising the domain directly, so that renaming a
/// field in the engine cannot silently change the shape the frontend depends on.
/// </remarks>
internal static class ApiMapping
{
    public static IncidentDto ToDto(this Incident incident) => new(
        incident.Id,
        incident.Title,
        incident.Narrative,
        incident.Site,
        incident.DetectedAt,
        incident.DepartureDeadline,
        Math.Round((incident.DepartureDeadline - incident.DetectedAt).TotalHours, 2),
        incident.VehicleCount,
        incident.PriorityVehicleCount,
        incident.OperationalChargePointCount,
        incident.ChargePoints.Count,
        incident.FailedChargePointCount,
        Math.Round(incident.TotalRequiredEnergyKwh, 1),
        incident.Failures,
        incident.Fleet.Select(ToDto).ToList(),
        incident.ChargePoints.Select(ToDto).ToList(),
        incident.Tariff.Select(ToDto).ToList(),
        new ConstraintsDto(
            incident.Constraints.AcArrayCapacityKw,
            incident.Constraints.PreDepartureReadyMinutes,
            incident.Constraints.PlugSwapBaseMinutes,
            incident.Constraints.FaultRecoveryProbability),
        incident.Vocabulary.ToDto());

    private static VehicleDto ToDto(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.Route,
        vehicle.BatteryCapacityKwh,
        vehicle.InitialStateOfChargePct,
        vehicle.RequiredStateOfChargePct,
        Math.Round(vehicle.RequiredEnergyKwh, 1),
        vehicle.IsPriorityRoute,
        vehicle.ScheduledDeparture,
        vehicle.RosteredChargePointId);

    private static ChargePointDto ToDto(ChargePoint point) => new(
        point.Id,
        point.Kind.ToString(),
        point.RatedPowerKw,
        point.IsOperational,
        point.FaultCode,
        point.FaultSummary);

    private static TariffWindowDto ToDto(TariffWindow window) => new(
        window.Label, window.From, window.To, window.PricePerKwhGbp);

    public static VocabularyDto ToDto(this IncidentVocabulary words) => new(
        words.DomainKey,
        words.DomainLabel,
        words.UnitSingular,
        words.UnitPlural,
        words.ResourceSingular,
        words.ResourcePlural,
        words.LevelUnit,
        words.RateUnit,
        words.DeadlineNoun,
        words.OnTimeMetricLabel,
        words.PriorityLabelPlural,
        words.CapacityPoolLabel,
        words.BufferLabel,
        words.ShortfallLabel);

    public static ScenarioSummaryDto ToSummaryDto(this Scenario scenario) => new(
        scenario.Key,
        scenario.Title,
        scenario.DomainLabel,
        scenario.Narrative,
        scenario.SuggestedChallenge,
        scenario.Incident.VehicleCount,
        scenario.Incident.OperationalChargePointCount);

    public static PlanDto ToDto(this ResponsePlan plan) => new(
        plan.Id,
        plan.Name,
        plan.Headline,
        plan.Description,
        plan.Actions,
        plan.ChargingPolicy.ToString(),
        plan.ChargeTargetPolicy.ToString(),
        plan.MobileBuffer is { } buffer
            ? new MobileBufferDto(
                buffer.Outlets,
                buffer.OutletPowerKw,
                buffer.StoredEnergyKwh,
                buffer.PlannedArrival,
                buffer.CallOutCostGbp)
            : null);

    public static OutcomeDto ToDto(this PlanOutcome outcome) => new(
        outcome.PlanId,
        outcome.PlanName,
        outcome.Seed,
        outcome.TrialCount,
        outcome.OnTimeDeparturePct,
        outcome.OnTimeDeparturePctP5,
        outcome.OnTimeDeparturePctP95,
        outcome.PriorityOnTimeDeparturePct,
        outcome.VehiclesAtRisk,
        outcome.ExpectedLateVehicles,
        outcome.ExpectedUnmetEnergyKwh,
        outcome.ExpectedEnergyCostGbp,
        outcome.ExpectedInterventionCostGbp,
        outcome.ExpectedOperationalCostGbp,
        outcome.ExpectedBufferEnergyKwh,
        outcome.ExpectedGridEnergyKwh,
        outcome.ChargePointUtilisationPct,
        outcome.RiskLevel.ToString(),
        outcome.CriticalConstraint,
        outcome.Vehicles
            .Select(v => new VehicleOutcomeDto(
                v.VehicleId, v.Route, v.IsPriorityRoute, v.OnTimeProbability,
                v.ExpectedShortfallKwh, v.ExpectedSlackMinutes, v.IsAtRisk))
            .ToList(),
        outcome.LoadCurve
            .Select(s => new LoadSampleDto(
                s.At, s.GridPowerKw, s.BufferPowerKw, s.VehiclesCharging, s.VehiclesReady))
            .ToList());

    public static ComparisonDto ToDto(this PlanComparison comparison) => new(
        comparison.Baseline.PlanId,
        comparison.Alternative.PlanId,
        comparison.RecommendedPlanId,
        comparison.DecisionRule,
        comparison.OnTimeImprovementPp,
        comparison.VehiclesAtRiskAvoided,
        comparison.UnmetEnergyAvoidedKwh,
        comparison.AdditionalCostGbp,
        comparison.CostPerDepartureSecuredGbp);

    public static RecommendationDto ToDto(this Recommendation recommendation) => new(
        recommendation.RecommendedPlanId,
        recommendation.RecommendedPlanName,
        recommendation.Headline,
        recommendation.Actions,
        recommendation.Rationale
            .Select(r => new RationalePointDto(r.Text, r.ClaimIds))
            .ToList(),
        recommendation.DecisionRule,
        recommendation.ResidualRisk.ToString(),
        recommendation.CriticalConstraint,
        recommendation.DeterministicSummary);

    public static VerificationDto ToDto(this ClaimVerification verification) => new(
        verification.TotalClaims,
        verification.VerifiedClaims,
        verification.UnsupportedNumbers,
        verification.AllClaimsVerified,
        verification.NarrativeAccepted,
        verification.NarrativeSource,
        verification.SimulationSeed,
        verification.TrialCount,
        verification.Claims.Select(ToDto).ToList(),
        verification.Unsupported
            .Select(u => new UnsupportedNumberDto(u.Token, u.Context))
            .ToList());

    private static ClaimDto ToDto(Claim claim) => new(
        claim.Id,
        claim.Label,
        claim.Value,
        claim.DisplayValue,
        claim.Unit,
        claim.SourceField,
        claim.CalculationMethod,
        claim.SimulationSeed,
        claim.TrialCount,
        claim.Verified);

    private static AssumptionDto ToDto(AssumptionOverride assumption) => new(
        assumption.Kind.ToString(),
        assumption.Value,
        assumption.Label,
        assumption.Question,
        assumption.Recognised);

    private static DeltaDto ToDto(DecisionDelta delta) => new(
        ToDto(delta.PreviousOnTimeClaim),
        ToDto(delta.PreviousAtRiskClaim),
        delta.PreviousOnTimeDeparturePct,
        delta.OnTimeDeparturePct,
        delta.OnTimeChangePp,
        delta.PreviousVehiclesAtRisk,
        delta.VehiclesAtRisk,
        delta.PreviousRiskLevel.ToString(),
        delta.RiskLevel.ToString(),
        delta.RecommendationChanged,
        delta.Summary);

    public static DecisionResponse ToResponse(
        this DecisionResult result,
        IReadOnlyList<string>? notes = null) => new(
        result.Incident.ToDto(),
        result.Plans.Select(ToDto).ToList(),
        result.Outcomes.Select(ToDto).ToList(),
        result.Comparison.ToDto(),
        result.Recommendation.ToDto(),
        result.Verification.ToDto(),
        result.ExecutiveSummary,
        result.Seed,
        result.TrialCount,
        new IntelligenceDto(
            result.IntelligenceProvider,
            result.IntelligenceLive,
            result.IntelligenceLive ? "Azure OpenAI connected" : "Deterministic demo mode"),
        result.Assumption is null ? null : ToDto(result.Assumption),
        result.Delta is null ? null : ToDto(result.Delta),
        notes ?? []);

    public static AdjustmentDto ToDto(this DraftAdjustment adjustment) =>
        new(adjustment.Field, adjustment.Reason);

    public static BriefingResponse ToResponse(this DecisionBriefing briefing, IncidentVocabulary words)
    {
        ArgumentNullException.ThrowIfNull(briefing);
        ArgumentNullException.ThrowIfNull(words);

        return new BriefingResponse(
            briefing.DomainKey,
            briefing.DomainLabel,
            briefing.Title,
            briefing.Situation,
            briefing.RecommendedPlanId,
            briefing.Headline,
            briefing.Seed,
            briefing.TrialCount,
            briefing.VerifiedClaims,
            briefing.UnsupportedNumbers,
            briefing.TotalSeconds,
            briefing.CounterfactualLabel,
            briefing.Claims.Select(ToDto).ToList(),
            briefing.Beats
                .Select(b => new BriefingBeatDto(
                    b.Id, b.Kind, b.StartSeconds, b.DurationSeconds, b.Heading, b.Caption, b.ClaimIds))
                .ToList(),
            briefing.Resources
                .Select(r => new CanvasResourceDto(r.Id, r.Kind, r.Rate, r.Operational, r.FaultCode))
                .ToList(),
            briefing.Plans
                .Select(plan => new CanvasPlanDto(
                    plan.PlanId,
                    plan.PlanName,
                    plan.Recommended,
                    plan.OnTimePct,
                    plan.AtRiskCount,
                    plan.RiskLevel,
                    plan.Units
                        .Select(u => new CanvasUnitDto(
                            u.Id, u.Label, u.IsPriority, u.OnTimeProbability,
                            u.ShortfallLevel, u.SlackMinutes, u.AtRisk))
                        .ToList()))
                .ToList(),
            words.ToDto());
    }

    public static VerificationProbeResponse ToResponse(this VerificationProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var supported = probe.Findings.Count(f => f.Supported);
        var unsupported = probe.Findings.Count - supported;

        return new VerificationProbeResponse(
            probe.Accepted,
            probe.Submitted,
            probe.Findings.Count,
            supported,
            unsupported,
            probe.Findings
                .Select(f => new NumberFindingDto(
                    f.Token, f.Value, f.Context, f.Supported, f.ClaimId, f.Reason))
                .ToList(),
            probe.Displayed,
            probe.DisplayedSource,
            probe.Accepted
                ? "Every number in this paragraph is backed by a claim or an incident fact, so it "
                  + "would be shown as written."
                : $"{unsupported} number{(unsupported == 1 ? "" : "s")} in this paragraph "
                  + $"{(unsupported == 1 ? "is" : "are")} not backed by any claim, so the whole "
                  + "paragraph is discarded and the deterministic summary is shown instead.",
            probe.Seed,
            probe.TrialCount,
            probe.Claims.Select(ToPublicDto).ToList());
    }

    private static ClaimDto ToPublicDto(Claim claim) => ToDto(claim);
}
