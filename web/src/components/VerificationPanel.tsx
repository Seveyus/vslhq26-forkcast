import { useState } from 'react'
import type { Decision } from '../api/schema'

interface Props {
  decision: Decision
}

export function VerificationPanel({ decision }: Props) {
  const [openClaim, setOpenClaim] = useState<string | null>(null)
  const { verification } = decision
  const clean = verification.unsupportedNumbers === 0 && verification.allClaimsVerified

  return (
    <section className="panel verify" id="verification">
      <header className="panel__head">
        <div>
          <h2>Evidence ledger</h2>
          <p className="panel__sub">
            Every number on this page, with the simulation field it came from. The model writes the
            explanation; it is never allowed to invent the figures.
          </p>
        </div>
        <div className={`stamp${clean ? ' stamp--clean' : ' stamp--dirty'}`}>
          {clean ? 'Verified' : 'Rejected'}
        </div>
      </header>

      <div className="verify__counts">
        <div className="count">
          <strong>{verification.verifiedClaims}</strong>
          <span>verified claims</span>
        </div>
        <div className={`count${verification.unsupportedNumbers > 0 ? ' count--bad' : ''}`}>
          <strong>{verification.unsupportedNumbers}</strong>
          <span>unsupported numbers</span>
        </div>
        <div className="count">
          <strong>{verification.simulationSeed}</strong>
          <span>simulation seed</span>
        </div>
        <div className="count">
          <strong>{verification.trialCount}</strong>
          <span>trials per plan</span>
        </div>
      </div>

      {verification.unsupported.length > 0 && (
        <div className="verify__rejected">
          <h3>Discarded from the written explanation</h3>
          <ul>
            {verification.unsupported.map((item) => (
              <li key={`${item.token}-${item.context}`}>
                <code>{item.token}</code>
                <span>{item.context}</span>
              </li>
            ))}
          </ul>
          <p>
            No claim supports these figures, so the generated wording was discarded and the
            deterministic summary is shown instead.
          </p>
        </div>
      )}

      <ul className="claims">
        {verification.claims.map((claim) => {
          const open = openClaim === claim.id
          return (
            <li key={claim.id} className={`claim${open ? ' is-open' : ''}`}>
              <button
                type="button"
                className="claim__row"
                aria-expanded={open}
                onClick={() => setOpenClaim(open ? null : claim.id)}
              >
                <span className={`claim__tick${claim.verified ? ' is-verified' : ''}`} aria-hidden="true" />
                <span className="claim__label">{claim.label}</span>
                <span className="claim__value">{claim.displayValue}</span>
                <span className="claim__chevron" aria-hidden="true" />
              </button>

              {open && (
                <dl className="claim__detail">
                  <div>
                    <dt>Source field</dt>
                    <dd>
                      <code>{claim.sourceField}</code>
                    </dd>
                  </div>
                  <div>
                    <dt>Calculation</dt>
                    <dd>{claim.calculationMethod}</dd>
                  </div>
                  <div>
                    <dt>Exact value</dt>
                    <dd>
                      {claim.value} {claim.unit}
                    </dd>
                  </div>
                  <div>
                    <dt>Reproduce with</dt>
                    <dd>
                      seed {claim.simulationSeed}, {claim.trialCount} trials
                    </dd>
                  </div>
                  <div>
                    <dt>Status</dt>
                    <dd className={claim.verified ? 'is-good' : 'is-bad'}>
                      {claim.verified
                        ? 'Round-trips to the simulation output'
                        : 'Does not match its source field'}
                    </dd>
                  </div>
                </dl>
              )}
            </li>
          )
        })}
      </ul>
    </section>
  )
}
