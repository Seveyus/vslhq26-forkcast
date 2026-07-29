import type { Incident, Outcome, Plan } from '../api/schema'
import { money, percent, riskTone } from '../lib/format'
import { useAnimatedNumber } from '../lib/useAnimatedNumber'
import { Timeline } from './Timeline'

interface Props {
  label: string
  plan: Plan
  outcome: Outcome
  incident: Incident
  recommended: boolean
  peakKw: number
}

export function FuturePanel({ label, plan, outcome, incident, recommended, peakKw }: Props) {
  const onTime = useAnimatedNumber(outcome.onTimeDeparturePct)
  const atRisk = useAnimatedNumber(outcome.vehiclesAtRisk)
  const tone = riskTone(outcome.riskLevel)
  const words = incident.vocabulary

  const spread = Math.max(1, outcome.onTimeDeparturePctP95 - outcome.onTimeDeparturePctP5)

  return (
    <article className={`future${recommended ? ' future--recommended' : ''}`}>
      <header className="future__head">
        <div className="future__label">
          <span className="future__letter">{label}</span>
          <div>
            <h3>{outcome.planName}</h3>
            <p>{plan.description}</p>
          </div>
        </div>
        {recommended && <span className="badge badge--recommended">Recommended</span>}
      </header>

      <div className="future__headline">
        <div className="future__figure">
          <strong>{percent(onTime)}</strong>
          <span>{words.onTimeMetricLabel}</span>
        </div>
        <div className={`pill pill--${tone}`}>{outcome.riskLevel} risk</div>
      </div>

      <div
        className="range"
        title={`90% of simulated nights land between ${percent(
          outcome.onTimeDeparturePctP5,
        )} and ${percent(outcome.onTimeDeparturePctP95)}`}
      >
        <div className="range__track">
          <div
            className="range__band"
            style={{
              left: `${outcome.onTimeDeparturePctP5}%`,
              width: `${spread}%`,
            }}
          />
          <div className="range__marker" style={{ left: `${outcome.onTimeDeparturePct}%` }} />
        </div>
        <div className="range__labels">
          <span>{percent(outcome.onTimeDeparturePctP5, 0)}</span>
          <span>90% of simulated nights</span>
          <span>{percent(outcome.onTimeDeparturePctP95, 0)}</span>
        </div>
      </div>

      <dl className="metrics">
        <div className="metric">
          <dt>{words.unitPlural} at risk</dt>
          <dd className={outcome.vehiclesAtRisk > 2 ? 'is-bad' : 'is-good'}>
            {Math.round(atRisk)}
            <small> of {incident.vehicleCount}</small>
          </dd>
        </div>
        <div className="metric">
          <dt>Intervention cost</dt>
          <dd>
            {money(outcome.additionalCostGbp)}
            <small className="metric__note">call-out and {words.bufferLabel}</small>
          </dd>
        </div>
        <div className="metric">
          <dt>{words.shortfallLabel}</dt>
          <dd>
            {outcome.expectedUnmetEnergyKwh.toFixed(
              outcome.expectedUnmetEnergyKwh >= 100 ? 0 : 1,
            )}{' '}
            <small>{words.levelUnit}</small>
          </dd>
        </div>
        <div className="metric">
          <dt>{words.priorityLabelPlural}</dt>
          <dd className={outcome.priorityOnTimeDeparturePct >= 99 ? 'is-good' : 'is-bad'}>
            {percent(outcome.priorityOnTimeDeparturePct, 0)}
          </dd>
        </div>
      </dl>

      <Timeline
        incident={incident}
        outcome={outcome}
        recommended={recommended}
        peakKw={peakKw}
      />

      <footer className="future__constraint">
        <span className="future__constraint-label">Critical constraint</span>
        <p>{outcome.criticalConstraint}</p>
      </footer>
    </article>
  )
}
