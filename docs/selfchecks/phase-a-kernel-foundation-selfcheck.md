# Phase A kernel foundation self-check (2026-08-27)

This fact sheet records the first Phase A delivery (`91efd68`): the `GameState`
project, typed deterministic kernel, Items first slice, checkpoint, diagnostics
projection, isolation gate, and unit/property tests. Phase A is **not** complete:
production shadow integration and replay differential are still open.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| GameState project | `src/CasualtiesUnknownOnline.GameState/` | net48, no CUO/Unity/BepInEx/Steam/network references. |
| Kernel external contract | `IGameStateKernel.cs`, `GameStateKernel.cs` | Execute/Apply/CreateCheckpoint/Restore/Query + FindItem. |
| Transaction loop | `GameStateKernel.cs` | route -> Decide -> working copy -> Reduce -> invariants -> global revision -> atomic swap -> operation window. |
| Operation idempotency | `Kernel/CommittedOperationWindow.cs` | bounded 2048-entry window; repeats return the original batch. |
| Item domain | `Domains/Items/` | `ItemIdentity`, `ItemLocation` (World/Carried/Contained/Terminal), `ItemState`, Spawn/PickUp/Drop/Destroy commands, `ItemSpawned`/`ItemRelocated`/`ItemDestroyed` events. |
| Item reducers/invariants | `Domains/Items/ItemDomainModule.cs` | wrong revision, no Terminal resurrection, carried/contained owner/parent invariants, deterministic reduce. |
| Checkpoint | `GameCheckpoint.cs`, `GameStateStore.cs` | item table + global revision + run epoch round-trip. |
| Diagnostics projection | `Projections/ItemDiagnosticsProjection.cs`, `ItemTerminalFact.cs`, `ItemTerminalDiff.cs` | active-fact projection and semantic comparator; terminal items excluded. |
| Isolation gate | `tools/check-gamestate-isolation.ps1` | refuses CUO/Unity/BepInEx/Steam/network project and source references; called from `check-architecture.ps1`. |

## Tests

| Test file | Covers |
|---|---|
| `tests/CasualtiesUnknownOnline.Tests/GameState/GameStateKernelTests.cs` | spawn, duplicate operation idempotency, duplicate spawn conflict, stale revision, pickup/drop lifecycle, terminal no-resurrection, wrong epoch, Apply idempotency, checkpoint round-trip. |
| `tests/CasualtiesUnknownOnline.Tests/GameState/ItemDomainInvariantTests.cs` | 500 random operations; unique instance ids, positive revisions, carried owner, contained parent. |
| `tests/CasualtiesUnknownOnline.Tests/GameState/GameStateItemDefectFamilyTests.cs` | named first-slice mappings: duplicate operation, first-writer-wins conflict, duplicate drop idempotency, terminal after checkpoint restore, old-epoch rejection. |
| `tests/CasualtiesUnknownOnline.Tests/GameState/ItemDiagnosticsProjectionTests.cs` | active facts exclude terminal, comparison reports missing/unexpected/differing facts, identical facts agree. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: clean, 0 warnings/errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1586 passed, 0 failed.
- `dotnet format`: applied.
- `tools/check-architecture.ps1` (includes GameState isolation): passed.
- No production wire/protocol/behavior change: Runtime only gained a read-only
  diagnostics accessor on `ItemService`; no old item path was altered.

## Structure review

- GameState is dependency-free and deterministic: no Unity, network, file, random,
  or wall-clock calls.
- Command/Event/Effect split is explicit; phase A has no effects/network projection.
- The kernel is small (136 lines) and domain code is isolated in `ItemDomainModule`.
- No new architecture debt added; all new types stay well under the 600-line gate.
- The one known seam to revisit before Phase B: `IDomainModule` currently routes by
  `CanHandle`/`CanReduce` on the single item module; multi-domain routing should be
  re-evaluated when a second domain lands.

## Open items / next actions

1. Add the production shadow hook beside the existing item decision path.
2. Add replay differential tests over the existing item `.replay` files.
3. Triage the first-slice boundary: use/slot/container contents and craft/cook.
4. Build the historical ghost/duplicate/race defect-family evidence table.
5. Write the final Phase A self-check fact sheet once exit criteria are met.
