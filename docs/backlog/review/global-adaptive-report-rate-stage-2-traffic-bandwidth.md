# Global adaptive report-rate flow control — Stage 2: per-peer/per-stream traffic & bandwidth estimates

- Status: Review
- Priority: Medium
- Category: Network / adaptive sync / flow control
- Parent: `../todo/global-adaptive-report-rate-flow-control.md`

## Objective

Extend the Stage 1 health-driven governor from ping/loss evidence only to a
measurement-driven governor that also knows how much bandwidth is actually being
sent per stream and per peer. Add richer pressure inputs (per-peer/per-stream
bandwidth, failed-send evidence) and per-stream byte-budget policy tuning so a
loss-tolerant stream can be reduced not only by latency/loss but also by
measured bandwidth pressure and by the real size of its frames.

No wire-protocol version change: only local measurement and rate decisions.

## What landed

- Per-peer × per-message and per-peer × per-payload send/receive traffic
  aggregates in `NetworkTrafficTracker` / `NetworkTrafficWindow`, plus a
  retained last-completed window on `NetworkTrafficMonitor` and mid-session
  `Reset()` re-anchoring to the current time.
- `AdaptiveStreamWireMapper`: maps the Stage 1 adaptive stream ids to the wire
  observation key (`WirePayloadType` for player/enemy streams, `NetMsg` for the
  tutorial-claw stream).
- `AdaptiveTrafficEstimate` + `AdaptiveTrafficEstimator`: pure per-peer/per-stream
  bandwidth calculation (frames, bytes, bytes/sec, failed-send ratio, peer total
  send/receive bytes/sec) from a traffic window.
- `AdaptivePressureClassifier` accepts an `AdaptivePressureInput` that combines
  the existing peer-health snapshot with the traffic estimate; bandwidth and
  failed-send thresholds join RTT/jitter/loss as pressure signals.
- `AdaptiveStreamProfile` gains an optional per-stream `MaxBytesPerSecond`
  byte budget; `AdaptiveRatePolicy` clamps the effective cadence by the measured
  average frame size and that budget, in addition to the pressure/priority
  factor.
- `AdaptiveStreamRateService` queries the traffic estimator for the peer/stream
  being adapted and passes the estimate into the classifier and policy. Immature
  (<1 s) windows are ignored to keep the initial join burst from creating false
  bandwidth pressure; broadcast queries build traffic windows lazily only when
  peers exist.
- `EntitySyncService` only refreshes the swing hold while a swing is actually
  held, so idle frames do not build full traffic snapshots.

## Non-goals

- No anti-cheat/malicious-rate policing.
- No prediction/interpolation.
- No wire/protocol change.
- No cumulative-stream coalescing (Stage 3).
- No item/fluid/trader stream integration (Stage 4).

## Verification

- Full solution build: 0 warnings / 0 errors.
- Full test suite: 2451 runtime tests + 16 normative gates passed.
- `dotnet format`: clean.
- New focused coverage: per-peer cross aggregates and reset; last-window
  retention and reset; stream wire mapping plus catalog consistency;
  per-stream/per-peer estimator rates and peer-total fallback; bandwidth and
  failed-send classifier thresholds; byte-budget cap and below-min clamp;
  rate-service high-bandwidth/failed-send/broadcast-min/immature-window/
  post-roll/reset fallback tests.
- Independent adversarial self-check: three fresh-subagent passes; findings
  (hot-path window snapshots, partial-window dilution, missing edge tests,
  mid-session reset re-anchor, swing-hold lifecycle) were all addressed.
- Deployment: `tools/deploy.ps1` deployed to the real game directory; SHA-256
  hashes of all six CUO DLLs match between build output and deployed
  `BepInEx\plugins\CasualtiesUnknownOnline`.

## Remaining before final unified acceptance

- Real dual-client acceptance remains the user's final unified acceptance pass.
