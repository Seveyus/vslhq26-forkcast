import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, ForkcastError } from '../api/client'
import type { Briefing, BriefingBeat, Claim } from '../api/schema'

interface Props {
  scenario?: string
  /** The counterfactual currently applied, so the film is briefed on the same state. */
  question?: string
  /** Bumped whenever the decision changes, to invalidate a stale film. */
  stateToken: string
}

/** Browser playback is a compressed read of the render timeline, not a different one. */
const TARGET_SECONDS = 26

/**
 * The decision film: the verified state, played back.
 *
 * The canvas is for exploring a decision; this is for communicating one. Both read the same
 * verified state, and this component renders exactly what `/api/briefing/export` returns — the
 * beats, their captions, and the claims each beat is permitted to show. It composes no figure of
 * its own, which is what makes the claim on the tin true: the film is generated from the verified
 * decision state, not from a script.
 */
export function DecisionFilmPlayer({ scenario, question, stateToken }: Props) {
  const [briefing, setBriefing] = useState<Briefing | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [stale, setStale] = useState(false)
  const [playing, setPlaying] = useState(false)
  const [elapsed, setElapsed] = useState(0)

  const loadedToken = useRef<string | null>(null)
  const frame = useRef(0)
  const startedAt = useRef(0)

  const scale = briefing && briefing.totalSeconds > 0 ? TARGET_SECONDS / briefing.totalSeconds : 1
  const runtime = briefing ? briefing.totalSeconds * scale : 0

  // A new decision makes any loaded film a description of a world that no longer applies.
  useEffect(() => {
    if (loadedToken.current !== null && loadedToken.current !== stateToken) {
      setStale(true)
      setPlaying(false)
      setElapsed(0)
    }
  }, [stateToken])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const next = await api.briefing(scenario, question)
      setBriefing(next)
      loadedToken.current = stateToken
      setStale(false)
      setElapsed(0)
      return next
    } catch (cause: unknown) {
      setError(
        cause instanceof ForkcastError ? cause.message : 'The decision brief could not be loaded.',
      )
      return null
    } finally {
      setLoading(false)
    }
  }, [scenario, question, stateToken])

  const play = useCallback(async () => {
    const current = stale || !briefing ? await load() : briefing
    if (current) {
      setElapsed(0)
      setPlaying(true)
    }
  }, [briefing, stale, load])

  useEffect(() => {
    if (!playing || runtime <= 0) {
      return
    }

    startedAt.current = performance.now() - elapsed * 1000

    const tick = () => {
      const next = (performance.now() - startedAt.current) / 1000
      if (next >= runtime) {
        setElapsed(runtime)
        setPlaying(false)
        return
      }
      setElapsed(next)
      frame.current = requestAnimationFrame(tick)
    }

    frame.current = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame.current)
    // elapsed is the resume point, deliberately not a dependency.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [playing, runtime])

  const beats = briefing?.beats ?? []
  const activeIndex = useMemo(() => {
    if (beats.length === 0) {
      return 0
    }
    const at = elapsed / Math.max(scale, 1e-6)
    const found = beats.findIndex((beat) => at < beat.startSeconds + beat.durationSeconds)
    return found < 0 ? beats.length - 1 : found
  }, [beats, elapsed, scale])

  const active = beats[activeIndex] as BriefingBeat | undefined
  const claimsById = useMemo(
    () => new Map((briefing?.claims ?? []).map((claim) => [claim.id, claim])),
    [briefing],
  )
  const activeClaims = (active?.claimIds ?? [])
    .map((id) => claimsById.get(id))
    .filter((claim): claim is Claim => claim != null)

  return (
    <section className="panel film" id="film">
      <header className="panel__head">
        <div>
          <p className="eyebrow">Generated from verified state</p>
          <h2>Decision film</h2>
          <p className="panel__sub">
            Watch both operational futures unfold. Every figure shown below is linked to the same
            evidence ledger used by the recommendation.
          </p>
        </div>
        <div className="film__controls">
          <button
            type="button"
            className="button button--primary"
            onClick={() => void play()}
            disabled={loading}
          >
            {loading ? 'Briefing…' : playing ? 'Restart' : 'Play decision film'}
          </button>
          {briefing && (
            <button
              type="button"
              className="button button--ghost"
              onClick={() => setPlaying((was) => !was)}
              disabled={loading || elapsed >= runtime}
            >
              {playing ? 'Pause' : 'Resume'}
            </button>
          )}
        </div>
      </header>

      {error && (
        <p className="challenge__miss" role="alert">
          {error}
        </p>
      )}

      {stale && (
        <p className="film__stale">
          Decision state changed — film regenerated from updated evidence on next play.
        </p>
      )}

      {!briefing && !loading && !error && (
        <p className="film__idle">
          The film is composed from the decision currently on screen: its situation, both futures,
          the recommendation and the evidence behind each scene. Press play to brief it.
        </p>
      )}

      {briefing && (
        <>
          <div className="film__stage" aria-live="polite">
            <div className="film__chrome">
              <span className="film__domain">{briefing.domainLabel}</span>
              <span className="film__badge">
                <em />
                Verified playback
              </span>
            </div>

            <div className={`scene scene--${active?.kind ?? 'situation'}`} key={active?.id}>
              <p className="scene__kicker">{active?.heading}</p>
              <p className="scene__caption">{active?.caption}</p>

              {activeClaims.length > 0 && (
                <ul className="scene__figures">
                  {activeClaims.slice(0, 4).map((claim) => (
                    <li key={claim.id}>
                      <strong>{claim.displayValue}</strong>
                      <span>{claim.label}</span>
                    </li>
                  ))}
                </ul>
              )}

              {active?.kind === 'futures' && (
                <div className="scene__boards">
                  {briefing.plans.map((plan) => (
                    <div
                      key={plan.planId}
                      className={`scene__board${plan.recommended ? ' is-recommended' : ''}`}
                    >
                      <span className="scene__board-name">{plan.planName}</span>
                      <ul className="scene__units">
                        {plan.units.map((unit) => (
                          <li
                            key={unit.id}
                            className={unit.isPriority ? 'is-priority' : undefined}
                            style={{ opacity: 0.25 + 0.75 * unit.onTimeProbability }}
                            data-risk={unit.atRisk ? 'true' : 'false'}
                          />
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              )}

              {active?.kind === 'situation' && (
                <ul className="scene__resources">
                  {briefing.resources.map((resource) => (
                    <li key={resource.id} data-down={resource.operational ? 'false' : 'true'}>
                      {resource.id}
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className="film__timeline">
              <div className="film__progress" style={{ width: `${(elapsed / Math.max(runtime, 1e-6)) * 100}%` }} />
              <ul className="film__beats">
                {beats.map((beat, index) => (
                  <li
                    key={beat.id}
                    className={index === activeIndex ? 'is-active' : undefined}
                    style={{ flexGrow: beat.durationSeconds }}
                    title={beat.heading}
                  >
                    <span>{beat.heading}</span>
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <aside className="rail">
            <header>
              <span className="future__constraint-label">Evidence rail</span>
              <p className="rail__scene">{active?.heading}</p>
              <p className="rail__note">Current scene constrained by the evidence ledger.</p>
            </header>

            {activeClaims.length === 0 ? (
              <p className="rail__empty">No numerical claim required for this scene.</p>
            ) : (
              <ul className="rail__claims">
                {activeClaims.map((claim) => (
                  <li key={claim.id}>
                    <div className="rail__claim-head">
                      <code>{claim.id}</code>
                      <strong>{claim.displayValue}</strong>
                    </div>
                    <span className="rail__source">{claim.sourceField}</span>
                    <span className={claim.verified ? 'is-good' : 'is-bad'}>
                      {claim.verified ? 'verified' : 'unverified'}
                    </span>
                  </li>
                ))}
              </ul>
            )}

            <footer className="rail__foot">
              seed {briefing.seed} · {briefing.trialCount} trials per plan ·{' '}
              {briefing.verifiedClaims} claims
              {briefing.counterfactualLabel && (
                <span className="rail__counterfactual">{briefing.counterfactualLabel}</span>
              )}
            </footer>
          </aside>
        </>
      )}
    </section>
  )
}
