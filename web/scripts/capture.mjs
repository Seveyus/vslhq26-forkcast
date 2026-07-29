/**
 * Drives the running application and captures the user journey.
 *
 * Used for README screenshots and as the source footage for the demo video. It clicks the real
 * interface against the real API: nothing here renders a mock-up of the product.
 *
 *   node scripts/capture.mjs [outputDir] [baseUrl]
 */
import { mkdir } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const outputDir = path.resolve(process.argv[2] ?? '../demo/assets')
const baseUrl = process.argv[3] ?? 'http://localhost:5173'

const VIEWPORT = { width: 1600, height: 1000 }

await mkdir(outputDir, { recursive: true })

const browser = await chromium.launch()
const context = await browser.newContext({
  viewport: VIEWPORT,
  deviceScaleFactor: 2,
  reducedMotion: 'no-preference',
})
const page = await context.newPage()

const shot = async (name, options = {}) => {
  await page.screenshot({ path: path.join(outputDir, `${name}.png`), ...options })
  console.log(`captured ${name}`)
}

const region = async (name, selector) => {
  const element = page.locator(selector).first()
  await element.scrollIntoViewIfNeeded()
  await page.waitForTimeout(450)
  await element.screenshot({ path: path.join(outputDir, `${name}.png`) })
  console.log(`captured ${name}`)
}

await page.goto(baseUrl, { waitUntil: 'networkidle' })
await page.waitForSelector('.incident')
await page.waitForTimeout(400)
await shot('01-hero')
await region('02-incident', '.incident')

await page.getByRole('button', { name: 'Simulate response options' }).click()
await page.waitForTimeout(900)
await region('03-agent', '.agent')

await page.waitForSelector('.futures', { timeout: 30_000 })
await page.waitForTimeout(1400)
await region('04-futures', '.futures')
await region('05-recommendation', '.recommend')

// Open a claim so the provenance is visible in the still.
await page.locator('.claim__row').first().click()
await page.waitForTimeout(350)
await region('06-verification', '.verify')

await page.getByRole('button', { name: 'Test assumption' }).click()
await page.waitForSelector('.delta', { timeout: 30_000 })
await page.waitForTimeout(1200)
await region('07-challenge', '.challenge')
await region('08-futures-after-challenge', '.futures')

await region('09-architecture', '.architecture')

// The full-page shot is tall enough that the retina copy would dominate the repository, and it
// is only ever used as a contact sheet.
await page.evaluate(() => window.scrollTo(0, 0))
await page.waitForTimeout(500)
await shot('10-full-page', { fullPage: true, scale: 'css' })

await browser.close()
console.log(`\nScreenshots written to ${outputDir}`)
