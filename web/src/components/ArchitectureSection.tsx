const STAGES = [
  {
    title: 'Incident',
    detail: 'A duty manager describes what broke, in their own words.',
    owner: 'Human',
  },
  {
    title: 'Structured extraction',
    detail: 'The text becomes a schema: fleet, connectors, deadline, battery range.',
    owner: 'Azure OpenAI',
  },
  {
    title: 'Decision engine',
    detail: 'Two response plans, scheduled against connectors, power and re-plug delays.',
    owner: '.NET',
  },
  {
    title: '500 simulated nights',
    detail: 'Both plans see the same sampled night, so the gap between them is the plans.',
    owner: '.NET',
  },
  {
    title: 'Claim verifier',
    detail: 'Every figure round-trips to the simulation field it came from.',
    owner: '.NET',
  },
  {
    title: 'Recommendation',
    detail: 'A stated rule picks the plan. The model writes it up, and is checked.',
    owner: 'Both',
  },
] as const

export function ArchitectureSection() {
  return (
    <section className="panel architecture" id="architecture">
      <header className="panel__head">
        <div>
          <h2>How the answer is produced</h2>
          <p className="panel__sub">
            The language model interprets and explains. It never calculates, and nothing it writes
            reaches the screen unchecked.
          </p>
        </div>
      </header>

      <ol className="flow">
        {STAGES.map((stage, index) => (
          <li key={stage.title} className="flow__stage">
            <div className="flow__index">{String(index + 1).padStart(2, '0')}</div>
            <div className="flow__body">
              <h3>{stage.title}</h3>
              <p>{stage.detail}</p>
            </div>
            <span
              className={`flow__owner flow__owner--${stage.owner.toLowerCase().replace(/\s/g, '-')}`}
            >
              {stage.owner}
            </span>
          </li>
        ))}
      </ol>

      <div className="guardrails">
        <h3>The rule the architecture enforces</h3>
        <div className="guardrails__grid">
          <div>
            <span className="guardrails__allow">The model may</span>
            <ul>
              <li>read an incident report into a fixed schema</li>
              <li>name and describe a response plan</li>
              <li>write the executive explanation</li>
              <li>classify which assumption a question is challenging</li>
            </ul>
          </div>
          <div>
            <span className="guardrails__deny">The model may not</span>
            <ul>
              <li>produce a percentage, a cost or a vehicle count</li>
              <li>decide which plan wins</li>
              <li>state a figure that no claim supports</li>
              <li>be on the critical path — with no credentials, all of this still runs</li>
            </ul>
          </div>
        </div>
      </div>
    </section>
  )
}
