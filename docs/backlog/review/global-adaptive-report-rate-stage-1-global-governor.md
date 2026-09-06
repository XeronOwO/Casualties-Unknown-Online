# Global adaptive report-rate flow control — Stage 1: stream taxonomy + health-driven overwrite governor

- Status: Review
- Priority: Medium
- Category: Network / adaptive sync / flow control
- Parent: `global-adaptive-report-rate-flow-control.md`

## Objective

Lay the global foundation for adaptive report/sync frequency:

1. Make the reliable-vs-unreliable split explicit at the stream level.
2. Define a small declarative stream profile catalog for loss-tolerant streams.
3. Implement a pure health-pressure classifier and rate policy.
4. Add one shared `AdaptiveStreamRateService` that answers "what cadence should
   this stream use right now".
5. Integrate the existing 20 Hz overwrite streams (player broadcast/report,
   enemy broadcast, tutorial claw) so real network health can lower their
   cadence without wire-protocol changes or reliable-control throttling.

## What landed

- `AdaptiveStreamDeliveryMode`: `ReliableControl`, `LatestWins`, `Cumulative`.
- `AdaptiveStreamId` + `AdaptiveStreamProfile` + `AdaptiveStreamCatalog`:
  PlayerStateBroadcast, PlayerStateReport, EnemyStateBroadcast,
  TutorialClawBroadcast are all `LatestWins` in Stage 1.
- `AdaptivePressureClassifier`: maps RTT/jitter/probe-loss thresholds to
  Optimal / Moderate / High / Critical.
- `AdaptiveRatePolicy`: pure mapping from pressure + priority to effective Hz;
  only `LatestWins` is adapted, reliable/cumulative keep the exact configured
  cadence.
- `AdaptiveStreamRateService`: per-peer and worst-peer broadcast rate queries,
  sends intervals, and logs effective cadence changes.
- Integration: `EntitySyncService`, `EnemySyncService`, `TutorialClawService`
  now schedule their next send from the adaptive rate service instead of
  reading `StateStreamOptions` directly.

## Reliability distinction

- Reliable one-shot/control frames never consult the adaptive service.
- `LatestWins` frames may be rate-adapted: old value is overwritten by the next.
- `Cumulative` is defined but not adapted in Stage 1; coalescing is Stage 3.

## Verification

- Full solution build: passed (0 warnings / 0 errors).
- Full test suite: 2417 + 16 normative gates passed.
- New focused tests:
  - rate policy: optimal unchanged, pressure lowering, min clamp,
    reliable/cumulative not adapted, priority ordering, non-LatestWins ignoring
    adaptive bounds;
  - pressure classifier thresholds (RTT/jitter/loss exact boundaries);
  - catalog completeness and valid ranges;
  - rate service: empty broadcast, broadcast worst-peer, unicast per-peer,
    unknown stream fallback;
  - integration over fake network: high-RTT peer reduces player/enemy/tutorial
    cadence while the stream still flows.
- Independent adversarial self-check: completed by a fresh subagent; its
  findings (non-LatestWins clamp, observability, service-level tests, stale
  comments, session cache reset) were addressed in the final code.
- Deployment: `tools/deploy.ps1` deployed to the real game directory; SHA-256
  hashes of all six CUO DLLs match between the build output and the deployed
  `BepInEx\plugins\CasualtiesUnknownOnline` directory.

## Remaining before final unified acceptance

- Real dual-client acceptance remains the user's final unified acceptance pass.
