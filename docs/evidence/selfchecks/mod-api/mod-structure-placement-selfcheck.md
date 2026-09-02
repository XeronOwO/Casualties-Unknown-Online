# Mod structure placement — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — mod-facing multi-block structure
placement after the typed structure content binding already landed.

Decision: expose a mod-facing structure placement surface through the same
`SpawnEntity` permission/session policy family as entity, item, and single-tile
spawning. The Runtime gate lives in `ModStructurePlacementAdapter`; the Game
Adapter resolves the compiled structure cells, prepares any referenced custom
tiles, validates every target cell (in-world + air), and calls the vanilla
`WorldGeneration.SetBlock` path per cell. The existing `BlockPlaced` relay
replicates each write; no new wire message and no game/Unity type crosses the
mod boundary.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Mod-facing API | `IModStructurePlacement` in `CUO.Abstractions` — `CanPlace` + `TryPlaceStructure(structureId, originX, originY)`. |
| 2 | Context surface | `IModContext.StructurePlacement` exposes the per-mod adapter, alongside `EntitySpawn`, `ItemSpawn`, and `TilePlacement`. |
| 3 | Permission | Reuses `ModPermission.SpawnEntity`; no new permission bit, no handshake/protocol change. |
| 4 | Runtime gate | `ModStructurePlacementAdapter` enforces permission, request-shape rails, and active in-world session before the Game Adapter seam. |
| 5 | Runtime → Game Adapter boundary | `IModStructurePlacer` with `TryPlaceStructure`; default disabled implementation in the Runtime-only composition. |
| 6 | Game Adapter implementation | `GameAdapter` compiles structure cells through `GameAdapterStructureContentProvider`, resolves custom tiles through `GameAdapterTileContentProvider.TryPrepareForPlacement`, preflights all cells, then calls `WorldGeneration.SetBlock` per cell. |
| 7 | Replication | Each `SetBlock` postfix already feeds the existing `BlockPlaced` channel; no new wire. |
| 8 | Policy rails | Reuses `ModEntitySpawnPolicy` for structure id validation (same malformed-request guard as spawn). |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModStructurePlacement` | New Abstractions interface. |
| `IModContext` | Added `StructurePlacement` property. |
| `IModStructurePlacer` | New Runtime → Game Adapter boundary. |
| `DisabledModStructurePlacer` | New Runtime-only disabled implementation. |
| `ModStructurePlacementAdapter` | New per-mod adapter with permission/session/policy gating (top-level, not nested). |
| `ModLifecycle` / `ModService` | Wired the structure placer through the mod-context construction path. |
| `CuoBootstrap` | Registered the disabled structure placer in the Runtime default composition. |
| `PluginDependencyRegistrar` | Replaced the structure placer with the Game Adapter implementation. |
| `GameAdapter` | Implements `IModStructurePlacer` and writes the local multi-block structure. |
| `GameAdapterStructureContentProvider` | Stores compiled cells and resolves them for placement. |
| Tests | `ModStructurePlacementTests` + `FakeModStructurePlacer`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Permission gate | Missing `SpawnEntity` refuses structure placement | `ModStructurePlacementTests.MissingSpawnEntityPermission_IsRefused` |
| Forwarding | With permission + in-world session, request forwards to the structure placer | `ModStructurePlacementTests.WithPermission_ForwardsToGameAdapterStructurePlacer` |
| Session gate | Outside an active in-world session refused before adapter | `ModStructurePlacementTests.OutsideInWorldSession_IsRefusedBeforeAdapter` |
| Request rails | Empty / whitespace / over-cap structure id refused before adapter | `ModStructurePlacementTests.InvalidRequest_IsRefusedBeforeAdapter` |
| Adapter failure | A false return from the adapter is surfaced as false | `ModStructurePlacementTests.AdapterFailure_IsReturnedAsFalse` |
| Spawn regression | Existing entity/item/tile placement surfaces still pass | `ModEntitySpawnTests` / `ModItemSpawnTests` / `ModTilePlacementTests` |
| DI integration | Real mod stack exposes `IModContext.StructurePlacement` and routes through the replaced structure placer | build + full suite green |
| No wire/protocol regression | Structure placement rides the existing `BlockPlaced` relay; no new NetMsg | `docs/api/mod-api.md` §4h, full suite green |

## 4. Verification design

- Pure-managed unit tests for the Runtime gate with a recording fake; no game
  assembly or Unity objects are required for the mod-side contract.
- The Game Adapter implementation stays behind the same GameAdapter compile
  boundary as the other mod surfaces; DI wiring and the full solution build
  verify the seam end to end.
- The structure provider's compiled-cell path is exercised through the
  GameAdapter compile boundary rather than a fake because it depends on the
  Unity world palette; the static contract is covered by DTO tests and the
  generic binder.
- Static evidence: no new permission bit, no new NetMsg, no Unity/game type in
  Abstractions.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2019 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
