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
- [ ] 3. Switch ONE reader — `CloneInventoryRenderer.ApplyCloneInventory` — to the query, keeping the snapshot as the fallback for a row the
      kernel has not judged yet (a restore's first frames). Evidence: a two-client session still reads `[ItemCustody] mismatches=0` and the
      clones render unchanged.
- [ ] 4. Switch `StandingItemMaterializer` and the arbitration's slot checks the same way, one commit each.
- [ ] 5. Delete what no longer has a reader; gates + full run; deploy.

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