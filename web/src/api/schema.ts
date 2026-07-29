import { z } from 'zod'

/**
 * The wire contract, restated as runtime schemas.
 *
 * Forkcast's whole claim is that no number on screen is unaccounted for. Trusting the shape of
 * a network response on a TypeScript type alone would undercut that: types vanish at runtime.
 * Every response is parsed here, and a response that does not match is an error rather than a
 * half-rendered screen.
 */

export const vehicleSchema = z.object({
  id: z.string(),
  route: z.string(),
  batteryCapacityKwh: z.number(),
  initialStateOfChargePct: z.number(),
  requiredStateOfChargePct: z.number(),
  requiredEnergyKwh: z.number(),
  isPriorityRoute: z.boolean(),
  scheduledDeparture: z.string(),
  rosteredChargePointId: z.string(),
})

export const chargePointSchema = z.object({
  id: z.string(),
  kind: z.string(),
  ratedPowerKw: z.number(),
  isOperational: z.boolean(),
  faultCode: z.string().nullish(),
  faultSummary: z.string().nullish(),
})

export const tariffWindowSchema = z.object({
  label: z.string(),
  from: z.string(),
  to: z.string(),
  pricePerKwhGbp: z.number(),
})

export const vocabularySchema = z.object({
  domainKey: z.string(),
  domainLabel: z.string(),
  unitSingular: z.string(),
  unitPlural: z.string(),
  resourceSingular: z.string(),
  resourcePlural: z.string(),
  levelUnit: z.string(),
  rateUnit: z.string(),
  deadlineNoun: z.string(),
  onTimeMetricLabel: z.string(),
  priorityLabelPlural: z.string(),
  capacityPoolLabel: z.string(),
  bufferLabel: z.string(),
  shortfallLabel: z.string(),
})

export const scenarioSummarySchema = z.object({
  key: z.string(),
  title: z.string(),
  domainLabel: z.string(),
  narrative: z.string(),
  suggestedChallenge: z.string(),
  unitCount: z.number(),
  resourceCount: z.number(),
})

export const incidentSchema = z.object({
  id: z.string(),
  title: z.string(),
  narrative: z.string(),
  site: z.string(),
  detectedAt: z.string(),
  departureDeadline: z.string(),
  chargingWindowHours: z.number(),
  vehicleCount: z.number(),
  priorityVehicleCount: z.number(),
  operationalChargePointCount: z.number(),
  totalChargePointCount: z.number(),
  failedChargePointCount: z.number(),
  totalRequiredEnergyKwh: z.number(),
  failures: z.array(z.string()),
  fleet: z.array(vehicleSchema),
  chargePoints: z.array(chargePointSchema),
  tariff: z.array(tariffWindowSchema),
  constraints: z.object({
    acArrayCapacityKw: z.number(),
    preDepartureReadyMinutes: z.number(),
    plugSwapBaseMinutes: z.number(),
    faultRecoveryProbability: z.number(),
  }),
  vocabulary: vocabularySchema,
})

export const planSchema = z.object({
  id: z.string(),
  name: z.string(),
  headline: z.string(),
  description: z.string(),
  actions: z.array(z.string()),
  chargingPolicy: z.string(),
  chargeTargetPolicy: z.string(),
  mobileBuffer: z
    .object({
      outlets: z.number(),
      outletPowerKw: z.number(),
      storedEnergyKwh: z.number(),
      plannedArrival: z.string(),
      callOutCostGbp: z.number(),
    })
    .nullish(),
})

export const riskLevelSchema = z.enum(['Low', 'Medium', 'High', 'Critical'])

