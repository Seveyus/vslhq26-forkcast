---
format: 1920x1080
duration: 168s
message: "The model can write the explanation. It is never allowed to invent the numbers."
arc: "Bind → Promise → Read it → Simulate it → Two futures → Recommend → Prove it → Challenge it → How → Close"
audience: "Hackathon judges at Microsoft HQ, and enterprise engineering leads"
mode: autonomous
music: none
---

## Frame 1 — The bind

- status: animated
- duration: 15s
- src: compositions/frames/01-bind.html
- transition_in: cut
- scene: A clock at 18:40 and a hard deadline at 06:00, with the gap between them closing.
- voiceover: "At eighteen forty, a fast charger fails at an electric delivery depot. Twenty vehicles still have to leave by six in the morning. Eight charge points remain. Somebody has to decide what to do, now."

Open cold on the operational bind, not on the product. Two clock figures, 18:40 and
06:00, set against each other on a dark field, with the eleven-hour window drawn as a
thinning line between them. Numbers arrive as facts, one at a time: twenty vehicles,
eight charge points, one failure.

## Frame 2 — The promise

- status: animated
- duration: 11s
- src: compositions/frames/02-promise.html
- transition_in: crossfade
- scene: The Forkcast wordmark and the line the whole film is built on.
- voiceover: "Forkcast is an AI decision agent for operational incidents. It lets a team see both futures before making the call."

The brand moment. Wordmark, then the tagline resolving beneath it: *See both futures
before you decide.* Restrained — one accent, no flourish.

## Frame 3 — Read the incident

- status: animated
- duration: 17s
- src: compositions/frames/03-read.html
- transition_in: crossfade
- scene: The real incident card, with the constraints lifting out of the operator's own text.
- voiceover: "A duty manager describes what broke, in their own words. Azure OpenAI reads that text into a structured incident: the fleet, the connectors, the deadline, the battery range. Then the language model steps back."
- asset_candidates: assets/02-incident.png

The product's own incident card. Push in slowly on the textarea while the constraint
chips light in sequence — twenty vehicles, eight charge points, deadline 06:00, one
critical failure. The last beat lifts a caption: *the model reads. It does not
calculate.*

## Frame 4 — The engine runs

- status: animated
- duration: 17s
- src: compositions/frames/04-engine.html
- transition_in: crossfade
- scene: The agent working through the six real steps of the pipeline.
- voiceover: "From here it is arithmetic. A deterministic dot NET engine builds two response plans and simulates five hundred possible nights for each one, against connector queues, re-plug delays and the site's power limit."
- asset_candidates: assets/03-agent.png

The agent progress list, ticking through its six steps in time with the narration.
A counter runs to 500 in the corner. This is the beat that establishes the work is
real work.

## Frame 5 — Two futures

- status: animated
- duration: 27s
- src: compositions/frames/05-futures.html
- transition_in: crossfade
- scene: The two outcomes side by side, sixty point nine against ninety-seven point two.
- voiceover: "Two futures. Continue the current schedule, and sixty point nine percent of vehicles leave on time, with nine of twenty at risk. Reprioritise the queue and bring in a towed battery unit, and it is ninety-seven point two percent, with one at risk, for three hundred and seventy-nine pounds. Both plans are scored against the same simulated nights, so the gap between them is the plans, not luck."
- asset_candidates: assets/04-futures.png

The centrepiece comparison. Both figures count up simultaneously and land together;
the recommended panel takes its green ring last. Hold long enough for a judge to read
all four metrics on each side. The final line earns the common-random-numbers point.

## Frame 6 — The recommendation

- status: animated
- duration: 15s
- src: compositions/frames/06-recommend.html
- transition_in: crossfade
- scene: The recommendation, with the rule that chose it stated in the open.
- voiceover: "Forkcast recommends reprioritising the priority routes and activating the battery buffer. The rule that picked it is written on the screen, next to the evidence it used."
- asset_candidates: assets/05-recommendation.png

The recommendation headline, then the decision rule card sliding in beside it. The
point is that the rule is visible and arguable, not buried.

## Frame 7 — Every number, accounted for

- status: animated
- duration: 22s
- src: compositions/frames/07-verify.html
- transition_in: crossfade
- scene: The claim panel, and an invented figure being caught and thrown out.
- voiceover: "This is the part that matters. Every figure on that screen is a claim, carrying the simulation field it came from, how it was calculated, and the seed to reproduce it. Eight verified claims. Zero unsupported numbers. And if the model ever writes a figure no claim supports, the whole paragraph is discarded, not corrected."
- asset_candidates: assets/06-verification.png

The emotional centre. Open on the four counters, then expand one claim to show its
source field and calculation. In the last third, a line of generated prose appears
with an invented figure in it; the figure is struck through in red and the paragraph
falls away, replaced by the deterministic summary. Give this frame room.

## Frame 8 — Challenge it

- status: animated
- duration: 20s
- src: compositions/frames/08-challenge.html
- transition_in: crossfade
- scene: One assumption changed, the simulation rerun, ninety-seven point two falling to eighty-six point seven.
- voiceover: "And you can argue with it. Ask what happens if the battery unit arrives an hour late, and the simulation genuinely runs again. On-time departures fall from ninety-seven point two to eighty-six point seven percent. Vehicles at risk go from one to eight. Nothing here was written in advance."
- asset_candidates: assets/07-challenge.png

The what-if. Type the question, then the figures move: 97.2 rolls down to 86.7 while
the risk pill goes from Low to High and the at-risk count climbs 1 → 8. The closing
caption: *these numbers came back from the engine.*

## Frame 9 — How it holds

- status: animated
- duration: 16s
- src: compositions/frames/09-boundary.html
- transition_in: crossfade
- scene: The boundary — what the model may do, and what only the engine may do.
- voiceover: "The model may read a report, name a plan, and write the explanation. It may not produce a number, decide which plan wins, or be on the critical path. With no credentials at all, everything you just saw still runs."
- asset_candidates: assets/09-architecture.png

Two columns resolving out of the pipeline diagram: *the model may* in accent blue,
*the model may not* in red. Cleanest possible statement of the architecture.

## Frame 10 — Close

- status: animated
- duration: 8s
- src: compositions/frames/10-close.html
- transition_in: crossfade
- scene: The wordmark, the tagline, and the line to remember.
- voiceover: "Forkcast. See both futures before you decide."

Return to the brand frame from Frame 2, now with the thesis line held beneath it:
*The model can write the explanation. It is never allowed to invent the numbers.*
