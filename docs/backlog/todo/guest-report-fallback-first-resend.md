# Guest pending-report fallback: flat 60 s first resend

- Status: Todo
- Priority: Low-Medium
- Category: Network / sync coverage / world blocks
- Source: Sync cadence review 2026-09-19 (`review/sync-cadence-review.md`, finding 4 — the same 60 s first-resend family, opposite direction)
- Related: `review/guest-block-mutation-re-report.md` (W1 — the guest→host report half), `review/guest-command-loss-reconciliation.md` (the item-command family's 5 s × 12 window), `review/session-control-convergence.md` (the guest's own entry window)

## Problem (evidence)

`PendingReportFallback` (`src/CasualtiesUnknownOnline.Runtime/Session/World/PendingReportFallback.cs`)
is the shared cadence for the guest's unacknowledged reports — the world-block report table
(row W1) and the runtime-entity creation table (row E3). Its window arms on the FIRST outstanding
entry and then re-sends once per `IntervalMs = 60_000`, flat: there is no dense phase.

The live report is sent immediately at the trigger, so the fallback only heals a swallowed one —
but the documented lazy-P2P swallow window is up to ~30 s after world entry, and a guest mutation
made inside it (a broken block, its drops, a runtime-created entity) stays invisible on the host
until the 60 s mark. The host→guest direction of the same family was tightened on 2026-09-19
(`review/sync-cadence-review.md`: the entry repair answers a still-open readiness window inside the
guest's own 5 s window); this direction was measured there and left unchanged.

The sibling item-command family already converges this way: `GuestCommandReconciliation` re-sends
every unacknowledged item report in a bounded 5 s × 12 window (`review/guest-command-loss-reconciliation.md`).

## Goal

A guest report swallowed during the entry swallow window converges on the host well inside that
window (the family's order of magnitude: one 5 s step), while the steady cost stays at one
re-report a minute.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest breaks a block inside the swallow window | Host converges within the chosen first-resend latency |
| 2 | Guest creates a runtime entity inside the swallow window | Host converges within the same latency |
| 3 | Report acknowledged before the window | No duplicate re-send |
| 4 | Steady state (long after entry) | Unchanged: one re-report a minute per outstanding set |
| 5 | Clock wrap | Window re-bases instead of stalling (the existing test) |
| 6 | Bandwidth baseline | No regression beyond the recorded baseline |

## Non-goals

- Changing what a report carries, or the host's arbitration of it.
- A per-cell cadence (the window stays per outstanding set).

## Notes for the implementer

- The arming edge should be the guest's own world entry — `ISessionControl.LocalSceneReported`
  already publishes it and `SessionControlConvergence` consumes it — not a new message.
- `PendingReportFallback` is shared by two tables, so the window shape belongs in this class (one
  policy, two owners) rather than in either table.
- The existing window test asserts the flat cadence; it has to be extended with the dense phase
  rather than replaced, and the clock-wrap case must stay green.
