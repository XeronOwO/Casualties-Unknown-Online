# Global adaptive report-rate / sync-frequency flow control

- Status: Future
- Priority: Medium (enhancement after frame-level/frequent-report features)
- Category: Network / transport / adaptive sync
- Parent: none (global cross-domain; first candidate is remote medical, but the framework is not medical-specific)

## Objective

Frame-level/frequent-report features that tolerate partial loss must share one
global adaptive report-rate mechanism, not a medical-only solution. The goal is
to measure live traffic, bandwidth, latency and jitter, then dynamically adjust
how often each loss-tolerant sync/report stream is sent without changing wire
protocol semantics or losing final authoritative correctness.

A concrete first candidate is the Stage 1 remote medical frame-level injection
stream, but the framework must be domain-agnostic so future streams (player
state, fluid, entity, item movement, other frequent minigame reports) can opt
into it.

## Background / terminology

- **Report rate**（上报率）: how often a non-authoritative side sends incremental
  reports to the host.
- **Sync frequency**（同步频率）: how often the host fans authoritative state out
  to observers.
- **Adaptive sync frequency / dynamic reporting rate**: the ability to change
  those rates based on observed network conditions.

## Scope

- Per-peer effective-bandwidth, RTT, loss/jitter estimation.
- Traffic/byte accounting already exists at the transport observer; extend it to
  per-stream/payload classification so medical, player-state, fluid, entity and
  item streams are distinguishable.
- A generic configurable policy:
  - per-stream minimum/maximum cadence
  - loss-tolerance metadata (overwrite semantics, cumulative semantics, final
    reconciliation)
  - priority/importance weights
  - dynamic degradation order under pressure
- Host-side fan-out cadence may be adjusted independently of the client's report
  cadence.
- Each participating domain must either tolerate skipped intermediate
  frames/updates or provide a terminal/full-state reconciliation (e.g. medical
  `EndCommitted`).
- No wire-protocol version change: only message rate changes.

## Non-goals

- No anti-cheat or malicious-rate policing (separate strict-validation item).
- No prediction/interpolation as part of this ticket; presentation-side
  interpolation can be added per domain later if needed.

## Candidate streams (not exhaustive)

- Remote medical operation session frame reports and host state broadcasts.
- 20 Hz player/entity state stream (overwrite/last-wins semantics).
- Fluid region diff/full-viewport stream.
- Enemy snapshot stream.
- World item movement/periodic snapshot stream.
- Tutorial claw presentation stream.
- Any future minigame shared-session that reports incremental progress.

## Acceptance direction

- Under simulated bandwidth/latency pressure, message volume drops while final
  committed state remains identical.
- No regression in frame-level/optimal mode when no pressure is detected.
- A new stream can opt in with small, declarative configuration rather than a
  domain-specific duplicate implementation.
- Metrics observable in logs/telemetry.