export const outcomeSchema = z.object({
  planId: z.string(),
  planName: z.string(),
  seed: z.number(),
  trialCount: z.number(),
  onTimeDeparturePct: z.number(),
  onTimeDeparturePctP5: z.number(),
  onTimeDeparturePctP95: z.number(),
  priorityOnTimeDeparturePct: z.number(),
  vehiclesAtRisk: z.number(),
  expectedLateVehicles: z.number(),
  expectedUnmetEnergyKwh: z.number(),
  expectedEnergyCostGbp: z.number(),
  additionalCostGbp: z.number(),
  totalOperationalCostGbp: z.number(),
  bufferEnergyKwh: z.number(),
  gridEnergyKwh: z.number(),
  chargePointUtilisationPct: z.number(),
  riskLevel: riskLevelSchema,
  criticalConstraint: z.string(),
  vehicles: z.array(
    z.object({
      vehicleId: z.string(),
      route: z.string(),
      isPriorityRoute: z.boolean(),
      onTimeProbability: z.number(),
      expectedShortfallKwh: z.number(),
      expectedSlackMinutes: z.number(),
      isAtRisk: z.boolean(),
    }),
  ),
  loadCurve: z.array(
    z.object({
      at: z.string(),
      gridPowerKw: z.number(),
      bufferPowerKw: z.number(),
      vehiclesCharging: z.number(),
      vehiclesReady: z.number(),
    }),
  ),
})

export const claimSchema = z.object({
  id: z.string(),
  label: z.string(),
  value: z.number(),
  displayValue: z.string(),
  unit: z.string(),
  sourceField: z.string(),
  calculationMethod: z.string(),
  simulationSeed: z.number(),
  trialCount: z.number(),
  verified: z.boolean(),
})

export const verificationSchema = z.object({
  totalClaims: z.number(),
  verifiedClaims: z.number(),
  unsupportedNumbers: z.number(),
  allClaimsVerified: z.boolean(),
  narrativeAccepted: z.boolean(),
  narrativeSource: z.string(),
  simulationSeed: z.number(),
  trialCount: z.number(),
  claims: z.array(claimSchema),
  unsupported: z.array(z.object({ token: z.string(), context: z.string() })),
})

export const decisionSchema = z.object({
  incident: incidentSchema,
  plans: z.array(planSchema),
  outcomes: z.array(outcomeSchema),
  comparison: z.object({
    baselinePlanId: z.string(),
    alternativePlanId: z.string(),
    recommendedPlanId: z.string(),
    decisionRule: z.string(),
    onTimeImprovementPp: z.number(),
    vehiclesAtRiskAvoided: z.number(),
    unmetEnergyAvoidedKwh: z.number(),
    additionalCostGbp: z.number(),
    costPerDepartureSecuredGbp: z.number(),
  }),
  recommendation: z.object({
    recommendedPlanId: z.string(),
    recommendedPlanName: z.string(),
    headline: z.string(),
    actions: z.array(z.string()),
    rationale: z.array(z.object({ text: z.string(), claimIds: z.array(z.string()) })),
    decisionRule: z.string(),
    residualRisk: riskLevelSchema,
    criticalConstraint: z.string(),
    deterministicSummary: z.string(),
  }),
  verification: verificationSchema,
  executiveSummary: z.string(),
  seed: z.number(),
  trialCount: z.number(),
  intelligence: z.object({
    provider: z.string(),
    live: z.boolean(),
    badge: z.string(),
  }),
  assumption: z
    .object({
      kind: z.string(),
      value: z.number(),
      label: z.string(),
      question: z.string(),
      recognised: z.boolean(),
    })
    .nullish(),
  delta: z
    .object({
      previousOnTimeDeparturePct: z.number(),
      onTimeDeparturePct: z.number(),
      onTimeChangePp: z.number(),
      previousVehiclesAtRisk: z.number(),
      vehiclesAtRisk: z.number(),
      previousRiskLevel: riskLevelSchema,
      riskLevel: riskLevelSchema,
      recommendationChanged: z.boolean(),
      summary: z.string(),
    })
    .nullish(),
  notes: z.array(z.string()),
})

export const probeExampleSchema = z.object({
  label: z.string(),
  narrative: z.string(),
  expectation: z.string(),
})

