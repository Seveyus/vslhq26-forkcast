import { useEffect, useRef, useState } from 'react'

const easeOutCubic = (t: number) => 1 - Math.pow(1 - t, 3)

/**
 * Eases a displayed number towards a new value.
 *
 * Used when a challenged assumption moves a metric, so the change is something you watch happen
 * rather than something you have to notice. Respects prefers-reduced-motion by snapping.
 */
export function useAnimatedNumber(target: number, durationMs = 900): number {
  const [value, setValue] = useState(target)
  const fromRef = useRef(target)
  const frameRef = useRef(0)

  useEffect(() => {
    const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
    const from = fromRef.current

    if (reduced || from === target) {
      fromRef.current = target
      setValue(target)
      return
    }

    const start = performance.now()

    const step = (now: number) => {
      const progress = Math.min(1, (now - start) / durationMs)
      setValue(from + (target - from) * easeOutCubic(progress))

      if (progress < 1) {
        frameRef.current = requestAnimationFrame(step)
      } else {
        fromRef.current = target
      }
    }

    frameRef.current = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frameRef.current)
  }, [target, durationMs])

  return value
}
