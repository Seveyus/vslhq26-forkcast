/**
 * Records an uncut screen capture of the real application, with a visible cursor.
 *
 * This exists to answer one question a reviewer is entitled to ask about any polished demo
 * video: is that a working application, or a beautifully animated prototype? The answer has to
 * be a continuous take of the actual product responding to actual clicks — so this script drives
 * the real UI against the real API and records the result without cuts.
 *
 * Playwright does not draw the OS cursor into its recording, so a cursor is injected into the
 * page and driven from the same coordinates as the real mouse events. The clicks are real; only
 * the pointer's appearance is synthetic, and it is labelled as such in the output.
 *
 *   node scripts/record-live.mjs [outDir] [baseUrl]
 */
import { mkdir, readdir, rename } from 'node:fs/promises'
import path from 'node:path'
import { chromium } from 'playwright'

const outDir = path.resolve(process.argv[2] ?? '../demo/capture')
const baseUrl = process.argv[3] ?? 'http://localhost:5173'
const VIEWPORT = { width: 1920, height: 1080 }

/** Injected pointer. Follows the same coordinates the real mouse events are dispatched at. */
const CURSOR_SCRIPT = `
  (() => {
    const dot = document.createElement('div');
    dot.id = '__forkcast_cursor';
    dot.style.cssText = [
      'position:fixed', 'z-index:2147483647', 'width:22px', 'height:22px',
      'margin:-3px 0 0 -3px', 'pointer-events:none', 'left:-100px', 'top:-100px',
      'transition:transform .08s ease-out',
    ].join(';');
    dot.innerHTML =
      '<svg viewBox="0 0 22 22" width="22" height="22">' +
      '<path d="M3 1.5 L3 16.5 L7.2 12.6 L10 19.5 L12.6 18.3 L9.9 11.7 L15.6 11.4 Z"' +
      ' fill="#ffffff" stroke="#04080f" stroke-width="1.3" stroke-linejoin="round"/></svg>';

    const ring = document.createElement('div');
    ring.id = '__forkcast_click';
    ring.style.cssText = [
      'position:fixed', 'z-index:2147483646', 'width:44px', 'height:44px',
      'margin:-22px 0 0 -22px', 'border-radius:50%', 'pointer-events:none',
      'left:-100px', 'top:-100px', 'opacity:0',
      'border:2px solid #4a9eff', 'background:rgba(74,158,255,.18)',
    ].join(';');

    // Recording indicator. States plainly what the viewer is looking at, so the segment cannot
    // be mistaken for the animated part of the film it is spliced into.
    const badge = document.createElement('div');
    badge.id = '__forkcast_badge';
    badge.style.cssText = [
      'position:fixed', 'z-index:2147483645', 'top:22px', 'right:26px',
      'display:flex', 'align-items:center', 'gap:9px', 'pointer-events:none',
      'padding:8px 15px 8px 13px', 'border-radius:999px',
      'background:rgba(10,16,28,.82)', 'border:1px solid rgba(255,112,137,.5)',
      'font:600 13px/1 "Segoe UI Variable Display","Segoe UI",system-ui,sans-serif',
      'letter-spacing:.07em', 'color:#ffd9e0', 'text-transform:uppercase',
      '-webkit-font-smoothing:antialiased',
    ].join(';');
    badge.innerHTML =
      '<span style="width:9px;height:9px;border-radius:50%;background:#ff7089;' +
      'box-shadow:0 0 0 4px rgba(255,112,137,.22)"></span>' +
      '<span>Live screen capture &middot; real API &middot; uncut</span>';

    // The init script runs before <body> exists. Appending to <html> at that point leaves the
    // nodes outside the rendered tree, so they never appear — mount once the body is there.
    const mount = () => {
      const host = document.body ?? document.documentElement;
      host.append(ring, dot, badge);
    };
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', mount, { once: true });
    } else {
      mount();
    }

    window.__cursorTo = (x, y) => {
      dot.style.left = x + 'px';
      dot.style.top = y + 'px';
    };
    window.__cursorClick = (x, y) => {
      ring.style.left = x + 'px';
      ring.style.top = y + 'px';
      ring.style.transition = 'none';
      ring.style.opacity = '1';
      ring.style.transform = 'scale(.35)';
      requestAnimationFrame(() => {
        ring.style.transition = 'transform .45s ease-out, opacity .45s ease-out';
        ring.style.transform = 'scale(1)';
        ring.style.opacity = '0';
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

/** Glides the pointer so the take reads as a person using the product. */
let at = { x: 960, y: 980 }
async function glideTo(x, y, steps = 26) {
  const from = { ...at }
  for (let i = 1; i <= steps; i++) {
    const t = i / steps
    const ease = 1 - Math.pow(1 - t, 3)
    const nx = from.x + (x - from.x) * ease
    const ny = from.y + (y - from.y) * ease
    await page.mouse.move(nx, ny)
    await page.evaluate(([px, py]) => window.__cursorTo?.(px, py), [nx, ny])
    await page.waitForTimeout(14)
  }
  at = { x, y }
}

async function clickAt(locator) {
  const box = await locator.boundingBox()
  if (!box) throw new Error('target has no box')
  const x = Math.round(box.x + box.width / 2)
  const y = Math.round(box.y + box.height / 2)
  await glideTo(x, y)
  await page.waitForTimeout(220)
  await page.evaluate(([px, py]) => window.__cursorClick?.(px, py), [x, y])
  await page.mouse.click(x, y)
}

const mark = (label) => console.log(`  ${(Date.now() - t0) / 1000}s  ${label}`)
const t0 = Date.now()

await page.goto(baseUrl, { waitUntil: 'networkidle' })
await page.waitForSelector('.incident')
await page.evaluate(() => window.__cursorTo?.(960, 980))
await page.waitForTimeout(900)
mark('loaded')

// 1. The incident, then the real click on Simulate.
await page.locator('.incident').scrollIntoViewIfNeeded()
await page.waitForTimeout(700)
await clickAt(page.getByRole('button', { name: 'Simulate response options' }))
mark('clicked Simulate')

// 2. The agent working, then the results arriving.
await page.waitForSelector('.futures', { timeout: 60_000 })
mark('futures rendered')
await page.waitForTimeout(1500)

// 3. Scroll the comparison into full view and let it be read.
await page.locator('.futures').scrollIntoViewIfNeeded()
await page.waitForTimeout(2600)

// 4. The what-if, clicked for real.
await page.locator('.challenge').scrollIntoViewIfNeeded()
await page.waitForTimeout(900)
await clickAt(page.getByRole('button', { name: 'Test assumption' }))
mark('clicked Test assumption')

await page.waitForSelector('.delta', { timeout: 60_000 })
mark('delta rendered')
await page.waitForTimeout(2800)

const video = page.video()
await context.close()

if (video) {
  const raw = await video.path()
  const target = path.join(outDir, 'live-journey.webm')
  await rename(raw, target)
  console.log(`\nRecorded ${target}`)
}

await browser.close()
console.log(`Total take: ${(Date.now() - t0) / 1000}s`)
console.log(await readdir(outDir))
