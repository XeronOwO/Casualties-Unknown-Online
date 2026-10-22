# The archive's two id carriers can disagree about where one id is

- Status: Done (accepted in batch `20261022-c`; the record is
  `docs/evidence/acceptance/id-carriers-can-disagree-about-placement-20261022.md`). The disagreement was
  REPRODUCED on the machine inside the report window before anything was changed
  (`.acceptance/20261022-b/`), and all five acceptance rows below pass.
- Priority: Low
- Category: Persistence / item identity / restore
- Source: found by the independent adversarial review of `review/id-resume-sources-still-unseeded.md`
  (2026-10-11), which asked what the new two-carrier seed does NOT settle. It settles WHICH ids exist;
  it says nothing about the two carriers disagreeing about one id it names twice.
- Related: `review/id-resume-sources-still-unseeded.md` (the seed and the carrier reader,
  `RestoredItemIds`), `done/item-id-counter-is-not-resumed-by-a-restore.md`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/CutCharacterPlacement.cs` (the rule this
  landing adds), `src/CasualtiesUnknownOnline.Runtime/Session/CharacterData/CharacterDataClone.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/LocalBodyPlacement.cs`,
  `docs/architecture/save-archive-format.md` §3.4 and §6.1

## The claim, and the probe that settled it

An archive writes one item's instance id in two places: the kernel checkpoint's item rows (which state
its `ItemLocation` — world, carried, contained, worn) and `characters/<playerKey>.json` (which states
the body slot that item occupies). Both are written from the live world at different moments — the
character file by the 1 Hz report (`CharacterDataHandler` → `CharacterDataStore.SaveReportedCharacterData`),
the kernel row by the item command that moved it — so for one item the two copies can describe different
places. The restore applied both halves independently and nothing reconciled them.

**Reproduced (pre-fix build, deployed `0.1.0+7b9ddfe0`).** The probe held the window open the way the
code already has it: `Body.PickUpItem`/`DropItem` report the ITEM fact at once and do NOT fire the
immediate character re-report (`OnInventoryChanged` is wired to SwapSlots/SwitchHands/WearWearable and
the craft/combine funnels — a comment in `CraftingPatches` says so, and the 1 Hz pump is the only
carrier of a PICKUP/DROP's slot change). The host parked its incoming `CharacterData` family for the
attempt (`net-drop-message`), the member created and carried a `bandage` (id 26672826005), and the host
took `/save`:

- `items.json` carries `identity.instanceId = 26672826005`, `location.kind = 2` (`Carried`, owner = the
  member) and `data.slotIndex = -1` (the pickup command carries no item data, so the row keeps the
  world item's slot);
- `characters/steam-<member>.json` carries NO row for 26672826005.

The archived pair is the ticket's claim: one id, two carriers, different places. Artifacts:
`20261022-b/p1-red-pickup-items.json`, `p1-red-pickup-character.json`, `probe.log`,
`red-host-window.txt`, `red-guest-window.txt` (under the directory named by
`acceptance-artifacts-dir`).

**A second defect the probe turned up, fixed here too.** The host's saved-character table and the clone
presentation's fact table held the SAME object: `CloneFactTable.ApplySnapshot` did `_cloneData[owner] =
data` on the snapshot the store had just saved, and a drop's `[CarriedSync] removed <id> from <peer>'s
snapshot` MUTATED it. The save carrier — the file every cut writes — could therefore change without a
report: in the same probe the host stored a 2-item report at 19:11:51.740, the drop's carried-sync
removed the item from the shared object, and the 19:11:54 cut wrote a 1-item file. That aliasing is why
the drop direction could not be raced on the pre-fix build (the mutation made the carriers agree by
accident), and it is a save-carrier defect independent of this ticket's window.

## What landed

| # | What the rule needed | Where it lives |
|---|---|---|
| 1 | ONE placement per id, decided at the cut where both carriers are known at one instant: the kernel's placement is the arbitrated fact, the file's slot stays the body's own fact | `CutCharacterPlacement.Reconcile`, called from `WorldCutWriter.Write` on the frozen checkpoint before the payload is encoded |
| 2 | an id the kernel places outside that body (world, another owner, a limb, terminal) leaves the file; its unknown-to-kernel descendants are LIFTED (with their own contents rebuilt) rather than lost with it | `CutCharacterPlacement` (removal + `LiftUnknown`) |
| 3 | an id the kernel holds on that body but the file is missing is ADDED from the kernel row — at the kernel's slot when it states one, at slot `-1` (unplaced) when it does not; an id the file states somewhere else (a take-out) is MOVED to the top and keeps the report's state | `CutCharacterPlacement` (`FromKernel`, the `-1` marker, the second pass over `fileNodes`) |
| 4 | an id the kernel has never judged stays exactly where the report put it (the report is then its only carrier — the same rule the id seed follows); an UNBOUND id (`0`) is never an identity, so every unbound row is kept and none enters the placement sets | `CutCharacterPlacement` (`byId` miss = keep; the `InstanceId == 0` guards) |
| 5 | the corrections are named, not silent: a line per correction in the log and the NET id count (added and removed, never one per action) in the cut report | `WorldCutWriter.Write` → `WorldCutWriteResult.PlacementCorrections` → the trigger's summary |
| 6 | the peer each file belongs to, which only the binder knows (the file keys by identity, the item rows by peer id) | `WorldCharacterCutSet.Owners`, `WorldCutWriteRequest.CharacterOwners` |
| 7 | the store must own the ITEM tree it stores: the clone presentation mutates its own copy, never the save carrier (`Health`/`Skills` remain shared leaves by design — the host's vitals model and the clone presentation write through them) | `CharacterDataClone` (the one deep copy, moved out of `PlayerCharacterAccess`) + `CharacterDataStore.SaveReportedCharacterData` |
| 8 | an unplaced row is placed by the restored body's own slot rules, and a refusal is named | `LocalBodyPlacement` (the one slot rung, shared with the give ladder) + `CharacterRestoreApplier.PlaceUnplacedItem` |
| 9 | the rule is the format's, not this ticket's | `docs/architecture/save-archive-format.md` §3.4 (the character file) and §6.1 (the restore), and the human internals pair (`en`/`zh`) |

The id-seed reader (`RestoredItemIds`) is untouched: this landing is about the PLACEMENT of an id both
carriers name, never about narrowing which ids exist.

## Acceptance matrix

| # | Scenario | Expected | State |
|---|---|---|---|
| 1 | The fixed build, the member creates+carries an item and the host cuts inside the report window (the host's `CharacterData` receive closed for the attempt) | The member's character file carries the item (slot -1, unplaced) while `items.json` says carried, and the cut report names the placement correction | pass (`20261022-c`: `r1-pickup-cut-items.json` kind 2/slot -1, `r1-pickup-cut-character.json` slot -1, `r1a1-host-log.txt` names the add) |
| 2 | Continue the cut of row 1 and let the member receive its character | The item is on the member's body (placed by the body's own rules) and no second copy of it exists in the world | pass (`r2-body-body.json` carries it once, `r2-kernel-kernel.json` carried=1 world=0) |
| 3 | The member drops the item and the host cuts inside the window (the store still carries it) | The member's file no longer carries it, `items.json` says World, and the cut names the correction | pass (`r3-drop-cut-items.json` kind 1, `r3-drop-cut-character.json` no row, `r3-host-log.txt` names the removal) |
| 4 | Continue the cut of row 3 | The item is in the world (not in the member's body) — one placement, no duplicate and no phantom | pass (`r4-body-body.json` no row, `r4-kernel-kernel.json` world=1) |
| 5 | The probe reads the host's stored character after the clone presentation removed a carried item | The stored snapshot still carries it (the store's copy is its own), and the next cut corrects the file | pass (`r5-store-store.json` count 2 after the pickup cut, `r3-store-store.json` count 2 after the drop's carried-sync) |

Rows 1–5 are the batch's judgement. The layer-end cut's reconcile is covered in shape by
`WorldCutCharacterReconcileTests.ALayerEndCutReconcilesTheMemberFileToo`; the batch stages a mid-run
continue, not a descent. The store copy's own regression test is
`CharacterDataCloneTests` (the copy is complete and owns the item tree); the store BOUNDARY is the
batch's row 5, because `CharacterDataStore` needs the session graph to construct.

## What the adversarial review changed in this commit

The independent review ran against the frozen tree before the commit and found 5 gate-red blockers and
11 majors/nits, all fixed here rather than filed:

- **B1 — the document census was not moved with the ticket.** The row still named the `todo/` path while
  the file lived in `review/`; both census gates were red. Fixed in `docs/standard/document-census.txt`.
- **B2 — a backlog cross-reference still pointed at `todo/`** (`review/id-resume-sources-still-unseeded.md`).
  Fixed in the same change.
- **B3 — the new index row was 169 characters** (budget 160). Shortened to a pointer.
- **B4 — `CharacterDataStore` grew past its recorded architecture-debt ceiling** (636 vs 629 lines,
  the copy's rationale comment). The rationale moved into `CharacterDataClone` and the store keeps one
  trailing note; the file is at 628 lines.
- **B5 — the container-refusal source-shape gate still scanned `PlayerInventoryPlacement.TryPlaceInBodySlot`**,
  which is now the expression-bodied delegation; the loop lives in `LocalBodyPlacement`. The gate scans
  the placement sources with `LocalBodyPlacement.cs` first (its `TryPlaceInBodySlot` is the body it
  pins), and the samples still pass unchanged.
- **M1 — two unbound (`InstanceId = 0`) items were collapsed by the duplicate guard.** Id 0 is not an
  identity: every unbound row is kept in place and none enters the placement sets; regression
  `TwoUnboundItems_AreBothKept`.
- **M2 — a lifted unknown container kept its contents, and a held descendant was also added at the top**
  (one id twice). `LiftUnknown` now rebuilds the lifted node's contents, so a held descendant is
  removed from it and re-added once; regression
  `AnUnknownContainerInsideARemovedOne_IsLiftedOnce_AndReleasesTheItemTheKernelHolds`.
- **M3 — a layer-end cut cannot remove a world placement**: the layer-boundary reset drops the
  world-rooted rows BEFORE the cut, so the id falls under the "never judged" rule and stays. That is now
  stated as a deliberate exception in the format page and in `Limits`; the test keeps covering the add
  half.
- **M4 — the store copy is deep for items/limbs, SHARED for `Health`/`Skills` by design.** The contract
  is stated in `CharacterDataClone`'s doc and pinned by `Assert.Same` in `CharacterDataCloneTests`; the
  archived file's `ownerSteamId` is the reporter's own value (0 when none) and the format page now says
  identity is the file name, never that field.
- **M5 — the store-copy boundary had no regression test.** Added
  `CharacterDataStoreCopyTests.AStoredReportIsACopy_ThePresentationCannotMutateTheSaveCarrier` over the
  production composition (the real `CharacterDataStore`), simulating the presentation's in-place removal.
- **M6 — the correction count/notes could contradict themselves.** `Added`/`Removed` are now the NET set
  difference of the file's bound ids before and after (one per id, never per action), and the removal
  notes skip descendants that are moved or lifted.
- **M7 — three comments still claimed the store and the presentation shared an object.** Updated in
  `LimbStateEventHandler`, `CharacterEventMerges` and `CharacterDataStore.RelayCharacterData`; the
  guards stay, the reasons now match the code.
- **M8 — copied claims were imprecise.** The ticket now names `SaveReportedCharacterData` (not
  `SaveCharacterData`), says the 1 Hz pump is the only carrier of a PICKUP/DROP's slot change, notes
  that an item ADDED from the kernel takes the kernel row's state, and its Related paths are
  repository-relative.
- **M9 — `TransferTableRestoreMerge`'s reason for refusing a `-1` entry was stale** ("the wearable path
  drops it"). The refusal stands for its real reason: the table cannot say whether a `-1` entry was a
  container content or a world item, so appending it would place it in a slot nobody chose.
- **M10 — the live-table mutation test was weaker than its name.** It now compares the serialized
  stored character before and after the cut, so a slot/condition/contents mutation would fail.
- **M11 — the shared-key doc over-claimed** ("carried by NEITHER" when only one of two same-named
  present players has a snapshot). The binder's doc now states the one-snapshot case and why the
  restore's claim still refuses the ambiguous key.
- Nits: the unplaced-refusal log says the copy is DESTROYED, §6.1's slot wording covers the `-1` row, and
  the correction clause sits inside the summary's parentheses.

## Limits recorded with the landing

- **The reconcile fixes PLACEMENT and membership, not the item's own data.** A carried item the file
  already states keeps the report's condition/components/slot; an item ADDED from the kernel takes the
  kernel row's state (a pickup row's data is the world item's last report, so it is as fresh as the
  carried-sync that produced it).
- **An id the kernel never judged is kept even when it is stale.** It may be a local creation the
  registration cadence has not reached yet (the report is its only carrier), or a stale drop the kernel
  refused — the cut cannot tell, and dropping it would be the silent loss this rule exists to prevent.
- **A `layer-end` cut cannot remove a world placement, and this is deliberate.** The layer-boundary
  reset drops the replaced layer's world-rooted rows BEFORE the cut is taken, so a bound id only the
  member's file still names falls under the "never judged" rule and stays. Removing it would destroy
  the item (the layer it lay in is being replaced), so keeping the body copy is the non-lossy answer;
  the format page states the exception (§3.4).
- **An unplaced item a restored body cannot take is destroyed and named in the log only.** The archive
  cannot state a slot it never knew; the local restore's refusal does not reach the Runtime restore
  account (the adapter's own local-apply name is the log). A body whose every free slot refuses it is
  the edge this leaves named rather than repaired.
- **The store copy isolates the item tree, not the vitals leaves.** `Health`/`Skills` stay shared with
  the clone presentation by design (the host vitals model writes through them); a presentation-side
  `HeadMouthRule` write can still be seen by a cut taken in the same window. The claim this landing
  makes is the PLACEMENT carrier's ownership.
- **Only the two reachable directions were raced.** The pickup direction is the reproduced one; the drop
  direction is symmetric by code reading and is staged in the batch after the store-copy fix (the
  aliasing had masked it). A cut that lands between a cross-player transfer's two halves is the same
  rule's shape, covered by the reconcile's placement cases, not raced in-game.
- The probe held the window open with the acceptance-only `net-drop-message` recipe (the host's own
  receive seam for one family). The natural window — up to one 1 Hz report — is what the code already
  has; the probe stretched it instead of racing it, and the ticket says so rather than claiming a
  natural race was won.
