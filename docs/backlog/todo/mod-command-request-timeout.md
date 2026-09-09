# Mod command requests dropped by the rate limiter leave pending callbacks unresolved

- Status: Todo
- Priority: Low
- Category: Network / sync coverage / mod API
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row N8)
- Related: `docs/evidence/selfchecks/mod-api/mod-service-split-selfcheck.md`

## Problem (evidence)

The mod API's rate limiter drops incoming frames on purpose
(`src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModRateLimitPolicy.cs:7`,
"dropped WITH a log, never queued"), and `ModLifecycle.cs:283` /
`ModCommandService.cs:246-261` implement it.

For `ModMessage` that is an accepted loss. For `ModCommandRequest` it has a side
effect on the requester:

- The requester registers a pending callback keyed by request id
  (`src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModCommandService.cs:327`,
  `_pending.Add(guestRequestId, new PendingCommand(callback));`).
- Only the result frame (`:215`) or session end (`FailAllPending`, `:226`) resolves
  it. A dropped request therefore leaves the caller's callback unresolved until
  the session ends — there is no timeout and no failure frame.
- `ModStatusTransport` (host → guests per-player status over `ModMessage`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModStatusTransport.cs:47`)
  inherits the same no-fallback semantics; that is acceptable for a status
  projection but should be documented.

## Goal

A dropped or rejected mod command request fails the caller's pending callback
promptly with an observable reason; the pending map cannot grow without bound;
the success path is unchanged. Record the accepted loss semantics for
`ModMessage` / `ModStatusTransport` in the matrix row and the mod API doc.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Host rate-limits a command request | Requester's callback fails with a reason (no silent wait) |
| 2 | Result frame lost | Callback fails on timeout, not at session end |
| 3 | Normal request/result | Unchanged |
| 4 | Session ends with pending requests | Existing fail-all still fires |
| 5 | Burst of dropped requests | Pending map stays bounded |
| 6 | `ModStatusTransport` | Documented as loss-tolerant (no backfill), not silently assumed reliable |

## Non-goals

- Making the mod API reliable/ordered end-to-end.
- Changing the rate-limit policy itself.
