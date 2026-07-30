import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import type { BriefingBeat, Claim } from '../src/api/schema.ts'
import {
  TARGET_PLAYBACK_SECONDS,
  activeBeatIndex,
  displayClaimId,
  displaySourceField,
  initialPlaybackState,
  playbackReducer,
  resolveBeatClaims,
} from '../src/components/decisionFilm.ts'

const claim: Claim = {
  id: 'verified-on-time',
  label: 'On-time outcome',
  value: 12.3,
  displayValue: '12.3%',
  unit: '%',
  sourceField: 'alternative.onTimeDeparturePct',
  calculationMethod: 'Deterministic test fixture',
  simulationSeed: 1,
  trialCount: 10,
  verified: true,
}

const beat = (claimIds: string[] = []): BriefingBeat => ({
  id: 'beat',
  kind: 'evidence',
  startSeconds: 0,
  durationSeconds: 10,
  heading: 'Evidence',
  caption: 'Evidence without an invented figure.',
  claimIds,
})

test('browser playback stays inside the requested 20–25 second window', () => {
  assert.ok(TARGET_PLAYBACK_SECONDS >= 20)
  assert.ok(TARGET_PLAYBACK_SECONDS <= 25)
})

test('active beat follows scaled backend timing', () => {
  const beats = [
    beat(),
    { ...beat(), id: 'second', startSeconds: 10, durationSeconds: 10 },
  ]
  assert.equal(activeBeatIndex(beats, 4.9, 0.5), 0)
  assert.equal(activeBeatIndex(beats, 5, 0.5), 1)
})

test('claim resolution uses only known current evidence and reports unknown ids', () => {
  const result = resolveBeatClaims(['verified-on-time', 'missing'], [claim])
  assert.deepEqual(result.claims.map((item) => item.id), ['verified-on-time'])
  assert.deepEqual(result.unknownIds, ['missing'])
})

test('a non-numerical beat resolves no evidence', () => {
  assert.deepEqual(resolveBeatClaims(beat().claimIds, [claim]), {
    claims: [],
    unknownIds: [],
  })
})

test('pause prevents tick progression and restart resets the clock', () => {
  const paused = playbackReducer({ elapsed: 7, playing: true }, { type: 'pause' })
  assert.deepEqual(
    playbackReducer(paused, { type: 'tick', elapsed: 12, runtime: 24 }),
    { elapsed: 7, playing: false },
  )
  assert.deepEqual(playbackReducer(paused, { type: 'restart' }), {
    elapsed: 0,
    playing: true,
  })
  assert.deepEqual(playbackReducer(paused, { type: 'reset' }), initialPlaybackState)
})

test('compute evidence paths do not expose fleet-only internal nouns', () => {
  const displayed = displaySourceField(
    'alternative.onTimeDeparturePct+alternative.vehiclesAtRisk+alternative.expectedUnmetEnergyKwh',
  )
  assert.doesNotMatch(displayed, /vehicle|departure|energy|kwh/i)
  assert.doesNotMatch(displayClaimId('alternative-unmet-energy'), /energy/i)
})

test('the player source contains no pinned published result percentage', async () => {
  const source = await readFile(
    new URL('../src/components/DecisionFilmPlayer.tsx', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(source, /\b(?:97\.2|86\.7|94\.7|57\.8|60\.9|85\.6)%?/)
})
