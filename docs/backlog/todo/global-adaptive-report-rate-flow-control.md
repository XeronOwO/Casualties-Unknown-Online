# Global adaptive report-rate / sync-frequency flow control — roadmap

- Status: Todo
- Priority: Medium (promoted by user from future on 2026-09-06)
- Category: Network / adaptive sync / flow control
- Source: User selection — "做上报率框架吧。如果觉得比较复杂，可以拆分成多阶段分阶段实施，一个会话一个阶段，不要 goal 一次性做完，拆分后不保留原文档。有些数据包是允许不可靠的，有些数据包是要求可靠的，需要做区分"

## Direction

Build one global mechanism for frequent, loss-tolerant report/sync streams, not a
medical-only solution. It must distinguish:

- **Reliable control** messages: never rate-limited/dropped/coalesced.
- **Unreliable overwrite (`LatestWins`)** streams: skipped intermediate frames
  are harmless because the next frame overwrites.
- **Unreliable cumulative** streams: intermediate deltas may be coalesced only
  when the domain has a terminal/full-state reconciliation.

No wire-protocol version change: only message rate changes.

## Stage index

| Stage | Ticket | Scope |
|---|---|---|
| 1 | [Global stream taxonomy + health-driven overwrite governor](review/global-adaptive-report-rate-stage-1-global-governor.md) | `AdaptiveStreamId`/delivery-mode catalog, pure pressure classifier + rate policy, `AdaptiveStreamRateService`, and integration of Player/Enemy/Tutorial overwrite streams |
| 2 | (not yet created) | Per-stream/per-peer traffic/bandwidth estimates, richer pressure inputs and policy tuning |
| 3 | (not yet created) | Cumulative stream support: coalescing/lightening for medical frame-level deltas, shrapnel ordinary positions, etc. |
| 4 | (not yet created) | Remaining frequent domains: item move/snapshot, fluid, trader state, other future streams |

## Candidate streams

Not exhaustive; later streams opt in through the same mechanism:

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

## Non-goals

- No anti-cheat/malicious-rate policing (separate `strict-validation-anti-cheat` future item).
- No prediction/interpolation as part of this ticket.
