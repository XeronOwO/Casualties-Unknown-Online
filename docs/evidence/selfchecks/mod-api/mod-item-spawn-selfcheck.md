# Mod item spawn — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — custom item spawn surface after the
typed item content binding already landed.

Decision: expose a mod-facing world-item spawn surface through the same
permission/session policy family as entity spawn. The Runtime gate lives in
`ModContext`; the Game Adapter creates the local `Item` via `Utils.Create` and
the existing item-domain channel replicates it. No new wire message and no
game/Unity type crosses the mod boundary.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Mod-facing API | `IModItemSpawn` in `CUO.Abstractions` — `CanSpawn` + `TrySpawn(itemId, x, y, rotation)`. |
| 2 | Context surface | `IModContext.ItemSpawn` exposes the per-mod adapter, alongside `EntitySpawn`. |
| 3 | Permission | Reuses `ModPermission.SpawnEntity`; no new permission bit, no handshake/protocol change. |
| 4 | Runtime gate | `ModItemSpawnAdapter` enforces permission, request-shape rails, and active in-world session before the Game Adapter seam. |
| 5 | Runtime → Game Adapter boundary | `IModItemSpawner` with `TrySpawnItem`; default disabled implementation in the Runtime-only composition. |
| 6 | Game Adapter implementation | `GameAdapter` creates the local `Item` through `Utils.Create`, verifies the `Item` component, and returns false + destroys non-item prefabs. |
| 7 | Replication | The normal `Item.Start` report path (`ItemWorldSync.OnItemInstantiated`) sends the existing item-domain `ItemSpawned` channel; no new wire. |
| 8 | Policy rails | Reuses `ModEntitySpawnPolicy` for item id, position, and rotation validation (same malformed-request guard). |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModItemSpawn` | New Abstractions interface. |
| `IModContext` | Added `ItemSpawn` property. |
| `IModItemSpawner` | New Runtime → Game Adapter boundary. |
| `DisabledModItemSpawner` | New Runtime-only disabled implementation. |
| `ModContext` | Added `ModItemSpawnAdapter` with permission/session/policy gating. |
| `ModLifecycle` / `ModService` | Wired the item spawner through the mod-context construction path. |
| `CuoBootstrap` | Registered the disabled item spawner in the Runtime default composition. |
| `PluginDependencyRegistrar` | Replaced the item spawner with the Game Adapter implementation. |
| `GameAdapter` | Implements `IModItemSpawner` and creates/rejects local item copies. |
| Tests | `ModItemSpawnTests` + `FakeModItemSpawner`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Permission gate | Missing `SpawnEntity` refuses item spawn | `ModItemSpawnTests.MissingSpawnEntityPermission_IsRefused` |
| Forwarding | With permission + in-world session, request forwards to the item spawner | `ModItemSpawnTests.WithPermission_ForwardsToGameAdapterItemSpawner` |
| Session gate | Outside an active in-world session refused before adapter | `ModItemSpawnTests.OutsideInWorldSession_IsRefusedBeforeAdapter` |
| Request rails | Empty id / NaN position / infinite rotation refused before adapter | `ModItemSpawnTests.InvalidRequest_IsRefusedBeforeAdapter` |
| Adapter failure | A false return from the adapter is surfaced as false | `ModItemSpawnTests.AdapterFailure_IsReturnedAsFalse` |
| Entity spawn regression | Existing entity spawn surface still passes | `ModEntitySpawnTests` |
| DI integration | Real mod stack exposes `IModContext.ItemSpawn` and routes through the replaced item spawner | build + full suite green |
| No wire/protocol regression | Item spawn rides the existing item-domain channel; no new NetMsg | `docs/api/mod-api.md` §4h, full suite green |

## 4. Verification design

- Pure-managed unit tests for the Runtime gate with a recording fake; no game
  assembly or Unity objects are required for the mod-side contract.
- The Game Adapter implementation stays behind the same GameAdapter compile
  boundary as the other mod surfaces; DI wiring and the full solution build
  verify the seam end to end.
- Static evidence: no new permission bit, no new NetMsg, no Unity/game type in
  Abstractions.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2006 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds x 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
