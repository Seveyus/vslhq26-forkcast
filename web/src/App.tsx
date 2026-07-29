import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, ForkcastError } from './api/client'
import type { Decision, DemoIncident } from './api/schema'
import { AgentProgress } from './components/AgentProgress'
import { ArchitectureSection } from './components/ArchitectureSection'
import { ChallengePanel } from './components/ChallengePanel'
import { FuturePanel } from './components/FuturePanel'
import { IncidentCard } from './components/IncidentCard'
import { RecommendationPanel } from './components/RecommendationPanel'
import { VerificationPanel } from './components/VerificationPanel'

/** Long enough to read, short enough that nobody is waiting on theatre. */
const STEP_MS = 420

export function App() {
  const [demo, setDemo] = useState<DemoIncident | null>(null)
  const [narrative, setNarrative] = useState('')
  const [decision, setDecision] = useState<Decision | null>(null)
  const [error, setError] = useState<ForkcastError | null>(null)
  const [running, setRunning] = useState(false)
  const [challenging, setChallenging] = useState(false)
  const [activeStep, setActiveStep] = useState(0)

  const resultRef = useRef<HTMLDivElement>(null)

  const steps = useMemo(
    () => [
      'Extracting operational constraints',
      'Generating response plans',
      `Running ${demo?.defaultTrialCount ?? 500} future simulations`,
      'Comparing operational outcomes',
      'Verifying numerical claims',
      'Preparing recommendation',
    ],
    [demo?.defaultTrialCount],
  )

  useEffect(() => {
    let cancelled = false

    api
      .demoIncident()
      .then((loaded) => {
        if (!cancelled) {
          setDemo(loaded)
          setNarrative(loaded.narrative)
        }
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setError(toForkcastError(cause))
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  // The steps advance on their own clock while the request is in flight. The result appears when
  // both have finished, so the animation never holds a finished answer back for long and never
  // pretends work is still happening.
  useEffect(() => {
    if (!running) {
      return
    }

    setActiveStep(0)
    const timer = window.setInterval(() => {
      setActiveStep((step) => Math.min(step + 1, steps.length - 1))
    }, STEP_MS)

    return () => window.clearInterval(timer)
  }, [running, steps.length])

  const simulate = useCallback(async () => {
    setRunning(true)
    setError(null)
    setDecision(null)

    const started = performance.now()

    try {
      const result = await api.run(narrative)
      const remaining = steps.length * STEP_MS - (performance.now() - started)
      if (remaining > 0) {
        await new Promise((resolve) => window.setTimeout(resolve, remaining))
      }
      setDecision(result)
    } catch (cause: unknown) {
      setError(toForkcastError(cause))
    } finally {
      setRunning(false)
    }
  }, [narrative, steps.length])

  const challenge = useCallback(
    async (question: string) => {
      setChallenging(true)
      setError(null)

      try {
        setDecision(await api.challenge(question, narrative))
      } catch (cause: unknown) {
        setError(toForkcastError(cause))
      } finally {
        setChallenging(false)
      }
    },
    [narrative],
  )

  const reset = useCallback(async () => {
    setChallenging(true)
    setError(null)

    try {
      setDecision(await api.run(narrative))
    } catch (cause: unknown) {
      setError(toForkcastError(cause))
    } finally {
      setChallenging(false)
    }
  }, [narrative])

  const settled = decision != null && !running
  useEffect(() => {
    if (settled) {
      resultRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [settled])

  const incident = decision?.incident ?? demo?.incident ?? null
  const plans = decision?.plans ?? demo?.plans ?? []
  const edited = demo != null && narrative !== demo.narrative

  return (
    <div className="shell">
      <header className="hero">
        <div className="hero__inner">
          <div className="hero__brand">
            <span className="hero__mark" aria-hidden="true">
              <svg viewBox="0 0 32 32" fill="none">
                <path
                  d="M6 25V13.5C6 10.46 8.46 8 11.5 8H23"
                  stroke="currentColor"
                  strokeWidth="2.4"
                  strokeLinecap="round"
                />
                <path
                  d="M6 25H11.5C14.54 25 17 22.54 17 19.5V19"
                  stroke="currentColor"
                  strokeWidth="2.4"
                  strokeLinecap="round"
                  opacity="0.4"
                />
                <circle cx="26" cy="8" r="3" fill="currentColor" />
                <circle cx="17" cy="16" r="3" fill="currentColor" opacity="0.4" />
              </svg>
            </span>
            <span className="hero__wordmark">Forkcast</span>
            <span className={`badge${decision?.intelligence.live ? ' badge--live' : ''}`}>
              {decision?.intelligence.badge ?? 'Deterministic demo mode'}
            </span>
          </div>

          <h1 className="hero__title">See both futures before you decide.</h1>
          <p className="hero__lede">An AI decision agent for operational incidents.</p>
          <p className="hero__note">
            Azure OpenAI reads the incident and writes the explanation. A deterministic .NET engine
            calculates every consequence, and no figure reaches this page without a claim behind it.
          </p>
        </div>
      </header>

      <main className="content">
        {error && (
          <div className="alert" role="alert">
            <strong>{error.message}</strong>
            {error.detail && <p>{error.detail}</p>}
          </div>
        )}

        {!incident && !error && <p className="loading">Loading the incident…</p>}

        {incident && (
          <IncidentCard
            incident={incident}
            narrative={narrative}
            pending={running}
            edited={edited}
            onNarrativeChange={setNarrative}
            onSimulate={simulate}
            onRestore={() => demo && setNarrative(demo.narrative)}
          />
        )}

        {running && <AgentProgress steps={steps} activeStep={activeStep} />}

        <div ref={resultRef}>
          {decision && incident && (
            <>
              {decision.notes.length > 0 && (
                <ul className="notes">
                  {decision.notes.map((note) => (
                    <li key={note}>{note}</li>
                  ))}
                </ul>
              )}

              <section className="futures" id="futures">
                <header className="futures__head">
                  <h2>Two futures</h2>
                  <p>
                    {decision.trialCount} simulated nights per plan, seed {decision.seed}. Both plans
                    are scored against the same sampled nights, so the gap between them is the plans
                    and not the sampling.
                  </p>
                </header>

                <div className="futures__grid">
                  {decision.outcomes.map((outcome, index) => {
                    const plan = plans.find((candidate) => candidate.id === outcome.planId)
                    if (!plan) {
                      return null
                    }
                    return (
                      <FuturePanel
                        key={outcome.planId}
                        label={String.fromCharCode(65 + index)}
                        plan={plan}
                        outcome={outcome}
                        incident={incident}
                        recommended={outcome.planId === decision.comparison.recommendedPlanId}
                        peakKw={peakKw(decision)}
                      />
                    )
                  })}
                </div>
              </section>

              <RecommendationPanel decision={decision} />
              <VerificationPanel decision={decision} />

              {demo && (
                <ChallengePanel
                  suggested={demo.suggestedChallenge}
                  examples={demo.exampleChallenges}
                  decision={decision}
                  pending={challenging}
                  onChallenge={challenge}
                  onReset={reset}
                />
              )}
            </>
          )}
        </div>

        <ArchitectureSection />
      </main>

      <footer className="foot">
        <p>
          Forkcast is decision support, not autopilot. The fleet model is simplified and the
          operational data is synthetic. A human operator makes the call.
        </p>
      </footer>
    </div>
  )
}

/**
 * The tallest point either plan reaches. Both timelines are drawn against it, because two charts
 * side by side on different scales invite exactly the wrong comparison.
 */
function peakKw(decision: Decision): number {
  return Math.max(
    1,
    ...decision.outcomes.flatMap((outcome) =>
      outcome.loadCurve.map((sample) => sample.gridPowerKw + sample.bufferPowerKw),
    ),
  )
}

function toForkcastError(cause: unknown): ForkcastError {
  return cause instanceof ForkcastError
    ? cause
    : new ForkcastError('Something went wrong.', cause instanceof Error ? cause.message : undefined)
}
