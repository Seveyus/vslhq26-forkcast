interface Props {
  steps: readonly string[]
  activeStep: number
}

export function AgentProgress({ steps, activeStep }: Props) {
  return (
    <section className="agent" aria-live="polite" aria-busy="true">
      <h2 className="agent__title">Forkcast is working</h2>
      <ol className="agent__steps">
        {steps.map((step, index) => {
          const state = index < activeStep ? 'done' : index === activeStep ? 'active' : 'waiting'
          return (
            <li key={step} className={`agent__step is-${state}`}>
              <span className="agent__marker" aria-hidden="true" />
              <span className="agent__step-label">{step}</span>
            </li>
          )
        })}
      </ol>
    </section>
  )
}
