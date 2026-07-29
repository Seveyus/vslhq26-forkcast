namespace Forkcast.Api.Contracts;

public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    string IntelligenceProvider,
    bool IntelligenceLive,
    long DefaultSeed,
    int DefaultTrialCount);

public sealed record VehicleDto(
    string Id,
    string Route,
    double BatteryCapacityKwh,
    double InitialStateOfChargePct,
    double RequiredStateOfChargePct,
    double RequiredEnergyKwh,
    bool IsPriorityRoute,
    DateTimeOffset ScheduledDeparture,
    string RosteredChargePointId);

public sealed record ChargePointDto(
    string Id,
    string Kind,
    double RatedPowerKw,
    bool IsOperational,
    string? FaultCode,
    string? FaultSummary);

public sealed record TariffWindowDto(
    string Label,
    DateTimeOffset From,
    DateTimeOffset To,
    double PricePerKwhGbp);

public sealed record ConstraintsDto(
    double AcArrayCapacityKw,
    double PreDepartureReadyMinutes,
    double PlugSwapBaseMinutes,
    double FaultRecoveryProbability);

public sealed record IncidentDto(
    string Id,
    string Title,
    string Narrative,
    string Site,
    DateTimeOffset DetectedAt,
    DateTimeOffset DepartureDeadline,
    double ChargingWindowHours,
    int VehicleCount,
    int PriorityVehicleCount,
    int OperationalChargePointCount,
    int TotalChargePointCount,
    int FailedChargePointCount,
    double TotalRequiredEnergyKwh,
    IReadOnlyList<string> Failures,
    IReadOnlyList<VehicleDto> Fleet,
    IReadOnlyList<ChargePointDto> ChargePoints,
    IReadOnlyList<TariffWindowDto> Tariff,
    ConstraintsDto Constraints);

public sealed record MobileBufferDto(
    int Outlets,
    double OutletPowerKw,
    double StoredEnergyKwh,
    DateTimeOffset PlannedArrival,
    double CallOutCostGbp);

public sealed record PlanDto(
    string Id,
    string Name,
    string Headline,
    string Description,
    IReadOnlyList<string> Actions,
    string ChargingPolicy,
    string ChargeTargetPolicy,
    MobileBufferDto? MobileBuffer);

public sealed record VehicleOutcomeDto(
    string VehicleId,
    string Route,
    bool IsPriorityRoute,
    double OnTimeProbability,
    double ExpectedShortfallKwh,
    double ExpectedSlackMinutes,
    bool IsAtRisk);

public sealed record LoadSampleDto(
    DateTimeOffset At,
    double GridPowerKw,
    double BufferPowerKw,
    int VehiclesCharging,
    int VehiclesReady);

public sealed record OutcomeDto(
    string PlanId,
    string PlanName,
    long Seed,
    int TrialCount,
    double OnTimeDeparturePct,
    double OnTimeDeparturePctP5,
    double OnTimeDeparturePctP95,
    double PriorityOnTimeDeparturePct,
    int VehiclesAtRisk,
    double ExpectedLateVehicles,
    double ExpectedUnmetEnergyKwh,
    double ExpectedEnergyCostGbp,
    double AdditionalCostGbp,
    double TotalOperationalCostGbp,
    double BufferEnergyKwh,
    double GridEnergyKwh,
    double ChargePointUtilisationPct,
    string RiskLevel,
    string CriticalConstraint,
    IReadOnlyList<VehicleOutcomeDto> Vehicles,
    IReadOnlyList<LoadSampleDto> LoadCurve);

public sealed record ComparisonDto(
    string BaselinePlanId,
    string AlternativePlanId,
    string RecommendedPlanId,
    string DecisionRule,
    double OnTimeImprovementPp,
    int VehiclesAtRiskAvoided,
    double UnmetEnergyAvoidedKwh,
    double AdditionalCostGbp,
    double CostPerDepartureSecuredGbp);

public sealed record RationalePointDto(string Text, IReadOnlyList<string> ClaimIds);

public sealed record RecommendationDto(
    string RecommendedPlanId,
    string RecommendedPlanName,
    string Headline,
    IReadOnlyList<string> Actions,
    IReadOnlyList<RationalePointDto> Rationale,
    string DecisionRule,
    string ResidualRisk,
    string CriticalConstraint,
    string DeterministicSummary);

public sealed record ClaimDto(
    string Id,
    string Label,
    double Value,
    string DisplayValue,
    string Unit,
    string SourceField,
    string CalculationMethod,
    long SimulationSeed,
    int TrialCount,
    bool Verified);

public sealed record UnsupportedNumberDto(string Token, string Context);

public sealed record VerificationDto(
    int TotalClaims,
    int VerifiedClaims,
    int UnsupportedNumbers,
    bool AllClaimsVerified,
    bool NarrativeAccepted,
    string NarrativeSource,
    long SimulationSeed,
    int TrialCount,
    IReadOnlyList<ClaimDto> Claims,
    IReadOnlyList<UnsupportedNumberDto> Unsupported);

public sealed record IntelligenceDto(string Provider, bool Live, string Badge);

public sealed record AssumptionDto(string Kind, double Value, string Label, string Question, bool Recognised);

public sealed record DeltaDto(
    double PreviousOnTimeDeparturePct,
    double OnTimeDeparturePct,
    double OnTimeChangePp,
    int PreviousVehiclesAtRisk,
    int VehiclesAtRisk,
    string PreviousRiskLevel,
    string RiskLevel,
    bool RecommendationChanged,
    string Summary);

public sealed record DecisionResponse(
    IncidentDto Incident,
    IReadOnlyList<PlanDto> Plans,
    IReadOnlyList<OutcomeDto> Outcomes,
    ComparisonDto Comparison,
    RecommendationDto Recommendation,
    VerificationDto Verification,
    string ExecutiveSummary,
    long Seed,
    int TrialCount,
    IntelligenceDto Intelligence,
    AssumptionDto? Assumption,
    DeltaDto? Delta,
    IReadOnlyList<string> Notes);

public sealed record NumberFindingDto(
    string Token,
    double Value,
    string Context,
    bool Supported,
    string? ClaimId,
    string? Reason);

public sealed record VerificationProbeResponse(
    bool Accepted,
    string Submitted,
    int NumbersFound,
    int NumbersSupported,
    int NumbersUnsupported,
    IReadOnlyList<NumberFindingDto> Findings,
    /// <summary>What the interface would put on screen after the check.</summary>
    string Displayed,
    /// <summary>"submitted" when the text survived, "deterministic" when it was replaced.</summary>
    string DisplayedSource,
    string Verdict,
    long SimulationSeed,
    int TrialCount,
    IReadOnlyList<ClaimDto> Claims);

/// <summary>A ready-made paragraph for the verifier demonstration.</summary>
public sealed record ProbeExampleDto(string Label, string Narrative, string Expectation);

public sealed record DemoIncidentResponse(
    IncidentDto Incident,
    string Narrative,
    IReadOnlyList<PlanDto> Plans,
    string SuggestedChallenge,
    IReadOnlyList<string> ExampleChallenges,
    IReadOnlyList<ProbeExampleDto> ExampleProbes,
    long DefaultSeed,
    int DefaultTrialCount);

public sealed record ParseIncidentResponse(
    IncidentDto Incident,
    string Source,
    IReadOnlyList<string> Notes,
    IReadOnlyList<AdjustmentDto> Adjustments);

public sealed record AdjustmentDto(string Field, string Reason);
