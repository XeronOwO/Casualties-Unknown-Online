# Phase E legacy inventory self-check (2026-08-30)

Initial inventory for Phase E — Delete the Dual Architecture. This file is the
working residue list and per-batch evidence trail. It will be updated as batches
land; it is not a permission to keep legacy code indefinitely.

## Method

- Searched `src/` for `Legacy`, `Compat`, `Shadow`, `Dual`, `Old`, `double-write`,
  and the known old `NetMsg` item/enemy/player direct-result names.
- Reviewed the Phase C/D handoffs and the item control/stream surfaces.
- Classified candidates into: **dead**, **rename/guard only**, **active current path**,
  and **requires kernel-path precondition** before removal.

## Candidate matrix

| Candidate | Location | Classification | Phase E action |
|---|---|---|---|
| `ItemCheckpointStore` | `Runtime/Session/Items/ItemCheckpointStore.cs` | Dead temporary Phase B in-memory checkpoint seam; only DI registration + its own tests reference it | **Remove now** |
| `ItemDiagnosticsProjection` | `GameState/Projections/ItemDiagnosticsProjection.cs` | Shadow differential diagnostic used only by tests/fake/replay | **Done**: moved to `tests/CasualtiesUnknownOnline.Tests/GameState/ItemDiagnosticsProjection.cs` as a test-only helper; no longer in the GameState kernel project |
| `ItemService.GetWorldItemsForDiagnostics` / `KernelShadow` | `Runtime/Session/Items/ItemService.cs` | Diagnostic/internal test/production (CraftSyncService uses `KernelShadow`) access | **Done**: renamed `KernelShadow` to `KernelAuthority`; no `Shadow` token remains in `src/` |
| `ItemKernelAuthority` "Shadow-compatible conveniences" (`ObserveSpawn`/`ObservePickup`/`ObserveDrop`/`ObserveDestroy`) | `Runtime/Session/Items/ItemKernelAuthority.cs` | Convenience entry points used by CraftSyncService and tests; they still route through the kernel | **Done**: section renamed to "Kernel convenience entry points"; methods kept as kernel entry points |
| `ItemKernelAuthority.KernelForDiagnostics` | `Runtime/Session/Items/ItemKernelAuthority.cs` | Test-only accessor exposing the internal `GameStateKernel` | **Done**: removed; tests now use the public `FindItem`/`QueryItems` surface |
| `ItemReject` frame | `Runtime/Protocol/NetMsg` + handlers | Last legacy item-frame survivor, required for block-break drop refusal | Keep until block-break drops have a kernel/event path; track as Phase E precondition |
| Direct `NetMsg` frames for world/trader/chat/character/enemy-presentation | Runtime handlers/channels | Current active presentation/control paths for non-persistent or continuous features | Not Phase E dual-authority targets unless a kernel path replaces them |
| Per-domain session reset caches | `ItemService.ResetSessionState`, world/player/enemy resets | Reset paths that may bypass unified `RunEpoch`/kernel restore | Audit and unify in later Phase E batches |
| `EnemyCombatOrderPolicy` kernel-process follow-up | Runtime + phase D next actions | Extracted policy not yet feeding kernel events | Phase E candidate; `EnemyAttackMsg` stays host-order local-apply for now |

## Removed batch log

| Date | Batch | Files | Verification |
|---|---|---|---|
| 2026-08-30 | Remove dead `ItemCheckpointStore` | `ItemCheckpointStore.cs`, `ItemCheckpointStoreTests.cs`, DI registration | `dotnet build` 0 warnings/0 errors; 1792 tests passed; format + architecture/event/entity/delivery gates passed |
| 2026-08-30 | Rename `KernelShadow` -> `KernelAuthority` and remove `Shadow` naming from `src/` | `ItemService.cs`, `CraftSyncService.cs`, `ItemKernelAuthority.cs`, `ItemSimWorld.cs`, `ReplayTests.cs`, `ItemKernelShadowTests.cs` -> `ItemKernelConvenienceTests.cs` | `dotnet build` 0 warnings/0 errors; 1792 tests passed; format + architecture/event/entity/delivery gates passed |
| 2026-08-30 | Add Phase E no-legacy guard | `tools/check-no-legacy.ps1`, `tools/check-architecture.ps1`, `docs/architecture-guards.md` | Architecture gate passed (including new no-legacy scan) |
| 2026-08-30 | Move `ItemDiagnosticsProjection` out of GameState to test-only | `src/CasualtiesUnknownOnline.GameState/Projections/ItemDiagnosticsProjection.cs` -> tests; updated `ItemSimWorld.cs` and `ItemDiagnosticsProjectionTests.cs` | `dotnet build` 0 warnings/0 errors; 1792 tests passed; format + architecture/event/entity/delivery gates passed |
| 2026-08-30 | Remove test-only `KernelForDiagnostics` accessor | `ItemKernelAuthority.cs`, `ItemKernelConvenienceTests.cs`, `ItemSimWorld.cs` | `dotnet build` 0 warnings/0 errors; 1792 tests passed; format + architecture/event/entity/delivery gates passed |
| 2026-08-30 | Extend no-legacy guard with removed diagnostic markers | `tools/check-no-legacy.ps1` | Architecture gate passed with `KernelShadow`/`KernelForDiagnostics`/`ItemDiagnosticsProjection` markers enabled |

## Session reset audit (2026-08-30)

- **Kernel authoritative reset** — `ItemKernelAuthority.ResetForSession()` is the only
  production call site that creates a fresh `GameStateKernel` and increments the
  `RunEpoch`. It is invoked from `ItemService.ResetSessionState()` on `SessionEnded`.
- **Projection/transient reset** — `CharacterDataStore`, `WorldService`,
  `EntitySyncService`, `EnemySyncService`, `PlayerCarryService`, and the other session
  services clear only projection/transient caches on `SessionEnded`. They do not write
  kernel domain tables directly.
- **Protocol reset** — `KernelProtocolService.ResetForSessionEnd()` clears journal,
  checkpoint chunks, and pending batches; `RunEpoch` filtering remains the wire-level
  guard against old-epoch packets.
- **Conclusion** — no per-domain session reset bypass was found. The current design is
  already unified around a single kernel epoch reset plus projection caches; no
  additional reset coordinator is required.


## Next actions

1. [x] Land the `ItemCheckpointStore` removal.
2. [x] Rename remaining Shadow-compatible names to non-shadow names where they are production
   entry points, keeping behavior identical.
3. [x] Audit per-domain session reset paths; the audit found a single kernel reset
   path and no bypass, so no reset-coordinator refactor is required.
4. [x] Add the Phase E guard: a source scan that fails on legacy/double-write patterns that
   remain after the planned removals.
