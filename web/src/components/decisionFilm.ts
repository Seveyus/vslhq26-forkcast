import type { Briefing, BriefingBeat, Claim, Decision } from '../api/schema'

/** A judge can watch the complete brief without the browser changing the canonical server timing. */
export const TARGET_PLAYBACK_SECONDS = 24

export interface PlaybackState {
  elapsed: number
  playing: boolean
}

export type PlaybackAction =
  | { type: 'play'; runtime: number }
  | { type: 'pause' }
  | { type: 'restart' }
  | { type: 'reset' }
  | { type: 'tick'; elapsed: number; runtime: number }

export const initialPlaybackState: PlaybackState = { elapsed: 0, playing: false }

/** Pure clock transitions keep pause and restart deterministic and directly testable. */
export function playbackReducer(state: PlaybackState, action: PlaybackAction): PlaybackState {
  switch (action.type) {
    case 'play':
      return {
        elapsed: state.elapsed >= action.runtime ? 0 : state.elapsed,
        playing: action.runtime > 0,
      }
    case 'pause':
      return state.playing ? { ...state, playing: false } : state
    case 'restart':
      return { elapsed: 0, playing: true }
    case 'reset':
      return initialPlaybackState
    case 'tick':
      if (!state.playing) {
        return state
      }
      if (action.elapsed >= action.runtime) {
        return { elapsed: action.runtime, playing: false }
      }
      return { elapsed: Math.max(0, action.elapsed), playing: true }
  }
}

export function playbackScale(totalSeconds: number): number {
  return totalSeconds > 0 ? TARGET_PLAYBACK_SECONDS / totalSeconds : 1
}

export function activeBeatIndex(
  beats: readonly BriefingBeat[],
  elapsedSeconds: number,
  scale: number,
): number {
  if (beats.length === 0) {
    return 0
  }

  const canonicalTime = elapsedSeconds / Math.max(scale, Number.EPSILON)
  const found = beats.findIndex(
    (beat) => canonicalTime < beat.startSeconds + beat.durationSeconds,
  )
  return found < 0 ? beats.length - 1 : found
}

export function resolveBeatClaims(
  claimIds: readonly string[],
  claims: readonly Claim[],
): { claims: Claim[]; unknownIds: string[] } {
  const byId = new Map(claims.map((claim) => [claim.id, claim]))
  const resolved: Claim[] = []
  const unknownIds: string[] = []

  for (const id of claimIds) {
    const claim = byId.get(id)
    if (claim) {
      resolved.push(claim)
    } else {
      unknownIds.push(id)
    }
  }

  return { claims: resolved, unknownIds }
}

/**
 * A challenged decision carries two historical claims which were verified in the immediately
 * preceding run. They remain separate from the current run's published eight-claim tally, but are
 * part of the evidence available to the current before/after briefing.
 */
export function decisionEvidenceClaims(decision: Decision): Claim[] {
  return decision.delta
    ? [
        ...decision.verification.claims,
        decision.delta.previousOnTimeClaim,
        decision.delta.previousAtRiskClaim,
      ]
    : [...decision.verification.claims]
}

/** Fingerprint every piece of evidence or situation text capable of changing the film. */
export function decisionFilmStateToken(decision: Decision): string {
  return JSON.stringify({
    domain: decision.incident.vocabulary.domainKey,
    incident: {
      id: decision.incident.id,
      title: decision.incident.title,
      narrative: decision.incident.narrative,
      deadline: decision.incident.departureDeadline,
      resources: decision.incident.chargePoints.map((resource) => [
        resource.id,
        resource.ratedPowerKw,
        resource.isOperational,
        resource.faultCode,
      ]),
    },
    seed: decision.seed,
    trialCount: decision.trialCount,
    recommendation: decision.comparison.recommendedPlanId,
    assumption: decision.assumption?.recognised
      ? [
          decision.assumption.kind,
          decision.assumption.value,
          decision.assumption.question,
        ]
      : null,
    claims: decisionEvidenceClaims(decision).map((claim) => [
      claim.id,
      claim.value,
      claim.displayValue,
      claim.sourceField,
      claim.verified,
    ]),
  })
}

/**
 * The export endpoint recomputes the brief. Refuse to put it on screen unless that recomputation
 * agrees with the Decision response already visible in the app.
 */
export function briefingMatchesDecision(briefing: Briefing, decision: Decision): boolean {
  const evidence = decisionEvidenceClaims(decision)
  if (
    briefing.domainKey !== decision.incident.vocabulary.domainKey ||
    briefing.title !== decision.incident.title ||
    briefing.seed !== decision.seed ||
    briefing.trialCount !== decision.trialCount ||
    briefing.recommendedPlanId !== decision.comparison.recommendedPlanId ||
    briefing.unsupportedNumbers !== decision.verification.unsupportedNumbers ||
    briefing.verifiedClaims !== evidence.filter((claim) => claim.verified).length ||
    briefing.claims.length !== evidence.length
  ) {
    return false
  }

  const currentClaims = new Map(evidence.map((claim) => [claim.id, claim]))
  const claimsMatch = briefing.claims.every((claim) => {
    const current = currentClaims.get(claim.id)
    return (
      current != null &&
      current.label === claim.label &&
      current.value === claim.value &&
      current.displayValue === claim.displayValue &&
      current.unit === claim.unit &&
      current.sourceField === claim.sourceField &&
      current.calculationMethod === claim.calculationMethod &&
      current.simulationSeed === claim.simulationSeed &&
      current.trialCount === claim.trialCount &&
      current.verified === claim.verified
    )
  })
  if (!claimsMatch || briefing.plans.length !== decision.outcomes.length) {
    return false
  }

  return briefing.plans.every((plan) => {
    const current = decision.outcomes.find((outcome) => outcome.planId === plan.planId)
    return (
      current != null &&
      plan.planName === current.planName &&
      plan.recommended ===
        (current.planId === decision.comparison.recommendedPlanId) &&
      plan.onTimePct === current.onTimeDeparturePct &&
      plan.atRiskCount === current.vehiclesAtRisk &&
      plan.riskLevel === current.riskLevel &&
      plan.units.length === current.vehicles.length
    )
  })
}

/**
 * Claim source fields still use the fleet-shaped internal model. Present a stable, domain-neutral
 * path in the cross-domain film while retaining the exact raw field in the element title.
 */
export function displaySourceField(sourceField: string): string {
  return sourceField
    .replaceAll('onTimeDeparturePct', 'onTimeOutcomePct')
    .replaceAll('vehiclesAtRisk', 'unitsAtRisk')
    .replaceAll('expectedUnmetEnergyKwh', 'expectedShortfall')
    .replaceAll('costPerDepartureSecuredGbp', 'costPerUnitSecuredGbp')
}

/** Claim IDs are stable API keys; the visible alias avoids leaking a fleet-only internal noun. */
export function displayClaimId(claimId: string): string {
  return claimId.replaceAll('unmet-energy', 'shortfall')
}
