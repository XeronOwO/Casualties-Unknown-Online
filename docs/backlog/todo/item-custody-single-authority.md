# Item custody: the adapter projects the kernel's rows instead of keeping five copies

- Status: Todo — opened 2026-10-25 by the user's decision: "directly do the big change, root it out, overturn whatever needs overturning —
  keeping it is only a hidden danger" (translated).
- Priority: Critical — every item loss reported in October 2026 has been two of the adapter's own copies disagreeing, or a Unity callback read
  as intent; the kernel already holds the single answer and the adapter never reads it.
- Category: Item custody / cross-player transfer / clone projection

## Design

`docs/design/model/item-custody.md` is the design, and it already states the target: "an item in CUO is one authoritative identity", custody is a
typed location (`WireItemLocationKind`: World / Carried / Contained / Terminal / Applied, with `WireItemLocation` carrying "the kernel's single
authoritative item location"), and an incarnation "is never the authority for the item's state". The page needed one `Gaps` line, not a new
design: the ADAPTER does not read that location at all.

## The measured gap

- `ItemLocation` appears **zero** times anywhere under `src/CasualtiesUnknownOnline.GameAdapter/` — the authority exists
  (`GameState/Domains/Items/ItemState.cs`, `ItemLocation`; on the wire `Protocol/Wire/WireItemLocation.cs`; readable per role through
  `Application/Kernel/IKernelItemFacts.cs` on the host and through the guest-side replay of the same kernel, `Runtime/Session/Items/ItemKernelAuthority.cs`:
  "Raised after a remote/replay batch is applied to this authority's kernel (guest side)").
- Instead the adapter keeps five copies of "where is it": `Runtime/Session/Items/ItemArbitration.cs` (502 lines, the transfer table),
  `GameAdapter/Character/CharacterDataCapture.cs` + `CharacterItemMsg.SlotIndex`, the world table (`ItemService`/`WorldItem`),
  `GameAdapter/Character/CloneFactTable.cs` (532), and the display objects `CloneInventoryRenderer.cs` / `StandingItemMaterializer.cs` (431).
- Three layers turn engine events into facts — `Items/ItemWorldSync.cs` (517), `Items/PickupSync.cs` (212), `Items/ItemReconcile.cs` (240) —
  and because a `Destroy` cannot distinguish "the item died" from "the item moved", two of them suppress facts by zeroing the instance id
  (`ItemApplication.KillRemoteItem`, `PlayerInventoryPlacement.TryDestroyById`).

## The two losses this produced (evidence)

- `RemoteIntentSlotRelease.Plan` implemented ONE of the game's two release arms; the both-held case dropped the item occupying the destination
  and never picked it back up (`give-degrades-to-a-ground-drop-and-rearranging-retires-it.md`, fixed by `17dd186`).
- `TryDestroyById` destroyed the giver's copy of a transferred item and left its id on it, so a destroy was reported for an item that had only
  moved; the kernel marked the row `Terminal` and refused its new owner's later state updates — `ItemUpdateState refused (InvalidTransition)`
  in the guest's log at 18:02:01.328, fixed by `bb0e69d2`.

## Stages (one commit each, gates green, the previous stage's evidence required)

1. **The projection type, shadow mode.** `ItemCustodyProjection` in the adapter: `instance id → local object`, each object kinded
   **real** (this client simulates it) or **display** (another member's row incarnated locally). Fed from the kernel rows the client already
   holds (`IKernelItemFacts.QueryItems()` where the kernel is local, the guest-side authority's rows otherwise). Computed BESIDE the existing
   stores with a mismatch line naming the row and both claims; no reader switched yet.
   Done when a session of give/rearrange/eat logs `[ItemCustody] rows=… objects=… mismatches=0`.
2. **Reads switch.** `CloneInventoryRenderer`, `StandingItemMaterializer` and the arbitration's slot checks read the projection; their own
   copies become derived views.
3. **Writes become commands.** Transfer apply, slot release, give and drop issue the kernel command first; the Unity callbacks validate the
   pending command instead of reporting; both id-zeroing suppressions are deleted.
4. **Delete the duplicates and the kill loops.** The arbitration's position copy, the clone table's independent custody, `ItemReconcile`'s
   kill pass, and every `Suppress*` flag that exists only to hide a callback.
5. **The machine rows.** Two clients, give + rearrange + eat, both clients' item state read before and after.

## Run sheet (stage 1) — DONE 2026-10-25, evidence below

- [x] 1. Read the row shape a projection needs: `ItemState` and `ItemLocation` (kind + owner + parent + position) and confirm the guest's
      kernel holds the same rows — evidence: `ItemState` is `(ItemIdentity, Revision, ItemLocation)` and `ItemLocation` is
      `(Kind, Owner, ParentItemId, LimbIndex, X, Y)` with `World/Carried/Contained/Applied/Terminal` factories; the guest holds the same
      rows because both sides run the kernel (`Runtime/Session/Items/ItemKernelAuthority.cs`: "Raised after a remote/replay batch is applied
      to this authority's kernel (guest side)"), and `IItemControl.QueryJudgedItems()` now exposes them to the adapter.
- [x] 2. Write `ItemCustodyComparison` + its unit tests — evidence: `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemCustodyComparison.cs`
      with seven tests in `tests/CasualtiesUnknownOnline.Tests/Items/ItemCustodyComparisonTests.cs` (object without row, live row without
      object, terminal row with no object, terminal row with a ghost object, this client's carried row is real, another member's is a
      display, a world row is real on every client), 7/7 in `.acceptance/l521-test.log`.
- [x] 3. Wire it beside the existing stores (subscription only, no reader changed), log the comparison at 1 Hz — evidence:
      `ItemCustodyProjection` ticks from `GameAdapter.Update`, and the two-client session `.acceptance/batch-custody2` read
      `[ItemCustody] rows=285 objects=285 mismatches=0` on BOTH clients (host 19:20:35.936, guest 19:20:41.172; the guest's first lines
      showed `objects=1`, the world still streaming, and it converged as the objects materialised).
- [x] 4. Gates + one full run — evidence: `.acceptance/l523-full.log` 802/802 gates 6511/6511 behaviour; the shape gate's 600-line ceiling
      was met by trimming `ItemService` (one expression-bodied member) and by recording the adapter's own +4 lines in
      `docs/architecture-debt.json`.

**What the shadow run found and corrected (the reason stage 1 exists):** the first comparison declared a WORLD row real only on the
client that simulates the world, and the guest logged 279 `KindDisagrees` immediately — a world item is simulated locally wherever the
scene shows it, so the rule became "real on every client; only another member's carried/contained/applied row is a display". That
correction came from the machine, not from a guess, and is now the rule the tests pin.

## Run sheet (stage 2, the next cycle)

- [x] 1. Name the display objects' identity space — evidence: no new space is needed and the design page's `Incarnations` now says why:
      an incarnation is the row's instance id plus a MARKER (`RemoteCloneRender` for a clone render, `StandingItemObject`/`StandingItems.Is`
      for a standing object), so a display is "a marked object whose id is its row" and a second identity would be a second place to keep
      in step. The slot a render needs is also the row's own fact — `ItemData.SlotIndex`, "the owner-local slot" — so a reader can ask the
      authority instead of its own table.
- [x] 2. The query a reader uses — evidence: `ItemCustodyComparison.TryFindRowInSlot(rows, owner, slot)` answers the row a clone render
      should show, never a Terminal one; two tests pin it (owner+slot, and a Terminal row never answering a slot), 9/9 in
      `.acceptance/l530-test.log`.
- [x] 3. Switch ONE reader — `CloneInventoryRenderer.ApplyCloneInventory` — to the query, keeping the snapshot as the fallback for a row the
      kernel has not judged yet (a restore's first frames) — evidence: the selection rule now lives in
      `GameAdapter/Character/CloneItemSelection.cs` (`ItemInSlot`), the renderer asks it for both the slot and the worn/limb branch, and the
      snapshot's own `SlotIndex` is the fallback only. Machine read: `.acceptance/batch-render`, host `[ItemCustody] rows=277 objects=277
      mismatches=0` (19:35:19.557) and guest the same after its streaming settle (19:35:22.646), while the guest's clone render still draws
      the owner's item — `[CloneRender] apply 1 items to clone slots (6 slots)` at 19:35:41.988.
- [x] 4. Switch `StandingItemMaterializer` to the authority — evidence: `StandingItemPlan.BuildFromRows(owner, items, isWorldRow, rows)` takes the
      SET from the kernel's rows for that owner (parent-before-child by the row's own `ParentItemId`) and the payload from the snapshot; a row
      the snapshot cannot describe is skipped, and an owner the kernel has no row for falls back to the snapshot walk so nothing blinks out
      while the authority catches up. Three tests pin those cases (`TheSwitchedPlan_TakesItsSetFromTheAuthority`,
      `..._SkipsARowItCannotDescribe`, `..._FallsBackToTheSnapshotWhenTheKernelHasNoRow`), 8/8 with the existing ones in `.acceptance/l549-test.log`.
      Machine read, `.acceptance/batch-standing`: the guest materialized the host's carried row (`[StandingItem] … 1 carried row(s) in the data,
      1 standing object(s)` at 19:44:36.334) and both clients settled at `[ItemCustody] rows=233 objects=233 mismatches=0`.
- [x] 5. Switch the arbitration's slot checks — evidence: **no code needed, the derivation already exists**, which is why this step is one
      citation rather than a rewrite: `ItemArbitration.RebuildCarriedTableFromKernel()` (`Runtime/Session/Items/ItemArbitration.cs`) rebuilds
      the whole transfer cache from `_kernelAuthority.QueryItems()` where the row's kind is `Carried`, and its own doc says why ("the kernel
      is the single source of truth for carried ownership; this method is called after each external wire-command batch so the legacy transfer
      cache converges without duplicating authority"). It runs on the host after every committed batch and on the projection-health recovery
      (`ItemKernelProjectionWiring.cs`: `_arbitration.RebuildCarriedTableFromKernel()` in `OnExternalBatchCommitted` and `RebuildProjection`).
      The guest does not arbitrate take/give, so it has no table to derive. Recommending a new mechanism here would have been the mirror
      error: the mechanism exists.
- [x] 6. Record what still duplicates the authority, and why the rest cannot be deleted before stage 3 — evidence:
      (a) the readers keep the snapshot PAYLOAD (definition, condition, contents) because a row carries identity/location/data but not the
      game definition object a render or an instantiation needs — that is the owner's own report, not a custody copy;
      (b) `ItemArbitration`'s `_transferred` is now a CACHE rebuilt from rows, not an independent truth;
      (c) the two id-zeroing suppressions (`ItemApplication.KillRemoteItem`, `PlayerInventoryPlacement.TryDestroyById`) and `ItemReconcile`'s
      kill loop still exist because they are the REPAIR path for the writes that are not commands yet. Deleting them before stage 3 would
      remove the only recovery the current write path has. Stage 3 makes the local mutations issue commands first; the deletions follow there,
      not here.

## Run sheet (stage 3) — judged 2026-10-25: the writes already command-first

- [x] 1. Enumerate the local mutations and check each one issues a command — evidence: the command surface is the one the adapter already
      uses. `ItemProjection` (the adapter's bridge from local object events) calls `_authority.TryPickup` / `TryDrop` / `TryDestroy` /
      `TryTransfer`, and the same calls appear at seventeen sites across the runtime: `CraftCarriedRowRetirement`, `CraftSyncService` (twice),
      `ItemArbitration`, `ItemProjection` (five), `OtherMedicalOperationApplier` (twice), `PlayerHealService` (twice),
      `PlayerInventoryTakeService`, `PlayerItemUseCommit` (three). The authority side owns the same four verbs
      (`ItemKernelAuthority.TryPickup/TryDrop/TryDestroy/TryTransfer`, each building its typed command with the row's current revision).
- [x] 2. Name what is NOT yet a command — evidence: exactly two shapes, and neither is a write path that could issue one:
      (a) the id-zeroing suppressions (`ItemApplication.KillRemoteItem`, `PlayerInventoryPlacement.TryDestroyById`) that stop a moved item
      being reported as destroyed — a report filter, not a mutation;
      (b) the repair loops (`ItemReconcile`'s kill sweep, `StandingItems`' retire scans) that bring the SCENE back in line with the data after
      an object survives its row — a reader-side reconciliation.
      Converting either into a command would invent a write that has no player action behind it, which is the "replayed effect" shape the
      rules forbid.

**Consequence for stage 4.** With stages 2 and 3 both answered by the architecture that already exists, the remaining work is deletion: five
stores still hold their own copy of "where is it" even though nothing needs that copy.

### Stage 4, step 1 — the transfer cache's identity role (started 2026-10-25)

- [x] One production reader repointed — evidence: `CraftSyncService.ApplyGuestReport` asked `_arbitration.IsTransferredToGuest`; it now asks
      `_items.OwnsCarriedItem(sender, id)`, which is `ItemKernelAuthority.OwnsCarriedItem` → `ItemCustodyRows.OwnsCarried(rows, …)`. The
      predicate is ONE shared function precisely so the two sources cannot drift by argument: the cache is rebuilt from the same rows
      (`RebuildCarriedTableFromKernel` keeps the `Kind == Carried` rows).
- [x] The predicate pinned — evidence: `ItemCustodyRowsTests` (three cases: a carried row is its owner's, a world/terminal row is not carried,
      an id with no row is not owned), 3/3 in `.acceptance/l574-test.log`.
- [x] Dead custody query deleted — evidence: `ItemArbitration.IsTransferredToAnyGuest` had **zero** production or test consumers; it and its
      `_transferred.Values.Any(...)` scan are gone.
- [x] **JUDGED 2026-10-26** (`.acceptance/batch-craftrow2`): a real craft from a guest reaching `ApplyGuestReport` — the fixture came from the recipe table itself: `craft-drive mode=materials index=<n>` named recipe 7 (`glowplantfruit` → `biochem`, ONE specific ingredient), the guest forged that fruit, `mode=list` then answered `craftable: 1`, and `mode=make index=7` crafted it. Both ends logged the chain: guest `[ItemTrace] op=45 origin=CraftReport`, `[PickUpResult] craftingbottle → slot 5`, `[Crafting] Craft committed: 1 entries, 1 products`; host `[CarriedSync] added craftingbottle (id 30967793301)`, `[Crafting] Craft of 76561198863287957: 1 entries, 1 products applied`, `Craft report (Craft) from 76561198863287957` — i.e. the repointed reader (`OwnsCarriedItem`) answered on the real path. Custody stayed clean across it: host `275/275/0`, guest `269/269/0`', could not be driven in the batch
      `.acceptance/batch-craft`: a fresh world reports `craftable: 0` with an empty material list (`craft-drive mode=list index=0`), so no craft
      is possible without a fixture that supplies materials. What the batch DID read: both clients at `[ItemCustody] rows=282 objects=282
      mismatches=0` (host 19:59:38.646, guest 19:59:41.167) with the changed line deployed — the projection was unaffected. The craft row is
      judged in a batch with materials on hand, and this box stays open until then.
- [ ] `IsTransferredToGuest` itself stays: two acceptance recipes read it by name (`carried-registration`, `container-read`), and its doc now
      says it is the derived view rather than a truth.

The second candidate is the clone fact table's custody fields, and the third `ItemReconcile`'s kill sweep — in that order, because that is the
order in which a mistake is cheapest to see.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | A give followed by a rearrange inside the receiver's backpack keeps the item, and its row never goes terminal | not run (the user read the give once: works, nothing lost) |
| 2 | An item dragged from a slot onto an occupied slot keeps both items | not run |
| 3 | A display object rebuilt by a clone refresh never reports a destroy for its row | not run |
| 4 | A keyframe that omits a carried row leaves the row alone and repairs the local object instead | not run |
| 5 | The eat flow's peel step and the item's custody agree after the replacement | not run |

## Limits

- Stages 2-4 delete code other surfaces read today; each commit names what it removes, and no stage lands without the previous stage's machine
  evidence.
- The display objects' own identity space is unnamed today; stage 1 kinds them in the projection, and stage 2 may need the name the design
  page's Incarnations section implies.

### Stage 4, the craft row — a second attempt, and what it needs (2026-10-26)

`.acceptance/batch-craftrow`: three clients in one world, the guest forged `stick` (slot 0), `scrapmetal` (slot 1) and `rope` (slot 2) with the
product's own grant path, and `craft-drive mode=list` still answered `craftable: 0` with an empty result list. So the blocker is not "no materials on
the body" — it is that none of those three satisfies a recipe's material set.

Two ways forward, and the ticket needs one of them before that row can be judged:
1. read a concrete recipe's material list from the game's own table and forge exactly those ids (the honest fixture), or
2. unlock a recipe and craft it with the materials it names, which is what `recipe-unlock-forge index=<n>` + `craft-drive mode=make index=<n>` are for.

What the batch DID confirm: the world entry and the guest's item creation are healthy, and `[ItemCustody]` stayed at `mismatches=0` on both clients
while the three materials existed. The row stays open.

### The two-ended reading the objective asked for (2026-10-26, `.acceptance/batch-custody-two-ended`)

Three clients in one world, `.acceptance/latest` logs read after the stream settled:

| client | rows | objects | mismatches |
|---|---|---|---|
| host | 268 | 268 | 0 |
| guest (Steam1) | 269 | 269 | 0 |
| alt (Steam2) | 270 | 270 | 0 |

**One artefact worth recording**, because it looks like a failure and is not: the FIRST custody line each guest printed, about a second after world
entry, was `rows=268 objects=1 mismatches=267`. The projection enumerates this client's own objects, and at that instant the guests had materialized one
of them; the very next line is 269/269/0. So a mismatched EARLY line during world entry is the stream, not a divergence — the reading that counts is the
settled one, and a batch that reads the first line and stops is reading the stream.

That closes the objective's "confirm the two-ended reading" step for stage 1. Stage 4's deletions and the craft row remain as the ticket states.

### Stage 4, step 2 — the clone fact table, judged by reading (2026-10-26)

Candidate 2 was "the clone fact table's custody fields". Reading it answers the step the way stage 3 was answered — with a citation rather than a
deletion:

- `GameAdapter/Character/CloneItemSelection.cs` asks the AUTHORITY first (`ItemCustodyComparison.TryFindRowInSlot`) and uses the snapshot entry only
  for the visual payload; the snapshot's own `SlotIndex` is the **fallback for the frames before the kernel has judged the row** — a restore's first
  snapshot, an item the owner just picked up. Deleting the fallback blinks a clone out in those frames, which is a regression, not a deletion.
- `CloneFactTable.ApplyCarriedSync` maintains the snapshot TREE (contents/nesting) and, for a slot-unknown event, keeps the entry's previous slot
  (`item.SlotIndex = data.Items[idx].SlotIndex`). That is the fallback's data, not a competing authority: the render's answer comes from the row.
- The two id-zeroing suppressions are report FILTERS (stage 3, item 2) and `ItemReconcile`'s sweep is reader-side reconciliation — converting either
  into a command would invent a write with no player action behind it.

So the fact table is a **derived view** (payload + pre-judgement fallback) and stays; what stage 4 could still delete was the dead custody query, which
is gone (`IsTransferredToAnyGuest`). The rule is now pinned by two tests in `ItemCustodyComparisonTests` —
`TheCloneRender_PrefersTheRowOverTheSnapshotsOwnSlot` (the row wins, so no second authority creeps back) and
`TheCloneRender_FallsBackToTheSnapshotsSlotWhenNoRowAnswers` (the fallback exists on purpose) — so a future deletion attempt fails loudly.