# Guest frame rate lower than host — baseline/overhead isolation self-check

Closes the performance investigation work in
`docs/backlog/todo/guest-frame-rate-lower-than-host.md` by landing the runtime
baseline instrumentation and removing one avoidable per-frame allocation in the
remote-player rendering hot path. It does not claim a measured FPS number from a
manual dual-client acceptance; the new telemetry is the intended evidence
gathering surface for that final pass.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Whole-frame update-pump timing | `GameAdapter.Update` now runs an optional stopwatch when `LatencyInstrumentation.IsEnabled`; `RecordFrame` records the full frame |
| 2 | Slow-frame/frame-drop signal | `LatencyOptions.SlowFrameThresholdMs` (default 25 ms / 40 FPS) counts frames at or above the threshold; logged as `[Latency] Frame: ... slow=N` |
| 3 | Existing per-domain timing remains | the existing `Measure("Renderer")`, `Measure("ItemPosition")` etc. continue to log the same per-domain summaries |
| 4 | Per-frame remote-player enumerable allocation | `EntitySyncService.RemotePlayers` was a live LINQ `Select` over `_entities.Values`; every per-frame iteration in `RemotePlayerRenderer`, `CrossPlayerDragUse`, `RadiationLineSync`, `EnemyTargetResolver` and the Online UI allocated a new enumerator |
| 5 | Cached remote-player view + indexed hot loops | `IEntitySyncControl.RemotePlayers` now returns a cached `IReadOnlyList<PlayerEntity>` rebuilt only on entity-table mutations (join/leave/session teardown); per-frame consumers use indexer loops over the list instead of `foreach` on the interface, avoiding boxed enumerator allocation |
| 6 | Guest-only item follow iteration | `ItemPositionFollow.Update` no longer calls `_follow.Keys.ToList()` every frame; stale removals are collected into a lazily allocated list and applied after the walk, so the common steady-state frame allocates no per-frame key snapshot |

## 2. Changes

- **Frame telemetry**
  - `LatencyInstrumentation.RecordFrame(double)` + `FrameSample` (calls,
    total/avg/max, slow count).
  - `LatencyInstrumentation.IsEnabled` gives callers a zero-overhead disabled
    path before starting a stopwatch.
  - `LatencyOptions.SlowFrameThresholdMs` is configurable through
    `Diagnostics:SlowFrameThresholdMs`.
  - `GameAdapter.Update` records one whole-frame sample per update and flushes
    it with the existing per-domain summaries.

- **Repeated allocation removal**
  - `IEntitySyncControl.RemotePlayers` is now `IReadOnlyList<PlayerEntity>`.
  - `EntitySyncService` maintains `_remotePlayers` and refreshes it after
    `StartMemberSync`, `UpsertEntity` (new join), `EndMemberSync`,
    `EndEntitySync`, `OnMemberRemoved`, and `OnSessionEnded`.
  - The behavior is covered by the existing PlayerJoin/PlayerLeave test: the
    cached list is populated after join and empty after leave.
  - `RemotePlayerRenderer`, `EnemyTargetResolver`, `RadiationLineSync`,
    `OnlineUiOverlay` (and the non-hot callers updated for consistency) iterate
    the cached list by index, so no per-frame boxed enumerator is created.
  - `ItemPositionFollow.Update` now defers stale-key removal until after the
    walk instead of allocating a `Keys.ToList()` snapshot every frame.

## 3. Verification (development-period, no manual acceptance)

- **Focused tests**: `LatencyInstrumentationTests` covers disabled no-op,
  frame aggregation/slow count, and flush clearing; `StateStreamTests` covers
  the cached `RemotePlayers` join/leave lifecycle.
- **Full suite**: `dotnet test CasualtiesUnknownOnline.slnx` — **2283 passed /
  0 failed**.
- **Gates**: `tools/check-architecture.ps1`,
  `tools/check-event-replay.ps1`, `tools/check-entity-event-dispatch.ps1` pass.
- **Format**: `dotnet format CasualtiesUnknownOnline.slnx` run.

## 4. Structure review

- `LatencyInstrumentation` remains one single-purpose diagnostics type; the
  new frame aggregate is a separate private accumulator.
- `EntitySyncService` remains the owner of the entity table and the cached
  read-only view; no other layer mutates the cache.
- No behavior change to sync semantics, protocol, or item/player authority.

## 5. Remaining acceptance guidance

With `Diagnostics:LatencyInstrumentation=true`, compare a host and a guest log
baseline:

- `[Latency] Frame` calls/avg/max/slow show the guest frame-time distribution.
- The existing per-domain rows (especially `Renderer`, `ItemPosition`,
  `EnemySync`) identify which CUO domain is disproportionate on the guest.
- If the guest is run in a Sandboxie/background window, the host/guest
  environment difference remains a separate variable to eliminate before
  attributing the remainder to CUO.
