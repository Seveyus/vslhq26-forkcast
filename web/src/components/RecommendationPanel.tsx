import type { Decision } from '../api/schema'
import { money, percent, riskTone, signed } from '../lib/format'

interface Props {
  decision: Decision
}

export function RecommendationPanel({ decision }: Props) {
  const { recommendation, comparison, verification } = decision
  const generated = verification.narrativeSource !== 'deterministic'

  return (
    <section className="panel recommend" id="recommendation">
      <header className="panel__head">
        <div>
          <p className="eyebrow">Decision brief</p>
          <h2>{recommendation.headline}</h2>
        </div>
        <div className={`pill pill--${riskTone(recommendation.residualRisk)} pill--lg`}>
          {recommendation.residualRisk} residual risk
        </div>
      </header>

      <div className="recommend__grid">
        <div className="recommend__main">
          <p className="recommend__summary">{decision.executiveSummary}</p>
          <p className="recommend__provenance">
            {generated
              ? `Written by ${decision.intelligence.provider} from the verified claims, then checked against them.`
              : 'Written by the decision engine directly from the verified claims.'}
          </p>

          <h3>What the yard does</h3>
          <ol className="recommend__actions">
            {recommendation.actions.map((action) => (
              <li key={action}>{action}</li>
            ))}
          </ol>
        </div>

        <aside className="recommend__side">
          <div className="swing">
            <span>On-time departures</span>
            <strong>{signed(comparison.onTimeImprovementPp)} pp</strong>
            <small>
              {percent(decision.outcomes[0].onTimeDeparturePct)} →{' '}
              {percent(decision.outcomes[1].onTimeDeparturePct)}
            </small>
          </div>
          <div className="swing">
            <span>Vehicles taken out of risk</span>
            <strong>{comparison.vehiclesAtRiskAvoided}</strong>
            <small>of {decision.incident.vehicleCount} in the fleet</small>
          </div>
          <div className="swing">
            <span>Net cost of acting</span>
            <strong>{money(comparison.additionalCostGbp)}</strong>
            <small>
              intervention less the metered energy it saves, or{' '}
              {money(comparison.costPerDepartureSecuredGbp)} per departure secured
            </small>
          </div>

          <div className="rule">
            <span>Decision rule</span>
            <p>{recommendation.decisionRule}</p>
          </div>
        </aside>
      </div>

      <ul className="evidence">
        {recommendation.rationale.map((point) => (
          <li key={point.text}>
            <span className="evidence__text">{point.text}</span>
            <span className="evidence__refs">
              {point.claimIds.map((id) => (
                <code key={id}>{id}</code>
              ))}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}
