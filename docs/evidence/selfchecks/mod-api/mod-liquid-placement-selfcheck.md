# Mod liquid placement — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — mod-facing liquid-tile placement and
flood fill after the typed liquid-tile content binding and local projection
already landed.

Decision: expose a mod-facing liquid-tile placement/flood-fill surface through
the same `SpawnEntity` permission/session policy family as entity, item, tile
and structure placement. The Runtime gate lives in `ModLiquidPlacementAdapter`;
the Game Adapter resolves the stable custom liquid-tile content id to its
deterministic custom world-fluid byte and calls the vanilla
`FluidManager.SetLiquid` / `StartFill` path. The host fluid stream replicates
the grid write; no new wire message and no game/Unity type crosses the mod
boundary.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Mod-facing API | `IModLiquidPlacement` in `CUO.Abstractions` — `CanPlace` + `TryPlaceLiquid(liquidTileId, x, y)` + `TryFloodFill(liquidTileId, startX, startY, maxFill)`. |
| 2 | Context surface | `IModContext.LiquidPlacement` exposes the per-mod adapter alongside the other placement surfaces. |
| 3 | Permission | Reuses `ModPermission.SpawnEntity`; no new permission bit, no handshake/protocol change. |
| 4 | Runtime gate | `ModLiquidPlacementAdapter` enforces permission, request-shape rails, and active in-world session before the Game Adapter seam. |
| 5 | Runtime → Game Adapter boundary | `IModLiquidPlacer` with `TryPlaceLiquid` / `TryFloodFill`; default disabled implementation in the Runtime-only composition. |
| 6 | Game Adapter domain | `LiquidTilePlacement` resolves the liquid-tile id through `GameAdapterLiquidTileContentProvider` and calls `FluidManager.SetLiquid` / `StartFill`; `GameAdapter` delegates the mod boundary to it. |
| 7 | Replication | The host fluid authority streams the changed grid through the existing `FluidRegion` channel; no new wire. |
| 8 | Authority boundary | Fluid placement is refused on a guest with a log; guest-initiated placement is reserved for `IModCommands` host-authoritative execution. |
| 9 | Policy rails | Reuses `ModEntitySpawnPolicy` for liquid-tile id validation (same malformed-request guard as spawn). |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModLiquidPlacement` | New Abstractions interface. |
| `IModContext` | Added `LiquidPlacement` property. |
| `IModLiquidPlacer` | New Runtime → Game Adapter boundary. |
| `DisabledModLiquidPlacer` | New Runtime-only disabled implementation. |
| `ModLiquidPlacementAdapter` | New per-mod adapter with permission/session/policy gating (top-level, not nested). |
| `ModLifecycle` / `ModService` | Wired the liquid placer through the mod-context construction path. |
| `CuoBootstrap` | Registered the disabled liquid placer in the Runtime default composition. |
| `PluginDependencyRegistrar` | Replaced the liquid placer with the Game Adapter implementation. |
| `GameAdapter` | Implements `IModLiquidPlacer` by delegating to the new liquid placement domain. |
| `LiquidTilePlacement` | New GameAdapter domain: resolves world byte, checks world/air/authority, calls `SetLiquid` / `StartFill`. |
| Tests | `ModLiquidPlacementTests` + `FakeModLiquidPlacer`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Permission gate | Missing `SpawnEntity` refuses liquid placement/flood fill | `ModLiquidPlacementTests.MissingSpawnEntityPermission_IsRefused` |
| Forwarding | With permission + in-world session, place request forwards to the liquid placer | `ModLiquidPlacementTests.WithPermission_ForwardsPlaceToGameAdapterLiquidPlacer` |
| Forwarding | With permission + in-world session, flood-fill request forwards with maxFill | `ModLiquidPlacementTests.WithPermission_ForwardsFloodFillToGameAdapterLiquidPlacer` |
| Session gate | Outside an active in-world session refused before adapter | `ModLiquidPlacementTests.OutsideInWorldSession_IsRefusedBeforeAdapter` |
| Request rails | Empty / whitespace / over-cap liquid-tile id refused before adapter | `ModLiquidPlacementTests.InvalidRequest_IsRefusedBeforeAdapter` |
| Adapter failure | A false return from the adapter is surfaced as false | `ModLiquidPlacementTests.AdapterFailure_IsReturnedAsFalse` |
| Spawn regression | Existing entity/item/tile/structure placement surfaces still pass | existing spawn/placement test families |
| DI integration | Real mod stack exposes `IModContext.LiquidPlacement` and routes through the replaced liquid placer | build + full suite green |
| No wire/protocol regression | Liquid placement rides the host fluid stream; no new NetMsg | `docs/api/mod-api.md` §4h, full suite green |

## 4. Verification design

- Pure-managed unit tests for the Runtime gate with a recording fake; no game
  assembly or Unity objects are required for the mod-side contract.
- The Game Adapter implementation stays behind the same GameAdapter compile
  boundary as the other mod surfaces; DI wiring and the full solution build
  verify the seam end to end.
- The guest refusal is deliberately inside the Game Adapter because it depends
  on the live host-authority role; the Runtime test surface verifies the
  permission/session/policy half.
- Static evidence: no new permission bit, no new NetMsg, no Unity/game type in
  Abstractions.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | full suite green (2088 passed after this slice) |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
