# Mod tile placement — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — mod-facing single-cell tile
placement after the typed tile content binding already landed.

Decision: expose a mod-facing tile/block placement surface through the same
`SpawnEntity` permission/session policy family as entity and item spawning.
The Runtime gate lives in `ModTilePlacementAdapter`; the Game Adapter resolves
the stable custom tile content id to its deterministic block index, prepares
the tile in the current world palette, and calls the vanilla
`WorldGeneration.SetBlock` path. The existing `BlockPlaced` relay replicates
the write; no new wire message and no game/Unity type crosses the mod boundary.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Mod-facing API | `IModTilePlacement` in `CUO.Abstractions` — `CanPlace` + `TryPlaceBlock(tileId, x, y)`. |
| 2 | Context surface | `IModContext.TilePlacement` exposes the per-mod adapter, alongside `EntitySpawn` and `ItemSpawn`. |
| 3 | Permission | Reuses `ModPermission.SpawnEntity`; no new permission bit, no handshake/protocol change. |
| 4 | Runtime gate | `ModTilePlacementAdapter` enforces permission, request-shape rails, and active in-world session before the Game Adapter seam. |
| 5 | Runtime → Game Adapter boundary | `IModTilePlacer` with `TryPlaceBlock`; default disabled implementation in the Runtime-only composition. |
| 6 | Game Adapter implementation | `GameAdapter` resolves the tile id through `GameAdapterTileContentProvider.TryPrepareForPlacement` and calls `WorldGeneration.SetBlock`. |
| 7 | Replication | The normal `SetBlock` postfix already feeds the existing `BlockPlaced` channel; no new wire. |
| 8 | Policy rails | Reuses `ModEntitySpawnPolicy` for tile id validation (same malformed-request guard as spawn). |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModTilePlacement` | New Abstractions interface. |
| `IModContext` | Added `TilePlacement` property. |
| `IModTilePlacer` | New Runtime → Game Adapter boundary. |
| `DisabledModTilePlacer` | New Runtime-only disabled implementation. |
| `ModTilePlacementAdapter` | New per-mod adapter with permission/session/policy gating (top-level, not nested). |
| `ModLifecycle` / `ModService` | Wired the tile placer through the mod-context construction path. |
| `CuoBootstrap` | Registered the disabled tile placer in the Runtime default composition. |
| `PluginDependencyRegistrar` | Replaced the tile placer with the Game Adapter implementation. |
| `GameAdapter` | Implements `IModTilePlacer` and writes the local custom block. |
| `GameAdapterTileContentProvider` | Added `TryPrepareForPlacement` so a placement can force the tile injection before `SetBlock`. |
| Tests | `ModTilePlacementTests` + `FakeModTilePlacer`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Permission gate | Missing `SpawnEntity` refuses tile placement | `ModTilePlacementTests.MissingSpawnEntityPermission_IsRefused` |
| Forwarding | With permission + in-world session, request forwards to the tile placer | `ModTilePlacementTests.WithPermission_ForwardsToGameAdapterTilePlacer` |
| Session gate | Outside an active in-world session refused before adapter | `ModTilePlacementTests.OutsideInWorldSession_IsRefusedBeforeAdapter` |
| Request rails | Empty / whitespace / over-cap tile id refused before adapter | `ModTilePlacementTests.InvalidRequest_IsRefusedBeforeAdapter` |
| Adapter failure | A false return from the adapter is surfaced as false | `ModTilePlacementTests.AdapterFailure_IsReturnedAsFalse` |
| Spawn regression | Existing entity/item spawn surfaces still pass | `ModEntitySpawnTests` / `ModItemSpawnTests` |
| DI integration | Real mod stack exposes `IModContext.TilePlacement` and routes through the replaced tile placer | build + full suite green |
| No wire/protocol regression | Tile placement rides the existing `BlockPlaced` relay; no new NetMsg | `docs/api/mod-api.md` §4h, full suite green |

## 4. Verification design

- Pure-managed unit tests for the Runtime gate with a recording fake; no game
  assembly or Unity objects are required for the mod-side contract.
- The Game Adapter implementation stays behind the same GameAdapter compile
  boundary as the other mod surfaces; DI wiring and the full solution build
  verify the seam end to end.
- The tile provider's `TryPrepareForPlacement` is exercised through the
  GameAdapter path rather than a fake because it depends on the Unity world
  palette; the static contract is covered by the existing tile content tests.
- Static evidence: no new permission bit, no new NetMsg, no Unity/game type in
  Abstractions.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2011 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds x 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
