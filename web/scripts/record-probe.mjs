/**
 * Records an uncut capture of the decision film playing, for splicing into the submission video.
 *
 * Same rules as record-live.mjs: the real UI, the real API, one continuous take, and an injected
 * pointer only because Playwright does not draw the OS cursor. It plays the film, lets the evidence
 * rail advance, then runs the counterfactual so the take also shows the film going stale and coming
 * back with the new evidence.
 *
 *   node scripts/record-film.mjs [outDir] [baseUrl]
 */
import { mkdir, rename } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const outDir = path.resolve(process.argv[2] ?? '../demo/capture')
const baseUrl = process.argv[3] ?? 'http://localhost:5173'
const VIEWPORT = { width: 1920, height: 1080 }

const CURSOR_SCRIPT = `
  (() => {
    const dot = document.createElement('div');
    dot.style.cssText = [
      'position:fixed','z-index:2147483647','width:22px','height:22px',
      'margin:-3px 0 0 -3px','pointer-events:none','left:-100px','top:-100px',
    ].join(';');
    dot.innerHTML =
      '<svg viewBox="0 0 22 22" width="22" height="22">' +
      '<path d="M3 1.5 L3 16.5 L7.2 12.6 L10 19.5 L12.6 18.3 L9.9 11.7 L15.6 11.4 Z"' +
      ' fill="#ffffff" stroke="#04080f" stroke-width="1.3" stroke-linejoin="round"/></svg>';

    const ring = document.createElement('div');
    ring.style.cssText = [
      'position:fixed','z-index:2147483646','width:44px','height:44px',
      'margin:-22px 0 0 -22px','border-radius:50%','pointer-events:none',
      'left:-100px','top:-100px','opacity:0',
      'border:2px solid #4a9eff','background:rgba(74,158,255,.18)',
    ].join(';');

    const badge = document.createElement('div');
    badge.style.cssText = [
      'position:fixed','z-index:2147483645','top:22px','right:26px',
      'display:flex','align-items:center','gap:9px','pointer-events:none',
      'padding:8px 15px 8px 13px','border-radius:999px',
      'background:rgba(10,16,28,.82)','border:1px solid rgba(255,112,137,.5)',
      'font:600 13px/1 "Segoe UI Variable Display","Segoe UI",system-ui,sans-serif',
      'letter-spacing:.07em','color:#ffd9e0','text-transform:uppercase',
    ].join(';');
    badge.innerHTML =
      '<span style="width:9px;height:9px;border-radius:50%;background:#ff7089;' +
      'box-shadow:0 0 0 4px rgba(255,112,137,.22)"></span>' +
      '<span>Live screen capture &middot; real API &middot; uncut</span>';

    const mount = () => (document.body ?? document.documentElement).append(ring, dot, badge);
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', mount, { once: true });
    } else {
      mount();
    }

    window.__cursorTo = (x, y) => { dot.style.left = x + 'px'; dot.style.top = y + 'px'; };
    window.__cursorClick = (x, y) => {
      ring.style.left = x + 'px'; ring.style.top = y + 'px';
      ring.style.transition = 'none'; ring.style.opacity = '1'; ring.style.transform = 'scale(.35)';
      requestAnimationFrame(() => {
        ring.style.transition = 'transform .45s ease-out, opacity .45s ease-out';
        ring.style.transform = 'scale(1)'; ring.style.opacity = '0';
      });
    };
  })();
`

await mkdir(outDir, { recursive: true })

const browser = await chromium.launch({ args: ['--force-device-scale-factor=1'] })
const context = await browser.newContext({
  viewport: VIEWPORT,
  deviceScaleFactor: 1,
  recordVideo: { dir: outDir, size: VIEWPORT },
})
const page = await context.newPage()
await page.addInitScript(CURSOR_SCRIPT)

let at = { x: 960, y: 900 }
async function glideTo(x, y, steps = 22) {
  const from = { ...at }
  for (let i = 1; i <= steps; i++) {
    const t = i / steps
    const ease = 1 - Math.pow(1 - t, 3)
    const nx = from.x + (x - from.x) * ease
    const ny = from.y + (y - from.y) * ease
    await page.mouse.move(nx, ny)
    await page.evaluate(([px, py]) => window.__cursorTo?.(px, py), [nx, ny])
    await page.waitForTimeout(13)
  }
  at = { x, y }
}

async function clickAt(locator) {
  await locator.scrollIntoViewIfNeeded()
  await page.waitForTimeout(280)
  const box = await locator.boundingBox()
  if (!box) throw new Error('target has no box')
  const x = Math.round(box.x + box.width / 2)
  const y = Math.round(box.y + box.height / 2)
  await glideTo(x, y)
  await page.waitForTimeout(180)
  await page.evaluate(([px, py]) => window.__cursorClick?.(px, py), [x, y])
  await page.mouse.click(x, y)
}

const t0 = Date.now()
const mark = (label) => console.log(`  ${((Date.now() - t0) / 1000).toFixed(2)}s  ${label}`)

await page.goto(baseUrl, { waitUntil: 'networkidle' })
await page.waitForSelector('.incident')
await page.evaluate(() => window.__cursorTo?.(960, 900))

await page.getByRole('button', { name: 'Simulate response options' }).click()
await page.waitForSelector('.probe', { timeout: 60_000 })
mark('decision ready')

// Frame the adversarial verifier and submit a paragraph with one invented figure in it.
await page.locator('.probe').scrollIntoViewIfNeeded()
await page.waitForTimeout(1600)
mark('probe framed')

await clickAt(page.getByRole('button', { name: 'Submit to the verifier' }))
await page.waitForSelector('.probe__result', { timeout: 60_000 })
mark('verdict rendered')

// Hold on the annotated paragraph and the ledger so both are readable.
await page.waitForTimeout(9500)

// Then the deterministic summary that replaces it.
await page.locator('.probe__displayed').scrollIntoViewIfNeeded()
await page.waitForTimeout(6500)
mark('replacement shown')

const video = page.video()
await context.close()

if (video) {
  const raw = await video.path()
  const target = path.join(outDir, 'probe-journey.webm')
  await rename(raw, target)
  console.log(`\nRecorded ${target}`)
}

await browser.close()
console.log(`Total take: ${((Date.now() - t0) / 1000).toFixed(2)}s`)
