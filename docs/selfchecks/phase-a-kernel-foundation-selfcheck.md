# Phase A shadow kernel self-check (2026-08-27)

This fact sheet records Phase A completion: the `GameState` project, typed
deterministic kernel, Items first slice, production shadow wiring, replay
differential, defect-family evidence, isolation gate, and tests.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| GameState project | `src/CasualtiesUnknownOnline.GameState/` | net48, no CUO/Unity/BepInEx/Steam/network references. |
| Kernel external contract | `IGameStateKernel.cs`, `GameStateKernel.cs` | Execute/Apply/CreateCheckpoint/Restore/Query + FindItem. |
| Transaction loop | `GameStateKernel.cs` | route -> Decide -> working copy -> Reduce -> invariants -> global revision -> atomic swap -> operation window. |
| Operation idempotency | `Kernel/CommittedOperationWindow.cs` | bounded 2048-entry window; repeats return the original batch. |
| Item domain | `Domains/Items/` | `ItemIdentity`, `ItemLocation` (World/Carried/Contained/Terminal), `ItemState`, Spawn/PickUp/Drop/Destroy commands, `ItemSpawned`/`ItemRelocated`/`ItemDestroyed` events. |
| Item reducers/invariants | `Domains/Items/ItemDomainModule.cs` | wrong revision, no Terminal resurrection, carried/contained owner/parent invariants, world relocation drop, deterministic reduce. |
| Checkpoint | `GameCheckpoint.cs`, `GameStateStore.cs` | item table + global revision + run epoch round-trip. |
| Diagnostics projection | `Projections/ItemDiagnosticsProjection.cs`, `ItemTerminalFact.cs`, `ItemTerminalDiff.cs` | active-fact projection and semantic comparator; terminal items excluded; revision comparability optional. |
| Production shadow | `Runtime/Session/Items/ItemKernelShadow.cs` | observes host spawn/pickup/drop/destroy and craft facts beside the old path; never mutates old state or sends wire. |
| Shadow wiring | `ItemService`, `ItemPendingPickupArbiter`, `ItemMessageFlowService`, `CraftSyncService` | accepted guest/host facts flow into the kernel; no behavior change. |
| Isolation gate | `tools/check-gamestate-isolation.ps1` | refuses CUO/Unity/BepInEx/Steam/network project and source references; called from `check-architecture.ps1`. |
| Replay diff | `tests/.../Fakes/ItemSimWorld.cs`, `tests/.../Replays/ReplayTests.cs` | every item `.replay` run asserts zero semantic diff between legacy terminal facts and kernel shadow. |

## Tests

| Test file | Covers |
|---|---|
| `tests/CasualtiesUnknownOnline.Tests/GameState/GameStateKernelTests.cs` | spawn, duplicate operation idempotency, duplicate spawn conflict, stale revision, pickup/drop lifecycle, terminal no-resurrection, wrong epoch, Apply idempotency, checkpoint round-trip. |
| `tests/CasualtiesUnknownOnline.Tests/GameState/ItemDomainInvariantTests.cs` | 500 random operations; unique instance ids, positive revisions, carried owner, contained parent. |
| `tests/CasualtiesUnknownOnline.Tests/GameState/GameStateItemDefectFamilyTests.cs` | named first-slice mappings: duplicate operation, first-writer-wins conflict, duplicate drop idempotency, terminal after checkpoint restore, old-epoch rejection. |
| `tests/CasualtiesUnknownOnline.Tests/GameState/ItemDiagnosticsProjectionTests.cs` | active facts exclude terminal, comparison reports missing/unexpected/differing facts, identical facts agree. |
| `tests/CasualtiesUnknownOnline.Tests/GameState/ItemKernelShadowTests.cs` | production shadow drives spawn/pickup/drop/destroy lifecycle and session reset. |
| `tests/CasualtiesUnknownOnline.Tests/Replays/ReplayTests.cs` | all 30 item `.replay` files now also assert zero kernel semantic diff against the legacy host tables. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: clean, 0 warnings/errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1594 passed, 0 failed.
- `dotnet format`: applied.
- `tools/check-architecture.ps1` (includes GameState isolation): passed.
- `tools/check-event-replay.ps1`, `tools/check-entity-event-dispatch.ps1`: passed.
- Replay differential: 30/30 item replay files have zero semantic diffs.
- No production wire/protocol change; old item path remains authoritative.

## Structure review

- GameState is dependency-free and deterministic: no Unity, network, file, random,
  or wall-clock calls.
- Command/Event/Effect split is explicit; Phase A has no effects/network projection.
- The kernel is small and domain code is isolated in `ItemDomainModule`.
- No new architecture debt added; all new types stay well under the 600-line gate.
- Runtime -> GameState is an interim direct reference documented in the Runtime
  csproj; the final target is Runtime -> Application -> GameState.

## Known limits (documented for Phase B)

- Kernel tracks location/identity/revision only; item condition, liquids, slots,
  containers contents, use/slot flows are not part of the first slice.
- Legacy world re-drop of an already-world item is represented as a world
  relocation in `DropItemCommand`; craft entries are observed as destroy +
  carried-spawn, not as a dedicated batch transaction yet.
- The legacy path still has no aggregate revisions, so replay diff compares with
  `includeRevision: false`; revision monotonicity is covered by kernel tests.

## Phase A exit criteria

1. Old online behavior unchanged: yes (full suite green, no wire change).
2. Real item replay traces zero semantic diff: yes (30/30).
3. Random operation sequences never violate invariants: yes.
4. Shadow state explains ghost/duplicate/race families: yes via defect-family tests plus zero replay diff.
5. Kernel/domain boundary proven: yes (isolation gate + deterministic pure domain).
6. Self-check fact sheet exists: yes.
