# Global adaptive report rate Stage 2 — per-peer/per-stream traffic & bandwidth estimates self-check

Owner cycle: backlog `docs/backlog/in-progress/global-adaptive-report-rate-stage-2-traffic-bandwidth.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Per-peer × per-message receive/send aggregates | `NetworkTrafficTracker` accumulates `(peer, NetMsg)` counters on the existing `RecordSend` / `RecordReceive` paths; `NetworkTrafficWindow` exposes them. |
| 2 | Per-peer × per-payload receive/send aggregates | `NetworkTrafficTracker` accumulates `(peer, WirePayloadType)` counters on `RecordSend` with payload type and `RecordReceivePayload`; `NetworkTrafficWindow` exposes them. |
| 3 | Last completed traffic window | `NetworkTrafficMonitor` retains the window produced by a roll and clears it on `Reset()`, so adaptive queries have a stable estimate after a window boundary. |
| 4 | Logical stream → wire mapping | `AdaptiveStreamWireMapper` maps player/enemy kernel streams to `WirePayloadType` and the tutorial-claw stream to `NetMsg.TutorialClawState`. |
| 5 | Per-stream/per-peer estimate | `AdaptiveTrafficEstimator` computes send/receive counts, bytes, bytes-per-second, failed-send percentage and peer total bytes-per-second from one immutable window. |
| 6 | Richer pressure input | `AdaptivePressureClassifier` now accepts `AdaptivePressureInput` (health + traffic) and raises pressure for per-peer total bandwidth, per-stream send bandwidth and failed-send percentage. |
| 7 | Byte-budget policy | `AdaptiveStreamProfile.MaxBytesPerSecond` and `AdaptiveRatePolicy` use measured average frame size to cap the effective Hz. |
| 8 | Hot-path protection | `EntitySyncService` only refreshes the swing hold while a swing is actually held; broadcast rate queries lazily build traffic windows only when peers exist. |

## 2. Whole-family audit

- `NetworkTrafficTracker` / `NetworkTrafficWindow` / `NetworkTrafficMonitor`: crossed per-peer aggregation added without changing existing global per-peer/per-payload logs.
- `AdaptiveSync`: new wire mapper, estimate, input record, classifier extension, profile byte budget, policy cap and rate-service integration.
- `EntitySyncService`: swing-hold adaptive refresh moved out of every idle frame; still refreshed during an active swing to follow role/peer changes.
- No wire/protocol `NetMsg` / `WirePayloadType` / version change.

## 3. Verification

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2451 runtime tests + 16 normative gates passed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| Focused adaptive/traffic tests | 81 tests passed |
| Independent adversarial self-check | First pass found should-fix hot-path, partial-window dilution and edge-test gaps; all addressed. Follow-up found mid-session reset and swing lifecycle issues; both addressed. Final review confirmed fixes. |

## 4. What was NOT changed

- No cumulative-stream coalescing (Stage 3).
- No item/fluid/trader stream integration (Stage 4).
- No anti-cheat/malicious-rate policing.
- No prediction/interpolation.
