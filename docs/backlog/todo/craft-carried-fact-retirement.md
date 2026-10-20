# A craft report never retires a carried material

- Status: Todo
- Priority: Medium
- Category: Crafting / clone fact table (carried-fact event coverage and report ordering)
- Source: independent adversarial review of `review/snapshot-overtakes-item-reports.md` (2026-10-20) —
  the review of that fix read the craft branch and found the inversion it did not close. Code reading
  only; no craft has been driven since the finding.
- Related: `review/snapshot-overtakes-item-reports.md` (the three-gesture fix this is the sibling of),
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyItemPatches.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/CraftingPatches.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Items/CraftingSync.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftSyncService.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Character/CloneFactTable.cs`

## Mechanism (code reading)

- `Recipe.TryMake` consumes the materials first, then spawns the product
  (`reversing/Assembly-CSharp/Assembly-CSharp/Recipe.cs:179`: `this.items[i].UseItem(itemsForRecipe[i])`
  in a loop; `:181`: `this.result.SpawnResult(this.INT)`). A destroyable non-liquid material's `UseItem`
  does `PlayerCamera.main.body.DropItem(item)` then `Object.Destroy(item.gameObject)`
  (`RecipeItem.cs:182-183`), and `DropItem` unparents immediately (`Body.cs:1448`:
  `item.transform.parent = null;`).
- `RecipeResult.SpawnResult` creates the product and lands it through `Body.AutoPickUpItem`
  (`RecipeResult.cs`: `Utils.Create(...)` then `PlayerCamera.main.body.AutoPickUpItem(component2)`). A
  wearable product is WORN there, and a wearable already in that slot is `DropItem`ed first
  (`Body.cs:525-527`).
- `CraftingPatches.RecipeTryMakePatch` opens `CallContext.Origin.Craft` for the whole native call. Under
  that scope `BodyItemPatches.DropItemPatch` suppresses the displaced wearable's drop report, and
  `WearWearablePatch.Postfix` skips the product's wear fact (its fact rides the ONE craft report) — but it
  still calls `PatchBridge.Impl?.OnInventoryChanged()`. That snapshot is captured after the materials were
  consumed and after the old wearable was dropped, and BEFORE `CraftingPatches.RecipeTryMakePatch.Postfix`
  commits the craft report (`OnCraftEnd` runs first there) and `CraftingSync.OnCraftEnd` allocates the
  product's id (`_ids.EnsureId(carried)` in the product loop).
- The product is id-less in that snapshot, so the divergence monitor skips it as unbound. The consumed
  materials and the displaced wearable are NOT: they were carried with ids (the 1 Hz snapshots carried
  them; `CraftingSync.OnCraftBegin` stamps every material's id before the native call), so
  `CloneFactTable.WarnOnDivergence`'s missing-entry loop logs
  `left the inventory without an event sync` for each of them.
- The craft report does not cure the fact table either: a `Destroyed` entry retires a row only when it is
  a world row (`CraftSyncService.ApplyGuestReport` → `CraftVerdict.WorldDestroy` →
  `RemoveWorldItemLocal`; the relay half says so in its own comment — "only the crafter ever had it; this
  side's clone fact table heals via the 1 Hz character snapshot"). A carried row takes
  `CraftVerdict.TransferredRemove` (transfer table + kernel row) and never reaches
  `CharacterDataSync.RemoveCarriedItem`, whose only caller is the `ItemDropped` event
  (`GameAdapterSessionBinding`: `ItemDropped += OnCarriedItemDropped` → `RemoveCarriedItem`). So the
  post-craft snapshot warns even after the report, on the host and on the relay's receivers.
- The non-craft auto-pickup path is not inverted: there the displaced item's drop report IS registered, so
  `OnInventoryChanged` defers on the pending drop. Only the Craft scope suppresses that report.

## What a fix has to decide

- **Retire the carried row from the fact table when the craft report says it was destroyed.** The craft
  report IS the event; it has to reach the same removal seam the drop event uses, on the host's apply and
  on the relay's apply (`CraftSyncService.ApplyGuestReport` / `ApplyRelayed` → the item domain's
  carried-removal event → `CharacterDataSync.RemoveCarriedItem`). Reusing the destroy event wholesale is a
  decision, not a given: `ItemApplication.OnRemoteItemDestroyed` deliberately refuses to kill a carried
  item's OBJECT ("A carried item or a remote clone display proxy must never be killed by a remote
  destroy"), so the fact-table retirement and the object retirement must be separated, or that event given
  a fact-table half by name.
- **Skip the in-craft snapshot** (`WearWearablePatch.Postfix` under `Origin.Craft`): the craft postfix
  already sends one right after `OnCraftEnd`, so the operation still gets one immediate re-report, now
  after the report that explains it. The two craft entry points are the only `Origin.Craft` entrants, and
  both send that snapshot.
- A pin for both halves: a source-shape assertion that the in-craft branch cannot re-acquire the
  snapshot, and a test that a craft report's destroyed carried entry removes the item from the clone fact
  table (`CloneFactTableDivergenceMonitorTests`' reflective harness can host the second half).

## Acceptance

The owner crafts a wearable product whose recipe consumes a destroyable, id-bound carried material (and
displaces a wearable in the same wear slot when one is reachable) on the deployed artifact: every peer's
window over the craft carries **zero** `[CharSync] divergence` for the consumed material and the displaced
wearable across at least one full 1 Hz cycle, the craft report's application line is present in the same
window, and the peers' fact-table read shows the material gone before the post-craft snapshot arrives.

## Limits (until the run)

- Filed from code reading during the review of `review/snapshot-overtakes-item-reports.md`; no craft has
  been driven against this path, so the warning is unobserved at runtime and the ids/slots of a real
  wearable recipe are not yet staged.
