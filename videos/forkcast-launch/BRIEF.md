---
workflow: product-launch-video
flow: automation
storyboard: no
message: "The model can write the explanation. It is never allowed to invent the numbers."
destination: youtube
aspect: 1920x1080
language: en
audience: "Hackathon judges at Microsoft HQ, and enterprise engineering leads"
length: 165s
angle: trust
style_preset: TBD
---

## Intent

Forkcast is an AI decision agent for operational incidents. This is the submission
video for the VSLive! Microsoft AI Hackathon 2026, it has to survive a room of
engineers who have already seen a dozen LLM wrappers today.

The angle is trust, not features. Every product like this can show a confident
answer; almost none can show why you should believe the number in it. So the film
opens on a real operational bind, shows both futures, and then spends its middle
on the thing that actually differentiates the product: the claim verifier. The
line the room should leave with is the message field above.

Tone: a premium Microsoft product launch. Calm, precise, confident. Not a student
project, not a startup sizzle reel. No hype adjectives: the numbers do the work.

## Assets

Real screens captured from the running application by `web/scripts/capture.mjs`.
Nothing in this video is a mock-up; every figure on screen came back from the
.NET engine at seed 20260728.

- `../../demo/assets/02-incident.png`, the incident card, editable, constraints read out
- `../../demo/assets/03-agent.png`, the agent working through the real request
- `../../demo/assets/04-futures.png`, the two futures side by side, shared scale
- `../../demo/assets/05-recommendation.png`, the recommendation, its rule and its evidence
- `../../demo/assets/06-verification.png`, the verification panel, one claim expanded
- `../../demo/assets/07-challenge.png`, the what-if, 97.2% to 86.7%
- `../../demo/assets/09-architecture.png`, how the answer is produced

## Customizations

- Follow the submission's shot list and timings closely; it is the approved plan.
- Hard requirement: 1920x1080, H.264, between 2:35 and 2:50, under 100 MB, at
  `demo/demo.mp4`.
- Captions on every narrated beat.
- Real product screens are the centre of the film. Do not build a parallel
  interface that differs from the submitted application.
- The verified-claims beat is the emotional centre. Give it room.

## Notes

Figures that must appear exactly as the engine returns them, because tests pin
them and the README repeats them:

- Continue current schedule: 60.9% on-time, 9 of 20 vehicles at risk, High risk
- Reprioritise + battery buffer: 97.2% on-time, 1 of 20 at risk, Low risk, £379
- The what-if: 97.2% falls to 86.7%, vehicles at risk go 1 to 8
- 8 verified claims, 0 unsupported numbers, seed 20260728, 500 trials per plan

Avoid: generic AI robot imagery, stock photography, glassmorphism, hype
adjectives, any copyrighted music, and any number that is not one of the above.
