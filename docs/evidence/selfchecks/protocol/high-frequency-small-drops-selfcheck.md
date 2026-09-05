# High-frequency small drops — message-volume observation

Owner cycle: backlog item-domain TODO "High-frequency small drops (shell
casings etc.): observe message volume before optimizing — batch/rate-limit
only if it actually hurts." Decision for this cycle: **add the observation
instrumentation only**. No batching/rate-limit is shipped yet; the backlog's
first step is to make the volume measurable, and a batching decision needs
real numbers, not a guess.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Item-domain wire sends span several message families | `ItemSpawnMsg`, `ItemDropMsg`, `ItemMoveMsg`, `ItemDestroyMsg`, `ItemPickupMsg` (Runtime/Protocol/Messages) |
| 2 | Originator sends and host relays are separate send paths | `ItemService.SendItemSpawned/Drop/Destroy/Pickup/Move`; host relays in `ItemService.PendingPickups.cs` (`HandleHostSpawnReport`, `HandleHostDropReport`, `CompleteAcceptedPickup`) and `FireItemDestroyedReceived` |
| 3 | A logical operation is not the same as transport fan-out | One guest report + one host relay to N guests is one logical operation per endpoint; the observer counts logical sends, not per-recipient frames |
| 4 | The observer needs a time edge | The runtime already uses `ITimeSource` + tiny `ICuoService` pumps (`PendingPickupPump`); `ItemTrafficPump` follows the same pattern |
| 5 | The state belongs to the item domain | `ItemService` owns the session-scoped tracker; the pump only drives the time edge |
| 6 | No protocol change | The observer is local-only; no new wire message, no ProtocolVersion bump |
| 7 | No game-mechanic change | The observer never touches item state, arbitration or game objects |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ItemTrafficKind` | New enum: Spawn / Drop / Move / Destroy / Pickup |
| `ItemTrafficBucket` | New immutable count bucket for one item label |
| `ItemTrafficWindow` | New immutable window snapshot (start/end, total, per-kind, top items) |
| `ItemTrafficWindowLog` | New pure formatter for the `[ItemTraffic]` log line |
| `ItemTrafficTracker` | New pure session-scoped counter (record, roll window, reset) |
| `ItemTrafficPump` | New `ICuoService`: each frame rolls/logs an elapsed window |
| `ItemService.Traffic.cs` | New partial: `RecordItemTraffic`, `PumpItemTraffic`, `CurrentItemTraffic`, `ItemTrafficLabel`, `ResetItemTraffic` |
| `ItemService.cs` / `ItemService.PendingPickups.cs` | Call `RecordItemTraffic` at the originator send methods and host relay branches (one logical send each) |
| `CuoBootstrap` | Registers `ItemTrafficPump` as an `ICuoService` after `PendingPickupPump` |
| Protocol / patches | Unchanged |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Spawn sends counted | Originator `SendItemSpawned` + host `HandleHostSpawnReport` relay record `Spawn` | `ItemService.Traffic.cs`; `ItemServiceTrafficTests.HostRelay_RecordsSpawnAndDrop` |
| Drop sends counted | Originator `SendItemDropped` + host `HandleHostDropReport` relay record `Drop` | Same wiring test |
| Move updates counted | `SendItemMove` records one `Move` per entry | `ItemServiceTrafficTests.HostSendMove_RecordsOnePerEntry` |
| Destroy sends counted | `SendItemDestroyed` and host `FireItemDestroyedReceived` record `Destroy` | Code path + full suite |
| Pickup sends counted | `SendItemPickedUp` and host `CompleteAcceptedPickup` record `Pickup` | Code path + full suite |
| Window rolls and resets | `ItemTrafficTracker.TryCollectWindow` rolls at the configured interval and clears | `ItemTrafficTrackerTests.TryCollectWindow_RollsAndResetsWithoutDoubleCounting` |
| No double counting across snapshots | `Snapshot()` does not reset | `ItemTrafficTrackerTests.Snapshot_DoesNotResetTheWindow` |
| Top item labels sorted | `TopItems` order by count then ordinal key | `ItemTrafficTrackerTests.TopItems_AreSortedByCountDescendingThenKey` |
| Invalid window rejected | Constructor rejects non-positive interval | `ItemTrafficTrackerTests.Constructor_RejectsNonPositiveWindow` |
| Session scoped | `ResetSessionState` calls `ResetItemTraffic` | `ItemService.cs` `ResetSessionState` |
| No protocol change | No wire message added | `NetMsg` unchanged; `DirectionTests` green in the full suite |

## 4. Verification design

- **L0 tracker tests:** `ItemTrafficTrackerTests` (5 tests) — accumulation,
  window roll/reset, top-item sorting, snapshot non-reset, invalid interval.
- **L0 wiring tests:** `ItemServiceTrafficTests` (2 tests) — host relays and
  host move sends are counted through the real production ItemService.
- **Full regression:** `dotnet test CasualtiesUnknownOnline.slnx` — 1035
  passed / 0 failed.
- **Static evidence:** the send paths are listed in §1 and the call sites are
  in `ItemService.cs` / `ItemService.PendingPickups.cs` / `ItemService.Traffic.cs`.
- **Runtime evidence:** development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; **no manual acceptance** (user 2026-08-16).

## 5. Verification results (2026-08-21)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1035 passed / 0 failed |
| Item-traffic focused tests | 7 passed / 0 failed |
| `dotnet format` (new files only verify-no-changes) | clean |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (32 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (32 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<game-dir>"` | deployed to the real game dir only |
| Protocol | unchanged (no bump) |

## 6. Structure review

- New files are all far under the 600-line gate: `ItemTrafficTracker` ~92,
  `ItemTrafficWindow` ~41, `ItemTrafficWindowLog` ~17, `ItemTrafficPump` ~34,
  `ItemService.Traffic.cs` ~41.
- One top-level type per file; no new expression-state bools; the counter is
  state owned by `ItemService` with a read-only `CurrentItemTraffic` surface.
- `ItemService.cs` stays at 597 lines, `ItemService.PendingPickups.cs` at
  253 — both under the gate.
- Dead mechanisms: none. The observer is a new consumer of existing send
  paths; it does not duplicate or replace any sync logic.
