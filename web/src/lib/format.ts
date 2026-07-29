import type { RiskLevel } from '../api/schema'

const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
  timeZone: 'UTC',
})

/**
 * Renders a site-local clock time.
 *
 * The API sends offsets that already encode site-local time, so the offset is applied and the
 * result formatted in UTC. Formatting in the viewer's zone would relabel an 18:40 depot fault
 * as something else depending on where the demo is being watched.
 */
export function clock(iso: string): string {
  const parsed = new Date(iso)
  const offsetMinutes = readOffsetMinutes(iso)
  const shifted = new Date(parsed.getTime() + offsetMinutes * 60_000)
  return timeFormatter.format(shifted)
}

function readOffsetMinutes(iso: string): number {
  const match = /([+-])(\d{2}):(\d{2})$/.exec(iso)
  if (!match) {
    return 0
  }
  const sign = match[1] === '-' ? -1 : 1
  return sign * (Number(match[2]) * 60 + Number(match[3]))
}

/** Absolute epoch milliseconds, used for positioning on the timeline. */
export function instant(iso: string): number {
  return new Date(iso).getTime()
}

export function percent(value: number, digits = 1): string {
  return `${value.toFixed(digits)}%`
}

export function money(value: number): string {
  return `£${Math.round(value).toLocaleString('en-GB')}`
}

export function energy(value: number): string {
  return `${value.toFixed(value >= 100 ? 0 : 1)} kWh`
}

export function signed(value: number, digits = 1): string {
  return `${value > 0 ? '+' : ''}${value.toFixed(digits)}`
}

export function riskTone(risk: RiskLevel): 'good' | 'warn' | 'bad' {
  switch (risk) {
    case 'Low':
      return 'good'
    case 'Medium':
      return 'warn'
    default:
      return 'bad'
  }
}
