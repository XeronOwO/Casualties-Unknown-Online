# Mod entity spawn — mechanism inventory and self-check

Owner cycle: backlog Phase 4 Mod API remainder, TODO "custom entities".
Decision: implement the first live `SpawnEntity` surface as a **native
`BuildingEntity` prefab spawn + reuse the existing runtime-entity channel**.
A mod names a game prefab id and a world position; CUO creates the local copy
through the Game Adapter and lets the normal `EntitySpawned` path replicate it
to every peer. No new wire message, no protocol bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Public API | `IModContext.EntitySpawn` + `IModEntitySpawn` in `CUO.Abstractions` — the only assembly mods may reference. |
| 2 | Permission | `ModPermission.SpawnEntity` gets its first live enforcement point in `ModService.EntitySpawn`; the policy already refuses that flag on `ClientOnly`/`Cosmetic`. |
| 3 | Runtime gates | `TrySpawn` requires `SessionActive` + `LocalInWorld`; a spawn outside a live world is refused. |
| 4 | Request rails | `ModEntitySpawnPolicy`: non-empty prefab id ≤128, finite X/Y/rotation. |
| 5 | Replication | The Game Adapter creates the local `BuildingEntity` (`Utils.Create`); the existing `BuildingEntity.Start` → `EntitySpawnedMsg` (NetMsg 68) path reports/relays/replays it. |
| 6 | Boundary | `IModEntitySpawner` is a narrow Runtime → Game Adapter seam; disabled/no-op in the Runtime-only composition, replaced by the plugin with `GameAdapter`. |
| 7 | Wire | No new NetMsg, no direction-table row, no ProtocolVersion change. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModContext` | Added `EntitySpawn` property (new public API surface). |
| `IModEntitySpawn` | New binding contract in `CUO.Abstractions`. |
| `IModEntitySpawner` | New Runtime boundary contract (Game Adapter implements). |
| `ModService` | New `ModService.EntitySpawn.cs` partial: permission/session/policy gate + `ModEntitySpawnAdapter`. |
| `ModEntitySpawnPolicy` | New pure request-shape rails. |
| `DisabledModEntitySpawner` | Default no-op registration keeping the Runtime-only/test graph constructible. |
| `CuoBootstrap` | Registered the disabled default; the plugin replaces it with the real adapter. |
| `GameAdapter` | New `GameAdapter.ModEntitySpawn.cs` partial forwarding to `EntitySpawnSync.TrySpawnFromMod`. |
| `EntitySpawnSync` | New `TrySpawnFromMod`: `Utils.Create` + BuildingEntity check + rotation; a non-entity local object is destroyed. |
| `Plugin` | Registered the real `IModEntitySpawner` from `GameAdapterImpl`. |
| Protocol version | Unchanged (no wire change). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Public API | `IModContext.EntitySpawn` / `IModEntitySpawn.TrySpawn` | Contract + tests below; `docs/api/mod-api.md` §4h. |
| Permission enforcement | `CanSpawn` and `TrySpawn` require `ModPermission.SpawnEntity` | `ModEntitySpawnTests.MissingSpawnEntityPermission_IsRefused`. |
| Session/world gate | Refuses outside active in-world session | `ModEntitySpawnTests.OutsideInWorldSession_IsRefusedBeforeAdapter`. |
| Request rails | Invalid id/NaN/infinity refused before adapter | `ModEntitySpawnTests.InvalidRequest_IsRefusedBeforeAdapter`, `PolicyRails_AreExactAndNoSilentFallback`. |
| Adapter delegation | Valid request forwards prefab/position/rotation | `ModEntitySpawnTests.WithPermission_ForwardsToGameAdapterSpawner` (recording fake). |
| Adapter failure | Unknown/non-building prefab returns false | `ModEntitySpawnTests.AdapterFailure_IsReturnedAsFalse`; `EntitySpawnSync.TrySpawnFromMod` destroys non-entity copies. |
| Replication unchanged | Reuses existing `EntitySpawned` channel | No new NetMsg; full suite green; `EntitySpawnSync` Start-report path untouched. |
| No wire/protocol regression | No new NetMsg, ProtocolVersion stays 32 | `docs/api/mod-api.md` §7; full suite green. |

## 4. Verification design (development-period, no manual acceptance)

- L0 simulation over the real `ModService` / `TestNode` stack: permission
  refusal, session/world gate, malformed request refusal, adapter delegation,
  adapter failure, policy rails. The Game Adapter's `Utils.Create` path is
  covered by the existing `EntitySpawnSync` channel contract (it is not
  exercised in managed tests because the game assembly is not loaded here).
- Static evidence: no new NetMsg; the spawn surface only calls an existing
  runtime-entity message; the mod-facing API stays in `CUO.Abstractions`.
- Runtime verification box: **L0 simulation + static evidence, no manual
  acceptance** (user rule 2026-08-16).

## 5. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1160 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean for tracked/untracked source (only ignored `obj/MyPluginInfo.cs` outside git) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass (arch 600-line/state-bool/one-type gates) |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game directory only |
| `check-delivery.ps1` | pass (checked boxes tracked in `../delivery-checklist.md`) |
| No manual acceptance | per development-period rule |
