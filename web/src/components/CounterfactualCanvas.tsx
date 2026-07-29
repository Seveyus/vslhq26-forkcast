import type { Decision, Outcome, Vocabulary } from '../api/schema'
import { percent } from '../lib/format'

interface Props {
  decision: Decision
}

/**
 * One living surface for the whole operational state.
 *
 * Every tile is a real unit and every colour is that unit's on-time probability across the 500
 * trials — not a decoration. Because it renders straight from the decision already in hand, a
 * counterfactual ripples across the whole board the moment the rerun lands: tiles recolour, the
 * at-risk count moves, the resource strip changes. That is the difference between a dashboard that
 * reports a number and a surface you can reason on.
 */
export function CounterfactualCanvas({ decision }: Props) {
  const words = decision.incident.vocabulary
  const buffered = decision.plans.find((plan) => plan.mobileBuffer != null)

  return (
    <section className="panel canvas" id="canvas">
      <header className="panel__head">
        <div>
          <p className="eyebrow">Counterfactual canvas</p>
          <h2>The whole operation, one surface</h2>
          <p className="panel__sub">
            One tile per {words.unitSingular}, shaded by how often it met its requirement across{' '}
            {decision.trialCount} simulated runs. Change an assumption below and the board moves
            with it.
          </p>
        </div>
        {decision.assumption?.recognised && (
          <span className="pill pill--warn pill--lg">{decision.assumption.label}</span>
        )}
      </header>

      <div className="canvas__resources">
        <span className="canvas__strip-label">
          {decision.incident.totalChargePointCount} {words.resourcePlural}
        </span>
        <ul>
          {decision.incident.chargePoints.map((point) => (
            <li
              key={point.id}
              className={`res${point.isOperational ? '' : ' res--down'}`}
              title={
                point.isOperational
                  ? `${point.id} — ${point.ratedPowerKw} ${words.rateUnit}`
                  : `${point.id} — offline${point.faultCode ? ` (${point.faultCode})` : ''}`
              }
            >
              <span className="res__id">{point.id}</span>
              <span className="res__rate">{point.ratedPowerKw}</span>
            </li>
          ))}
          {buffered?.mobileBuffer &&
            Array.from({ length: buffered.mobileBuffer.outlets }, (_, index) => (
              <li key={`buffer-${index}`} className="res res--buffer" title={words.bufferLabel}>
                <span className="res__id">BUF-{index + 1}</span>
                <span className="res__rate">{buffered.mobileBuffer!.outletPowerKw}</span>
              </li>
            ))}
        </ul>
      </div>

      <div className="canvas__grid">
        {decision.outcomes.map((outcome) => (
          <PlanBoard
            key={outcome.planId}
            outcome={outcome}
            words={words}
            recommended={outcome.planId === decision.comparison.recommendedPlanId}
          />
        ))}
      </div>

      <footer className="canvas__legend">
        <span>
          <em className="swatch swatch--bad" /> misses its {words.deadlineNoun} often
        </span>
        <span>
          <em className="swatch swatch--warn" /> marginal
        </span>
        <span>
          <em className="swatch swatch--good" /> makes it in almost every run
        </span>
        <span>
          <em className="swatch swatch--priority" /> {words.priorityLabelPlural}
        </span>
      </footer>
    </section>
  )
}

function PlanBoard({
  outcome,
  words,
  recommended,
}: {
  outcome: Outcome
  words: Vocabulary
  recommended: boolean
}) {
  return (
    <article className={`board${recommended ? ' board--recommended' : ''}`}>
      <header className="board__head">
        <div>
          <h3>{outcome.planName}</h3>
          <p>
            {percent(outcome.onTimeDeparturePct)} {words.onTimeMetricLabel} ·{' '}
            {outcome.vehiclesAtRisk} at risk
          </p>
        </div>
        {recommended && <span className="badge badge--recommended">Recommended</span>}
      </header>

      <ul className="board__units">
        {outcome.vehicles.map((unit) => (
          <li
            key={unit.vehicleId}
            className={`unit${unit.isPriorityRoute ? ' unit--priority' : ''}`}
            style={{
              background: shade(unit.onTimeProbability),
              // A washed-out tile leaves dark ink on a dark surface, so the label flips.
              color: unit.onTimeProbability > 0.55 ? '#061020' : 'var(--text)',
            }}
            title={[
              `${unit.vehicleId} · ${unit.route}`,
              `${percent(unit.onTimeProbability * 100)} of runs on time`,
              unit.expectedShortfallKwh > 0.05
                ? `${unit.expectedShortfallKwh.toFixed(1)} ${words.levelUnit} short on average`
                : 'no shortfall',
              `${unit.expectedSlackMinutes >= 0 ? '+' : ''}${unit.expectedSlackMinutes.toFixed(
                0,
              )} min of slack`,
            ].join('\n')}
          >
            <span className="unit__id">{unit.vehicleId.replace(/^[A-Z]+-/, '')}</span>
          </li>
        ))}
      </ul>
    </article>
  )
}

/**
 * Probability to colour. Deliberately steep above 0.9, because that is where the at-risk threshold
 * sits — a unit at 0.85 and one at 0.99 should not look alike.
 */
function shade(probability: number): string {
  const bad = [255, 112, 137]
  const warn = [245, 181, 68]
  const good = [53, 211, 154]

  const [from, to, t] =
    probability < 0.9
      ? [bad, warn, probability / 0.9]
      : [warn, good, (probability - 0.9) / 0.1]

  const channel = (index: number) => Math.round(from[index] + (to[index] - from[index]) * t)
  return `rgba(${channel(0)}, ${channel(1)}, ${channel(2)}, ${0.22 + 0.5 * probability})`
}
