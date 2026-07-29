import { useMemo, useState } from 'react'
import { api, ForkcastError } from '../api/client'
import type { NumberFinding, ProbeExample, VerificationProbe } from '../api/schema'

interface Props {
  examples: readonly ProbeExample[]
  narrative: string
}

/**
 * The claim verifier, offered adversarially.
 *
 * Every product in this space can show a confident answer. The distinguishing question is what
 * happens when the wording is wrong — so rather than assert that invented figures are caught,
 * this panel lets anyone write one and watch it get caught. It calls the same verifier, against
 * the same claim set, that the product applies to its own generated prose.
 */
export function VerifierProbePanel({ examples, narrative }: Props) {
  const [submitted, setSubmitted] = useState(examples[0]?.narrative ?? '')
  const [result, setResult] = useState<VerificationProbe | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = async (text: string) => {
    setPending(true)
    setError(null)
    try {
      setResult(await api.probe(text, narrative))
    } catch (cause: unknown) {
      setError(cause instanceof ForkcastError ? cause.message : 'The verifier could not be reached.')
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="panel probe" id="probe">
      <header className="panel__head">
        <div>
          <p className="eyebrow">Try to fool it</p>
          <h2>Write a number it cannot support</h2>
          <p className="panel__sub">
            Anything you write here goes through the same verifier, against the same claim set,
            that checks the model's own explanation. Invent a figure and watch what happens to the
            paragraph around it.
          </p>
        </div>
      </header>

      <ul className="probe__examples">
        {examples.map((example) => (
          <li key={example.label}>
            <button
              type="button"
              className="chip chip--action"
              disabled={pending}
              onClick={() => {
                setSubmitted(example.narrative)
                void run(example.narrative)
              }}
              title={example.expectation}
            >
              {example.label}
            </button>
          </li>
        ))}
      </ul>

      <form
        className="probe__form"
        onSubmit={(event) => {
          event.preventDefault()
          if (!pending && submitted.trim()) {
            void run(submitted.trim())
          }
        }}
      >
        <textarea
          className="incident__text probe__text"
          rows={4}
          value={submitted}
          maxLength={4000}
          aria-label="Paragraph to submit to the verifier"
          onChange={(event) => setSubmitted(event.target.value)}
        />
        <div className="probe__actions">
          <button
            type="submit"
            className="button button--primary"
            disabled={pending || !submitted.trim()}
          >
            {pending ? 'Checking…' : 'Submit to the verifier'}
          </button>
          {result && (
            <span className={`stamp ${result.accepted ? 'stamp--clean' : 'stamp--dirty'}`}>
              {result.accepted ? 'Accepted' : 'Rejected'}
            </span>
          )}
        </div>
      </form>

      {error && (
        <p className="challenge__miss" role="alert">
          {error}
        </p>
      )}

      {result && <ProbeResult result={result} />}
    </section>
  )
}

function ProbeResult({ result }: { result: VerificationProbe }) {
  const marked = useMemo(() => markUp(result.submitted, result.findings), [result])

  return (
    <div className={`probe__result${result.accepted ? ' is-accepted' : ' is-rejected'}`}>
      <div className="probe__counts">
        <span>
          <strong>{result.numbersFound}</strong> numbers found
        </span>
        <span className="is-good">
          <strong>{result.numbersSupported}</strong> supported
        </span>
        <span className={result.numbersUnsupported > 0 ? 'is-bad' : ''}>
          <strong>{result.numbersUnsupported}</strong> unsupported
        </span>
      </div>

      <p className="probe__marked">{marked}</p>

      {result.findings.length > 0 && (
        <ul className="probe__ledger">
          {result.findings.map((finding, index) => (
            <li key={`${finding.token}-${index}`} className={finding.supported ? 'is-ok' : 'is-bad'}>
              <code>{finding.token}</code>
              {finding.supported ? (
                <span>
                  {finding.claimId ? (
                    <>
                      backed by <code>{finding.claimId}</code> — {finding.reason}
                    </>
                  ) : (
                    <>incident fact — {finding.reason}</>
                  )}
                </span>
              ) : (
                <span>no claim and no incident fact produces this figure</span>
              )}
            </li>
          ))}
        </ul>
      )}

      <p className="probe__verdict">{result.verdict}</p>

      <div className="probe__displayed">
        <span className="future__constraint-label">
          {result.accepted
            ? 'What Forkcast shows'
            : 'What Forkcast shows instead — the deterministic summary'}
        </span>
        <p>{result.displayed}</p>
      </div>

      <p className="probe__foot">
        Checked against {result.claims.length} claims from the run at seed {result.simulationSeed},{' '}
        {result.trialCount} trials per plan.
      </p>
    </div>
  )
}

/**
 * Rebuilds the submitted paragraph with each number wrapped according to its verdict.
 *
 * Walks the findings in order and consumes the text between them, so the original wording and
 * spacing survive untouched — the point is to show the paragraph the user wrote, annotated, not a
 * reconstruction of it.
 */
function markUp(text: string, findings: readonly NumberFinding[]) {
  const nodes: React.ReactNode[] = []
  let cursor = 0

  findings.forEach((finding, index) => {
    const found = text.indexOf(finding.token, cursor)
    if (found < 0) {
      return
    }

    if (found > cursor) {
      nodes.push(text.slice(cursor, found))
    }

    nodes.push(
      <mark
        key={`${finding.token}-${index}`}
        className={finding.supported ? 'tok tok--ok' : 'tok tok--bad'}
        title={finding.supported ? (finding.reason ?? 'supported') : 'no claim supports this'}
      >
        {finding.token}
      </mark>,
    )

    cursor = found + finding.token.length
  })

  if (cursor < text.length) {
    nodes.push(text.slice(cursor))
  }

  return nodes
}
