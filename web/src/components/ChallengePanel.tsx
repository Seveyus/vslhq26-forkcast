import { useState } from 'react'
import type { Decision } from '../api/schema'
import { percent, riskTone, signed } from '../lib/format'

interface Props {
  suggested: string
  examples: readonly string[]
  decision: Decision
  pending: boolean
  onChallenge: (question: string) => void
  onReset: () => void
}

export function ChallengePanel({
  suggested,
  examples,
  decision,
  pending,
  onChallenge,
  onReset,
}: Props) {
  const [question, setQuestion] = useState(suggested)
  const { delta, assumption } = decision
  const unrecognised = assumption != null && !assumption.recognised

  return (
    <section className="panel challenge" id="challenge">
      <header className="panel__head">
        <div>
          <h2>Challenge the recommendation</h2>
          <p className="panel__sub">
            Change an assumption and the simulation runs again. Nothing here is a canned answer.
          </p>
        </div>
      </header>

      <form
        className="challenge__form"
        onSubmit={(event) => {
          event.preventDefault()
          if (!pending && question.trim()) {
            onChallenge(question.trim())
          }
        }}
      >
        <input
          type="text"
          value={question}
          aria-label="Assumption to challenge"
          onChange={(event) => setQuestion(event.target.value)}
          placeholder={suggested}
          maxLength={500}
        />
        <button type="submit" className="button button--primary" disabled={pending || !question.trim()}>
          {pending ? 'Re-running…' : 'Test assumption'}
        </button>
      </form>

      <ul className="challenge__examples">
        {examples.map((example) => (
          <li key={example}>
            <button
              type="button"
              className="chip chip--action"
              disabled={pending}
              onClick={() => {
                setQuestion(example)
                onChallenge(example)
              }}
            >
              {example}
            </button>
          </li>
        ))}
      </ul>

      {unrecognised && (
        <p className="challenge__miss">
          Forkcast did not recognise a supported assumption in that question, so nothing was
          changed and the original result still stands. Try one of the examples above.
        </p>
      )}

      {delta && (
        <div className="delta">
          <p className="delta__label">{assumption?.label}</p>

          <div className="delta__figures">
            <div className="delta__figure">
              <span>Before</span>
              <strong>{percent(delta.previousOnTimeDeparturePct)}</strong>
              <small>{delta.previousVehiclesAtRisk} at risk</small>
            </div>

            <div className={`delta__arrow${delta.onTimeChangePp < 0 ? ' is-worse' : ' is-better'}`}>
              <span>{signed(delta.onTimeChangePp)} pp</span>
            </div>

            <div className="delta__figure delta__figure--after">
              <span>After</span>
              <strong className={delta.onTimeChangePp < 0 ? 'is-bad' : 'is-good'}>
                {percent(delta.onTimeDeparturePct)}
              </strong>
              <small>{delta.vehiclesAtRisk} at risk</small>
            </div>

            <div className="delta__risk">
              <span>Residual risk</span>
              <div>
                <em className={`pill pill--${riskTone(delta.previousRiskLevel)}`}>
                  {delta.previousRiskLevel}
                </em>
                <span aria-hidden="true">→</span>
                <em className={`pill pill--${riskTone(delta.riskLevel)}`}>{delta.riskLevel}</em>
              </div>
            </div>
          </div>

          <p className="delta__summary">{delta.summary}</p>

          <p className="delta__verdict">
            {delta.recommendationChanged
              ? 'This changes which response Forkcast recommends.'
              : 'The recommended response does not change, but the margin does.'}
          </p>

          <button type="button" className="button button--ghost" onClick={onReset} disabled={pending}>
            Reset to the original assumptions
          </button>
        </div>
      )}
    </section>
  )
}