export const numberFindingSchema = z.object({
  token: z.string(),
  value: z.number(),
  context: z.string(),
  supported: z.boolean(),
  claimId: z.string().nullish(),
  reason: z.string().nullish(),
})

export const verificationProbeSchema = z.object({
  accepted: z.boolean(),
  submitted: z.string(),
  numbersFound: z.number(),
  numbersSupported: z.number(),
  numbersUnsupported: z.number(),
  findings: z.array(numberFindingSchema),
  displayed: z.string(),
  displayedSource: z.string(),
  verdict: z.string(),
  simulationSeed: z.number(),
  trialCount: z.number(),
  claims: z.array(claimSchema),
})

export const briefingBeatSchema = z.object({
  id: z.string(),
  kind: z.string(),
  startSeconds: z.number(),
  durationSeconds: z.number(),
  heading: z.string(),
  caption: z.string(),
  claimIds: z.array(z.string()),
})

export const canvasUnitSchema = z.object({
  id: z.string(),
  label: z.string(),
  isPriority: z.boolean(),
  onTimeProbability: z.number(),
  shortfallLevel: z.number(),
  slackMinutes: z.number(),
  atRisk: z.boolean(),
})

export const briefingSchema = z.object({
  domainKey: z.string(),
  domainLabel: z.string(),
  title: z.string(),
  situation: z.string(),
  recommendedPlanId: z.string(),
  headline: z.string(),
  seed: z.number(),
  trialCount: z.number(),
  verifiedClaims: z.number(),
  unsupportedNumbers: z.number(),
  totalSeconds: z.number(),
  counterfactualLabel: z.string().nullish(),
  claims: z.array(claimSchema),
  beats: z.array(briefingBeatSchema),
  resources: z.array(
    z.object({
      id: z.string(),
      kind: z.string(),
      rate: z.number(),
      operational: z.boolean(),
      faultCode: z.string().nullish(),
    }),
  ),
  plans: z.array(
    z.object({
      planId: z.string(),
      planName: z.string(),
      recommended: z.boolean(),
      onTimePct: z.number(),
      atRiskCount: z.number(),
      riskLevel: z.string(),
      units: z.array(canvasUnitSchema),
    }),
  ),
  vocabulary: vocabularySchema,
})

export const demoIncidentSchema = z.object({
  incident: incidentSchema,
  narrative: z.string(),
  plans: z.array(planSchema),
  suggestedChallenge: z.string(),
  exampleChallenges: z.array(z.string()),
  exampleProbes: z.array(probeExampleSchema),
  scenarios: z.array(scenarioSummarySchema),
  scenarioKey: z.string(),
  defaultSeed: z.number(),
  defaultTrialCount: z.number(),
})

export const healthSchema = z.object({
  status: z.string(),
  service: z.string(),
  version: z.string(),
  intelligenceProvider: z.string(),
  intelligenceLive: z.boolean(),
  defaultSeed: z.number(),
  defaultTrialCount: z.number(),
})

export type Incident = z.infer<typeof incidentSchema>
export type Plan = z.infer<typeof planSchema>
export type Outcome = z.infer<typeof outcomeSchema>
export type Claim = z.infer<typeof claimSchema>
export type RiskLevel = z.infer<typeof riskLevelSchema>
export type Decision = z.infer<typeof decisionSchema>
export type DemoIncident = z.infer<typeof demoIncidentSchema>
export type Health = z.infer<typeof healthSchema>
export type ProbeExample = z.infer<typeof probeExampleSchema>
export type Vocabulary = z.infer<typeof vocabularySchema>
export type ScenarioSummary = z.infer<typeof scenarioSummarySchema>
export type NumberFinding = z.infer<typeof numberFindingSchema>
export type VerificationProbe = z.infer<typeof verificationProbeSchema>
export type Briefing = z.infer<typeof briefingSchema>
export type BriefingBeat = z.infer<typeof briefingBeatSchema>
