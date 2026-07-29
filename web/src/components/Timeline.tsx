import { useId } from 'react'
import type { Incident, Outcome } from '../api/schema'
import { clock, instant } from '../lib/format'

interface Props {
  incident: Incident
  outcome: Outcome
  recommended: boolean
  /** Shared across both panels, so the two charts can be read against each other. */
  peakKw: number
}

const WIDTH = 560
const HEIGHT = 118
const PAD_TOP = 12
const PAD_BOTTOM = 22

/**
 * The night, drawn: how much power is flowing into the yard between the fault and the deadline,
 * and when each vehicle actually leaves.
 *
 * Grid supply and towed-battery supply are stacked separately, because the difference between
 * the two plans is precisely that one of them has a second source.
 */
export function Timeline({ incident, outcome, recommended, peakKw }: Props) {
  const gradientId = useId()
  const bufferGradientId = useId()

  const start = instant(incident.detectedAt)
  const end = instant(incident.departureDeadline)
  const span = Math.max(1, end - start)

  const samples = outcome.loadCurve
  const peak = Math.max(1, peakKw)

  const x = (iso: string) => ((instant(iso) - start) / span) * WIDTH
  const y = (kw: number) => HEIGHT - PAD_BOTTOM - (kw / peak) * (HEIGHT - PAD_TOP - PAD_BOTTOM)

  const area = (pick: (index: number) => number) => {
    if (samples.length === 0) {
      return ''
    }
    const top = samples
      .map((sample, index) => `${index === 0 ? 'M' : 'L'}${x(sample.at).toFixed(1)},${y(pick(index)).toFixed(1)}`)
      .join(' ')
    const baseline = HEIGHT - PAD_BOTTOM
    return `${top} L${x(samples[samples.length - 1].at).toFixed(1)},${baseline} L${x(samples[0].at).toFixed(1)},${baseline} Z`
  }

  const totalArea = area((index) => samples[index].gridPowerKw + samples[index].bufferPowerKw)
  const gridArea = area((index) => samples[index].gridPowerKw)
  const hasBuffer = samples.some((sample) => sample.bufferPowerKw > 0)

  return (
    <figure className="timeline">
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        preserveAspectRatio="none"
        role="img"
        aria-label={`Depot power from ${clock(incident.detectedAt)} to ${clock(
          incident.departureDeadline,
        )}, on a shared scale peaking at ${Math.round(peak)} kilowatts`}
      >
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.55" />
            <stop offset="100%" stopColor="var(--accent)" stopOpacity="0.02" />
          </linearGradient>
          <linearGradient id={bufferGradientId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--good)" stopOpacity="0.5" />
            <stop offset="100%" stopColor="var(--good)" stopOpacity="0.02" />
          </linearGradient>
        </defs>

        {hasBuffer && <path d={totalArea} fill={`url(#${bufferGradientId})`} />}
        <path d={gridArea} fill={`url(#${gradientId})`} />
        {hasBuffer && (
          <path
            d={totalArea}
            fill="none"
            stroke="var(--good)"
            strokeWidth="1.4"
            strokeLinejoin="round"
          />
        )}
        <path
          d={gridArea}
          fill="none"
          stroke="var(--accent)"
          strokeWidth="1.4"
          strokeLinejoin="round"
        />

        <line
          x1="0"
          y1={HEIGHT - PAD_BOTTOM}
          x2={WIDTH}
          y2={HEIGHT - PAD_BOTTOM}
          stroke="var(--line-strong)"
          strokeWidth="1"
        />

        {incident.fleet.map((vehicle) => {
          const position = x(vehicle.scheduledDeparture)
          const late = outcome.vehicles.find((v) => v.vehicleId === vehicle.id)?.isAtRisk ?? false
          return (
            <line
              key={vehicle.id}
              x1={position}
              y1={HEIGHT - PAD_BOTTOM - 5}
              x2={position}
              y2={HEIGHT - PAD_BOTTOM + 5}
              stroke={late ? 'var(--bad)' : recommended ? 'var(--good)' : 'var(--text-faint)'}
              strokeWidth="2"
              strokeLinecap="round"
            />
          )
        })}
      </svg>

      <figcaption className="timeline__axis">
        <span>{clock(incident.detectedAt)}</span>
        <span className="timeline__legend">
          <em className="timeline__key timeline__key--grid" /> site supply
          {hasBuffer && (
            <>
              <em className="timeline__key timeline__key--buffer" />{' '}
              {incident.vocabulary.bufferLabel}
            </>
          )}
        </span>
        <span>{clock(incident.departureDeadline)}</span>
      </figcaption>
    </figure>
  )
}
