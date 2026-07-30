/**
 * Playwright smoke for the live evidence-synchronised player.
 *
 * Assumes the API is on :5199 and Vite is on :5173. Screenshots are written only on failure and
 * only under the operating system's temporary directory.
 */
import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { chromium } from 'playwright'

const baseUrl = process.argv[2] ?? 'http://127.0.0.1:5173'
const faults = []
const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
page.setDefaultTimeout(60_000)

page.on('console', (message) => {
  if (message.type() === 'error') {
    faults.push(`console: ${message.text()}`)
  }
})
page.on('pageerror', (error) => faults.push(`pageerror: ${error.stack ?? error.message}`))
page.on('requestfailed', (request) => {
  if (request.url().includes('/api/')) {
    faults.push(`requestfailed: ${request.method()} ${request.url()}`)
  }
})
page.on('response', (response) => {
  if (response.url().includes('/api/') && response.status() >= 400) {
    faults.push(`${response.status()} ${response.url()}`)
  }
})

const progress = async () =>
  Number(await page.getByRole('progressbar', { name: 'Decision film progress' }).getAttribute('aria-valuenow'))

async function runSimulation() {
  await page.getByRole('button', { name: 'Simulate response options' }).click()
  await page.locator('#film').waitFor()
}

async function playAndSeeBeatAdvance() {
  await page.getByRole('button', { name: 'Play verified future' }).click()
  await page.locator('.film__stage').waitFor()
  const firstScene = await page.locator('.rail__scene').textContent()
  await page.waitForFunction(
    ({ first }) => {
      const bar = document.querySelector('[role="progressbar"]')
      const scene = document.querySelector('.rail__scene')
      return (
        Number(bar?.getAttribute('aria-valuenow') ?? 0) > 2 &&
        scene?.textContent?.trim() !== first?.trim()
      )
    },
    { first: firstScene },
    { timeout: 10_000 },
  )
}

try {
  await page.goto(baseUrl, { waitUntil: 'networkidle' })
  await page.locator('#incident').waitFor()

  // Scenario A — fleet without a counterfactual.
  assert.equal(
    await page
      .getByRole('button', { name: /Electric delivery depot/ })
      .getAttribute('aria-pressed'),
    'true',
  )
  await runSimulation()
  await playAndSeeBeatAdvance()
  assert.equal((await page.locator('.film__domain').textContent())?.trim(), 'Electric delivery depot')

  // Restart is tested while playing—the inherited implementation failed specifically here.
  await page.getByRole('button', { name: 'Restart' }).click()
  await page.waitForTimeout(350)
  assert.ok((await progress()) < 8, 'Restart did not return playback to the beginning')

  await page.getByRole('button', { name: 'Pause' }).click()
  const pausedAt = await progress()
  await page.waitForTimeout(800)
  assert.ok(Math.abs((await progress()) - pausedAt) <= 1, 'Pause allowed playback to advance')

  // Scenario B — fleet after the one-hour-delay counterfactual.
  const question = 'What happens if the temporary battery arrives one hour late?'
  await page.getByRole('textbox', { name: 'Assumption to challenge' }).fill(question)
  await page.getByRole('button', { name: 'Test assumption' }).click()
  await page.locator('.delta').waitFor()
  await page.getByText(
    'Decision state changed — playback regenerated from updated evidence.',
    { exact: true },
  ).waitFor()
  await page.locator('.film__beats li[title="Counterfactual test"]').waitFor()

  const deltaValues = await page.locator('.delta__figure strong').allTextContents()
  assert.equal(deltaValues.length, 2)
  await page.getByRole('button', { name: 'Play verified future' }).click()
  await page.waitForFunction(
    () => document.querySelector('.rail__scene')?.textContent?.trim() === 'Counterfactual test',
    undefined,
    { timeout: 22_000 },
  )
  const counterfactualCaption = (await page.locator('.scene__caption').textContent()) ?? ''
  assert.ok(deltaValues.every((value) => counterfactualCaption.includes(value.trim())))
  assert.equal(await page.locator('.rail__claims > li').count(), 4)
  assert.ok((await page.locator('.rail__claims').textContent())?.includes('previous-'))

  // Scenario C — compute. Switching must synchronously remove the old fleet decision.
  await page.getByRole('button', { name: /GPU compute cluster/ }).click()
  await page.getByRole('heading', { name: 'Cooling fault in Slough compute hall' }).waitFor()
  assert.equal(await page.locator('#film').count(), 0, 'Fleet film survived a domain switch')
  await runSimulation()
  await playAndSeeBeatAdvance()
  assert.equal((await page.locator('.film__domain').textContent())?.trim(), 'GPU compute cluster')

  // Reach the evidence beat so every claim label and visible source path is included in the check.
  await page.waitForFunction(
    () => document.querySelector('.rail__scene')?.textContent?.trim() === 'Evidence ledger',
    undefined,
    { timeout: 20_000 },
  )
  const computeFilm = (await page.locator('#film').textContent()) ?? ''
  assert.doesNotMatch(
    computeFilm,
    /\b(?:vehicle|vehicles|charger|chargers|battery|batteries|departure|departures|kwh)\b/i,
  )

  assert.deepEqual(faults, [], `Browser faults:\n${faults.join('\n')}`)
  console.log('Decision Film smoke passed: fleet, counterfactual, compute, pause, restart, 0 browser errors.')
} catch (error) {
  const failureDir = path.join(os.tmpdir(), 'forkcast-decision-film-smoke')
  await mkdir(failureDir, { recursive: true })
  const screenshot = path.join(failureDir, 'failure.png')
  await page.screenshot({ path: screenshot, fullPage: true })
  console.error(`Decision Film smoke failed. Screenshot: ${screenshot}`)
  throw error
} finally {
  await browser.close()
}
