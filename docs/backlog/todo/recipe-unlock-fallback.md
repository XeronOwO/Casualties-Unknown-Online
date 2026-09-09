# Recipe unlock has no fallback or backfill

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / crafting
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row I6)
- Related: the crafting report half is healed by the item keyframe/character snapshot (matrix rows I3/P3); `todo/sync-cadence-review.md`

## Problem (evidence)

`RecipeUnlock` (NetMsg 77) is one-shot in both directions:

- Guest report: `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftSyncService.cs:208`
  (`_sender.Send(_session.HostSteamId, NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = recipeIndex });`),
  triggered by `src/CasualtiesUnknownOnline.GameAdapter/Items/CraftingSync.cs:366`
  (`_craft.SendRecipeUnlock(blueprint.recipeIndex);`).
- Host relay: `CraftSyncService.cs:214` / `:219` / `:223`; the apply is a
  per-process static write (`src/CasualtiesUnknownOnline.GameAdapter/Items/RecipeUnlockApply.cs:41`,
  `recipe.INT = 0;`).
- No periodic re-send and no world-entry / checkpoint member: the ordered
  world-entry group `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs:34-44`
  has no recipe entry, and a late joiner never learns an unlock that happened
  before it joined.
- `CraftReport` itself (NetMsg 76) is better off: the host adopts untracked
  carried entries (`CraftSyncService.cs:137-145`) and the durable item state is
  healed by the 5 s item keyframe (row I3) and the 1 Hz character snapshot
  (row P3). The gap is the unlock state.

## Goal

An unlocked blueprint converges on every peer (host and guests) without a
reconnect, including late joiners, while the host remains the authority over
which recipes are unlocked.

## Design direction (decide at implementation)

1. **Unlock set in the backfill group** — carry the unlocked recipe-index set in
   the world-entry group / kernel checkpoint (or a small absolute message next to
   it) and re-send it on the existing 60 s cycle.
2. **Hash/dirty re-request** — the guest sends its unlock hash; the host answers
   with the authoritative set when it differs.
3. **Accepted loss** — rejected: a missing blueprint changes the crafting list,
   a user-visible divergence.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest uses a blueprint; the report is dropped | Host and third parties learn the unlock without reconnect |
| 2 | Host unlocks a recipe; the relay is dropped | Guest converges |
| 3 | Late joiner | Receives the current unlock set exactly once |
| 4 | Reconnect | Same; no duplicate unlock side effects |
| 5 | Duplicate delivery | Idempotent (`recipe.INT` write is already idempotent) |
| 6 | Host rejects / does not have the recipe | No unlock is applied anywhere; the failure is observable |
| 7 | CraftReport dropped at the same time | The item facts still converge via I3/P3; the unlock converges via this ticket |

## Non-goals

- Crafting recipe balance or content changes.
- The `CraftReport` item-fact path (already healed by the keyframe/snapshot).
