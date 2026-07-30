/**
 * Freezes the live decision brief to a file, for a renderer to consume.
 *
 * This is the seam that makes the claim literal: the browser player and any offline render read the
 * same payload, so they cannot disagree about a figure. The export validates before it writes —
 * every beat must reference only claims the payload carries, and the beats must tile the timeline —
 * because a renderer that is handed a broken brief produces a confident, wrong film.
 *
 *   node scripts/briefing-export.mjs --scenario fleet
 *   node scripts/briefing-export.mjs --scenario fleet --question "what if the battery is an hour late"
 *   node scripts/briefing-export.mjs --scenario compute --out ../demo/generated
 */
import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'

const args = process.argv.slice(2)
const flag = (name, fallback) => {
  const at = args.indexOf(`--${name}`)
  return at >= 0 && args[at + 1] ? args[at + 1] : fallback
}

const scenario = flag('scenario', 'fleet')
const question = flag('question', '')
const outDir = path.resolve(flag('out', '../demo/generated'))
const baseUrl = (flag('api', 'http://localhost:5199')).replace(/\/$/, '')

const query = new URLSearchParams({ scenario })
if (question) {
  query.set('question', question)
}

const url = `${baseUrl}/api/briefing/export?${query}`
console.log(`fetching ${url}`)

let response
try {
  response = await fetch(url)
} catch {
  console.error(
    `\nCannot reach the Forkcast API at ${baseUrl}.\n` +
      'Start it with: dotnet run --project src/Forkcast.Api',
  )
  process.exit(1)
}

if (!response.ok) {
  console.error(`The API returned ${response.status}.`)
  process.exit(1)
}

const briefing = await response.json()

/** Refuse to freeze a brief a renderer would mis-render. */
const problems = []

const known = new Set((briefing.claims ?? []).map((claim) => claim.id))
if (known.size === 0) {
  problems.push('the payload carries no claims')
}

let clock = 0
for (const beat of briefing.beats ?? []) {
  if (Math.abs(beat.startSeconds - clock) > 0.01) {
    problems.push(`beat "${beat.id}" starts at ${beat.startSeconds}s, expected ${clock}s`)
  }
  if (!(beat.durationSeconds > 0)) {
    problems.push(`beat "${beat.id}" has no duration`)
  }
  for (const id of beat.claimIds ?? []) {
    if (!known.has(id)) {
      problems.push(`beat "${beat.id}" references unknown claim "${id}"`)
    }
  }
  if (!beat.heading || !beat.caption) {
    problems.push(`beat "${beat.id}" is missing a heading or caption`)
  }
  clock += beat.durationSeconds
}

if (Math.abs(clock - (briefing.totalSeconds ?? 0)) > 0.01) {
  problems.push(`beats total ${clock}s but totalSeconds is ${briefing.totalSeconds}`)
}

if (problems.length > 0) {
  console.error('\nThe brief did not validate:')
  for (const problem of problems) {
    console.error(`  - ${problem}`)
  }
  process.exit(1)
}

await mkdir(outDir, { recursive: true })
const suffix = question ? 'counterfactual-briefing' : 'briefing'
const target = path.join(outDir, `${briefing.domainKey}-${suffix}.json`)
await writeFile(target, `${JSON.stringify(briefing, null, 2)}\n`, 'utf8')

console.log(`\nvalidated ${briefing.beats.length} beats · ${known.size} claims · ${clock}s`)
console.log(`domain     ${briefing.domainKey} (${briefing.domainLabel})`)
console.log(`seed       ${briefing.seed}, ${briefing.trialCount} trials per plan`)
if (briefing.counterfactualLabel) {
  console.log(`assumption ${briefing.counterfactualLabel}`)
}
console.log(`\nfrozen to ${target}`)
