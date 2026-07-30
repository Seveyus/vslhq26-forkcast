# Operational data contract

Forkcast ships **synthetic** operational data. That is a deliberate choice, not a gap: the demo has
to be reproducible from a published seed, auditable by a stranger, and free of anyone's private
operational detail. A live feed would cost all three and buy nothing a reviewer can check.

This document describes how a production integration would supply the same shape from real systems.
**No connector is implemented.** Nothing in the running application reaches an external service.

## What the engine actually needs

The engine models one shape: a queue of **units**, each needing a quantity delivered before its own
deadline, competing for **resources** whose combined throughput is capped. Everything below maps
onto that.

### Situation

| Field | Type | Required | Notes |
|---|---|---|---|
| `sourceSystem` | string | yes | Which system produced this snapshot, for provenance |
| `snapshotAt` | ISO-8601 with offset | yes | When the state was read, not when it was sent |
| `site` | string | yes | Human-readable site name |
| `detectedAt` | ISO-8601 with offset | yes | When the incident was detected |
| `deadline` | ISO-8601 with offset | yes | The last unit's hard deadline |
| `failures` | string[] | no | Short phrases, shown verbatim on the situation card |
| `dataQuality` | enum | no | `measured`, `estimated`, `stale`, `synthetic` |

### Units

| Field | Type | Required | Notes |
|---|---|---|---|
| `id` | string | yes | Stable identifier |
| `label` | string | yes | Route, workload name, order number |
| `capacity` | number | yes | Total size of the thing being filled |
| `currentLevelPct` | number | yes | 0-100, where it stands now |
| `requiredLevelPct` | number | yes | 0-100, what the deadline demands |
| `deadline` | ISO-8601 with offset | yes | Per-unit, not per-site |
| `isPriority` | boolean | yes | Cannot slip |
| `maxSharedRate` | number | yes | Per-unit cap on the shared resource pool |
| `maxDedicatedRate` | number | yes | Per-unit cap on high-rate or burst resources |
| `assignedResourceId` | string | yes | The pre-incident assignment |

### Resources

| Field | Type | Required | Notes |
|---|---|---|---|
| `id` | string | yes | Stable identifier |
| `kind` | enum | yes | `shared`, `dedicated`, `temporary` |
| `ratedRate` | number | yes | Throughput when healthy |
| `isOperational` | boolean | yes | |
| `faultCode` | string | no | Vendor code, shown in the interface |

### Site constraints

`sharedPoolCapacity`, `readyAheadMinutes`, `handoverBaseMinutes`, `handoverTailMeanMinutes`,
`recoveryProbability` and a recovery window. These are site facts rather than per-incident facts, and
would normally come from configuration rather than telemetry.

### Cost curve

Contiguous windows of `{ label, from, to, pricePerUnit, currency }` covering the whole decision
window. Gaps are rejected; both shipped scenarios assert contiguity in tests.

## Units and timestamps

- **Every timestamp carries an offset.** The engine reasons in site-local time, and a naive
  timestamp is ambiguous by exactly the amount that matters overnight.
- **Rates and levels share a unit per domain**, declared in the vocabulary: kWh at kW for a depot,
  GPU-hours at GPU-hours per hour for a compute hall. The engine never converts between them.
- **Costs are currency-tagged.** Both shipped scenarios are GBP; the currency is not yet part of the
  vocabulary, which is a known limitation.

## Validation on the way in

`IncidentComposer` already does this for model-extracted incidents, and a connector would reuse it:
unit count clamped to a supported range, resource counts clamped, level ranges ordered and bounded,
a deadline forced after the detection time and capped at 36 hours out. Every clamp is reported back
to the caller rather than applied silently.

## Where each domain's fields would come from

| Engine concept | Electric depot | Compute hall |
|---|---|---|
| Units | fleet-management system: vehicle, route, battery state | job scheduler: queued batch jobs, progress, SLA |
| Unit deadline | route departure board | reporting or SLA cut-off |
| Resources | charge-point management system (OCPP) | cluster inventory and node health |
| Resource fault | CPMS fault codes | rack or cooling telemetry |
| Shared pool cap | site metering, grid connection less base load | hall cooling or power envelope |
| Cost curve | supplier tariff or wholesale forecast | spot or reserved capacity pricing |
| Failures | incident-management ticket summary | incident-management ticket summary |

## Privacy

Nothing the engine needs is personal data. It needs asset identifiers, quantities and times. A
production integration should:

- pass **asset** identifiers, never driver or operator identity;
- resolve identifiers to labels inside the caller's own boundary;
- treat the incident narrative as free text that may contain names, and redact before it reaches a
  language model;
- keep the model optional, which it already is — the whole decision runs without it.

## Local examples

[`docs/examples/`](examples/) carries one snapshot per shipped domain in this shape. They are loaded
by nothing; they exist so an integrator can see the target format next to the prose.
