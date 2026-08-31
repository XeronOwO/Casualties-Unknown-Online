# Remote world-item spawn same-id dedup self-check

Owner cycle: backlog items "Duplicate unsynced item drops (guest-dug tree and
world-spawned items)" and "Guest-mined item static-physics desync". The two
reports shared one root cause: a guest's own world-item spawn was echoed back
by the host's committed batch, and the materialization path created a second
scene object with the same instance id.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Guest reports a runtime item spawn | `ItemWorldSync.OnItemInstantiated` allocates an instance id and sends `ItemSpawn` (`ItemWorldSync.cs:180-281`) |
| 2 | Host commits the command and broadcasts the batch | `KernelProtocolService.BroadcastCommittedBatch` sends to every handshaken guest, originator included (`KernelProtocolService.cs:66-90,491-497`) |
| 3 | Guest applies the committed batch and raises `ItemSpawned` | `ItemKernelAuthority.Apply` -> `BatchApplied` -> `KernelBatchItemProjection.ApplySpawnedToProjection` (`ItemKernelAuthority.cs:84-101`; `KernelBatchItemProjection.cs:128-144`) |
| 4 | Materialization previously only looked for id-less generation-time objects | `RemoteItemSceneOps.FindExistingAt` skips any object that already carries an `ItemInstanceId` (`RemoteItemSceneOps.cs:132-135`), so the originator's local original was missed |
| 5 | A duplicate object with the same instance id cannot be driven by the position stream | `ItemPositionFollow` / `ItemApplication.FindWorldItem` resolve one object per id (`ItemPositionFollow.cs:65-78`); extras stay kinematic or twitch without a stream target |

## 2. Root cause

`SpawnWorldItem` idempotency covered only the generation-time binding case. For
a guest-origin runtime item, the host's own committed batch arrives on the
reporter's side with the same instance id while the local original already
exists. `FindExistingAt` deliberately ignores id-stamped objects, so
`SpawnWorldItem` instantiated a duplicate. The duplicate is invisible to the
"one item = one id" scene invariants and remains outside the position-follow
stream — the frozen/unsynced copies in both backlog reports.

## 3. Fix

`RemoteItemSceneOps.SpawnWorldItem` now checks `FindWorldItem(w.ItemId)` before
any materialization and returns when a local object already owns that instance
id. This centralizes idempotency in the single scene-materialization primitive,
so every caller (`ItemSpawned`, `ItemDropped`, `ItemCook`, `ItemReconcile`) is
covered.

## 4. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Same-id object already exists | skip materialization | `RemoteItemSceneOps.SpawnWorldItem` first `FindWorldItem` guard |
| Generation-time binding path | unchanged | the existing `FindExistingAt` branch still runs when no id-stamped object exists |
| Local-origin echo | no duplicate scene object | host batch applies `ItemSpawned`; the already-present local original is reused |
| Reliable duplicate delivery | no duplicate scene object | same `FindWorldItem` guard, same result |
| Position stream / static settle | one object per id remains | unchanged `ItemPositionFollow`; the followed object is the only scene copy |
| Originator receives exactly one spawn event | event-level guard | `ItemRaceTests.OwnSpawnEcho_SurfacesExactlyOneSpawnEvent` |

## 5. Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1850 passed.
- `dotnet format`, `check-architecture`, `check-event-replay`,
  `check-entity-event-dispatch`, `check-delivery`: pass.
- Runtime acceptance: not performed (development-period verification is
  simulation/static evidence; user acceptance remains the final step).
