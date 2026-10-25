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

## Run sheet (stage 1)

- [ ] 1. Read the row shape a projection needs: `ItemState` and `ItemLocation` (kind + owner + parent + position) and confirm the guest's
      kernel holds the same rows (the ticket's citations are the evidence; re-read them, do not recall them).
      Fail branch: if a guest cannot read its own kernel, the stage becomes "expose the rows to the adapter" and the wire is read instead.
- [ ] 2. Write `ItemCustodyProjection` + its unit tests: a row with no object, an object with no row, a display object whose row becomes real,
      a terminal row whose object still exists.
      Fail branch: a case that cannot be expressed without a second store means the projection needs a field, not that the design is wrong —
      the test states which field and why in one line.
- [ ] 3. Wire it beside the existing stores (subscription only, no reader changed), log the comparison at 1 Hz.
      Evidence: the log line with `mismatches=0` over a two-client session; every mismatch, if any, recorded here as the bug it is.
4. Gates + one full run; commit with the log path on this ticket.
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