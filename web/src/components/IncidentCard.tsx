import type { Incident } from '../api/schema'
import { clock, energy } from '../lib/format'

interface Props {
  incident: Incident
  narrative: string
  pending: boolean
  onNarrativeChange: (value: string) => void
  onSimulate: () => void
  onRestore: () => void
  edited: boolean
}

export function IncidentCard({
  incident,
  narrative,
  pending,
  onNarrativeChange,
  onSimulate,
  onRestore,
  edited,
}: Props) {
  const chips = [
    { label: `${incident.vehicleCount} vehicles`, tone: 'plain' },
    { label: `${incident.operationalChargePointCount} charge points`, tone: 'plain' },
    { label: `Deadline ${clock(incident.departureDeadline)}`, tone: 'plain' },
    {
      label: `${incident.failedChargePointCount} critical failure${
        incident.failedChargePointCount === 1 ? '' : 's'
      }`,
      tone: 'bad',
    },
    { label: `${incident.priorityVehicleCount} priority routes`, tone: 'plain' },
    { label: `${energy(incident.totalRequiredEnergyKwh)} needed`, tone: 'plain' },
  ] as const

  return (
    <section className="panel incident" id="incident">
      <header className="panel__head">
        <div>
          <p className="eyebrow">{incident.site}</p>
          <h2>{incident.title}</h2>
        </div>
        <div className="incident__window">
          <span>{clock(incident.detectedAt)}</span>
          <em aria-hidden="true" />
          <span>{clock(incident.departureDeadline)}</span>
          <small>{incident.chargingWindowHours.toFixed(1)} h to fix it</small>
        </div>
      </header>

      <label className="incident__label" htmlFor="incident-narrative">
        Incident report
      </label>
      <textarea
        id="incident-narrative"
        className="incident__text"
        value={narrative}
        rows={6}
        maxLength={4000}
        spellCheck={false}
        disabled={pending}
        onChange={(event) => onNarrativeChange(event.target.value)}
      />

      <ul className="chips">
        {chips.map((chip) => (
          <li key={chip.label} className={`chip${chip.tone === 'bad' ? ' chip--bad' : ''}`}>
            {chip.label}
          </li>
        ))}
      </ul>

      <ul className="incident__failures">
        {incident.failures.map((failure) => (
          <li key={failure}>{failure}</li>
        ))}
      </ul>

      <div className="incident__actions">
        <button
          type="button"
          className="button button--primary button--lg"
          onClick={onSimulate}
          disabled={pending || !narrative.trim()}
        >
          {pending ? 'Simulating…' : 'Simulate response options'}
        </button>
        {edited && (
          <button type="button" className="button button--ghost" onClick={onRestore} disabled={pending}>
            Restore the original report
          </button>
        )}
      </div>
    </section>
  )
}
