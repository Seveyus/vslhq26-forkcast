import {
  useCallback,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
} from 'react'
import { api } from '../api/client'
import type { Briefing, Decision } from '../api/schema'
import {
  TARGET_PLAYBACK_SECONDS,
  activeBeatIndex,
  briefingMatchesDecision,
  decisionEvidenceClaims,
  decisionFilmStateToken,
  displayClaimId,
  displaySourceField,
  initialPlaybackState,
  playbackReducer,
  playbackScale,
  resolveBeatClaims,
} from './decisionFilm'

interface Props {
  decision: Decision
  scenario?: string
  /** The recognised counterfactual currently applied to the Decision response. */
  question?: string
  /** The same incident text used to produce the Decision response. */
  narrative: string
  /** Stops the old film as soon as a counterfactual request begins. */
  decisionPending: boolean
}

/**
 * The decision film is a projection of the verified Decision response already on screen. It
 * renders the server's beats and captions, but resolves every scene boundary against the current
 * Decision evidence before it presents that scene as verified.
 */
export function DecisionFilmPlayer({
  decision,
  scenario,
  question,
  narrative,
  decisionPending,
}: Props) {
  const [briefing, setBriefing] = useState<Briefing | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [regenerated, setRegenerated] = useState(false)
  const [clock, dispatch] = useReducer(playbackReducer, initialPlaybackState)

  const loadedToken = useRef<string | null>(null)
  const latestToken = useRef('')
  const requestController = useRef<AbortController | null>(null)
  const frame = useRef(0)
  const startedAt = useRef(0)

  const evidence = useMemo(() => decisionEvidenceClaims(decision), [decision])
  const stateToken = useMemo(() => decisionFilmStateToken(decision), [decision])
  const requestToken = `${scenario ?? ''}\u0000${question ?? ''}\u0000${stateToken}`
  latestToken.current = requestToken

  const scale = briefing ? playbackScale(briefing.totalSeconds) : 1
  const runtime = briefing ? briefing.totalSeconds * scale : TARGET_PLAYBACK_SECONDS

  const load = useCallback(async () => {
    requestController.current?.abort()
    const controller = new AbortController()
    requestController.current = controller
    const expectedToken = requestToken

    setLoading(true)
    setError(null)

    try {
      const next = await api.briefing(scenario, question, narrative, controller.signal)
      if (controller.signal.aborted || latestToken.current !== expectedToken) {
        return null
      }
      if (!briefingMatchesDecision(next, decision)) {
        throw new Error('Briefing does not match the current verified decision.')
      }

      setBriefing(next)
      loadedToken.current = expectedToken
      dispatch({ type: 'reset' })
      return next
    } catch {
      if (controller.signal.aborted || latestToken.current !== expectedToken) {
        return null
      }
      setBriefing(null)
      setError('The verified briefing could not be loaded.')
      return null
    } finally {
      if (requestController.current === controller) {
        requestController.current = null
        setLoading(false)
      }
    }
  }, [decision, narrative, question, requestToken, scenario])

  // A completed decision change immediately removes the previous film. If the user had already
  // prepared a film, prepare its evidence-synchronised replacement without autoplaying it.
  useEffect(() => {
    const hadRequestInFlight = requestController.current !== null
    requestController.current?.abort()
    requestController.current = null

    const previous = loadedToken.current
    if (
      hadRequestInFlight ||
      (previous !== null && previous !== requestToken)
    ) {
      setBriefing(null)
      setError(null)
      setLoading(false)
      setRegenerated(true)
      dispatch({ type: 'reset' })
      void load()
    }
  }, [load, requestToken])

  useEffect(() => {
    if (decisionPending) {
      dispatch({ type: 'pause' })
    }
  }, [decisionPending])

  useEffect(
    () => () => {
      requestController.current?.abort()
      requestController.current = null
      cancelAnimationFrame(frame.current)
    },
    [],
  )

  useEffect(() => {
    if (!clock.playing || runtime <= 0) {
      return
    }

    startedAt.current = performance.now() - clock.elapsed * 1000

    const tick = () => {
      const next = (performance.now() - startedAt.current) / 1000
      dispatch({ type: 'tick', elapsed: next, runtime })
      if (next < runtime) {
        frame.current = requestAnimationFrame(tick)
      }
    }

    frame.current = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame.current)
    // clock.elapsed is the resume point, not a dependency: every tick updates it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clock.playing, runtime])

  const start = useCallback(async () => {
    const current = briefing ?? (await load())
    if (current && latestToken.current === requestToken) {
      dispatch({
        type: 'play',
        runtime: current.totalSeconds * playbackScale(current.totalSeconds),
      })
    }
  }, [briefing, load, requestToken])

  const restart = useCallback(async () => {
    const current = briefing ?? (await load())
    if (current && latestToken.current === requestToken) {
      // Restart while already playing must also move the RAF wall-clock origin.
      startedAt.current = performance.now()
      dispatch({ type: 'restart' })
    }
  }, [briefing, load, requestToken])

  const beats = useMemo(() => briefing?.beats ?? [], [briefing])
  const activeIndex = useMemo(
    () => activeBeatIndex(beats, clock.elapsed, scale),
    [beats, clock.elapsed, scale],
  )
  const active = beats[activeIndex]
  const resolution = useMemo(
    () => resolveBeatClaims(active?.claimIds ?? [], evidence),
    [active, evidence],
  )
  const unverified = resolution.claims.filter((claim) => !claim.verified)
  const sceneBlocked = resolution.unknownIds.length > 0 || unverified.length > 0

  useEffect(() => {
    if (import.meta.env.DEV && resolution.unknownIds.length > 0) {
      console.warn(
        `Decision Film blocked unresolved claim ids: ${resolution.unknownIds.join(', ')}`,
      )
    }
  }, [resolution.unknownIds])

  const progress = Math.min(100, (clock.elapsed / Math.max(runtime, Number.EPSILON)) * 100)
  const playLabel = loading
    ? 'Preparing verified playback…'
    : clock.playing
      ? 'Playing verified future'
      : briefing && clock.elapsed > 0 && clock.elapsed < runtime
        ? 'Resume'
        : clock.elapsed >= runtime
          ? 'Play again'
          : 'Play verified future'
  const showBoards =
    !sceneBlocked &&
    (active?.kind === 'futures' ||
      active?.kind === 'risk' ||
      active?.kind === 'recommendation')
  const riskScene = active?.id === 'risk' || active?.kind === 'risk'

  return (
    <section className="panel film" id="film">
      <header className="panel__head">
        <div>
          <p className="eyebrow">Decision film</p>
          <h2>Watch the decision unfold</h2>
          <p className="panel__sub">
            A visual briefing generated from the current simulation and constrained by its evidence
            ledger.
          </p>
        </div>
        <div className="film__controls" aria-label="Decision film controls">
          <button
            type="button"
            className="button button--primary"
            onClick={() => void start()}
            disabled={loading || decisionPending || clock.playing}
          >
            {playLabel}
          </button>
          {briefing && (
            <>
              <button
                type="button"
                className="button button--ghost"
                onClick={() => dispatch({ type: 'pause' })}
                disabled={loading || decisionPending || !clock.playing}
              >
                Pause
              </button>
              <button
                type="button"
                className="button button--ghost"
                onClick={() => void restart()}
                disabled={loading || decisionPending}
              >
                Restart
              </button>
            </>
          )}
        </div>
      </header>

      {regenerated && (
        <p className="film__stale" aria-live="polite">
          Decision state changed — playback regenerated from updated evidence.
        </p>
      )}

      {error && (
        <div className="film__error" role="alert">
          <p>{error}</p>
          <button
            type="button"
            className="button button--ghost"
            onClick={() => void load()}
            disabled={loading || decisionPending}
          >
            Retry
          </button>
        </div>
      )}

      {loading && !briefing && (
        <p className="film__idle" aria-live="polite">
          Preparing verified playback…
        </p>
      )}

      {!briefing && !loading && !error && (
        <p className="film__idle">
          Change the assumption, and the film changes with the evidence. Playback starts only when
          you choose it.
        </p>
      )}

      {briefing && (
        <>
          <div className="film__stage">
            <div className="film__chrome">
              <span className="film__domain">{briefing.domainLabel}</span>
              <span className="film__badge">
                <em />
                Verified playback
              </span>
            </div>

            <div
              className={`scene scene--${active?.kind ?? 'situation'}${
                sceneBlocked ? ' scene--blocked' : ''
              }`}
              key={active?.id}
              aria-live="polite"
            >
              <p className="scene__kicker">{active?.heading}</p>
              {active?.kind === 'situation' && (
                <h3 className="scene__title">{briefing.title}</h3>
              )}
              <p className="scene__caption">
                {sceneBlocked
                  ? 'This scene cannot be presented as verified because its evidence reference is unresolved.'
                  : active?.caption}
              </p>

              {!sceneBlocked && resolution.claims.length > 0 && (
                <ul className="scene__figures">
                  {resolution.claims.slice(0, 4).map((claim) => (
                    <li key={claim.id}>
                      <strong>{claim.displayValue}</strong>
                      <span>{claim.label}</span>
                    </li>
                  ))}
                </ul>
              )}

              {showBoards && (
                <div className="scene__boards">
                  {briefing.plans.map((plan) => (
                    <div
                      key={plan.planId}
                      className={`scene__board${plan.recommended ? ' is-recommended' : ''}`}
                    >
                      <span className="scene__board-name">
                        {plan.planName}
                        {plan.recommended && (
                          <em className="scene__recommended">Recommended</em>
                        )}
                      </span>
                      <ul className="scene__units" aria-hidden="true">
                        {plan.units.map((unit) => (
                          <li
                            key={unit.id}
                            className={unit.isPriority ? 'is-priority' : undefined}
                            style={{
                              opacity: riskScene
                                ? unit.atRisk
                                  ? 1
                                  : 0.22
                                : 0.35 + 0.65 * unit.onTimeProbability,
                            }}
                            data-risk={unit.atRisk ? 'true' : 'false'}
                          />
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              )}

              {!sceneBlocked && active?.kind === 'situation' && (
                <ul className="scene__resources">
                  {briefing.resources.map((resource) => (
                    <li
                      key={resource.id}
                      data-down={resource.operational ? 'false' : 'true'}
                      aria-label={`${resource.id}, ${
                        resource.operational ? 'operational' : 'offline'
                      }`}
                    >
                      {resource.id}
                    </li>
                  ))}
                </ul>
              )}

              {!sceneBlocked && active?.kind === 'counterfactual' && (
                <p className="scene__state">Regenerated from the updated verified decision</p>
              )}
            </div>

            <div className="film__timeline">
              <div
                className="film__progress"
                style={{ width: `${progress}%` }}
                role="progressbar"
                aria-label="Decision film progress"
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={Math.round(progress)}
              />
              <ol className="film__beats" aria-label="Decision film beats">
                {beats.map((beat, index) => (
                  <li
                    key={beat.id}
                    className={index === activeIndex ? 'is-active' : undefined}
                    style={{ flexGrow: beat.durationSeconds }}
                    title={beat.heading}
                    aria-current={index === activeIndex ? 'step' : undefined}
                  >
                    <span>{beat.heading}</span>
                  </li>
                ))}
              </ol>
            </div>
          </div>

          <aside className={`rail${active?.kind === 'evidence' ? ' rail--focus' : ''}`}>
            <header>
              <span className="future__constraint-label">Evidence rail</span>
              <p className="rail__scene">{active?.heading}</p>
              <p className="rail__note">Current scene constrained by the evidence ledger.</p>
            </header>

            {resolution.unknownIds.length > 0 && (
              <p className="rail__warning" role="alert">
                Evidence reference unavailable:{' '}
                <code>{resolution.unknownIds.join(', ')}</code>. No unresolved figure is presented
                as verified.
              </p>
            )}

            {unverified.length > 0 && (
              <p className="rail__warning" role="alert">
                This scene contains unverified evidence and has been blocked.
              </p>
            )}

            {!sceneBlocked && resolution.claims.length === 0 ? (
              <p className="rail__empty">No numerical claim required for this scene.</p>
            ) : (
              <ul className="rail__claims">
                {resolution.claims.map((claim) => (
                  <li key={claim.id} className={claim.verified ? undefined : 'is-unverified'}>
                    <div className="rail__claim-head">
                      <code title={claim.id}>{displayClaimId(claim.id)}</code>
                      <strong>{claim.displayValue}</strong>
                    </div>
                    <span className="rail__label">{claim.label}</span>
                    <span className="rail__source" title={claim.sourceField}>
                      source · {displaySourceField(claim.sourceField)}
                    </span>
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
