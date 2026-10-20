# A Container Departure's Arrival Report — Self-Check (2026-10-20)

Delivery fact sheet for backlog item `review/pickup-early-return-kernel-relocation.md`: an item released out
of a container into its holder's hands states its ARRIVAL as a report of its own, so the kernel's contained
row cannot outlive the move and the bag's next container report cannot terminalize the item.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The re-pick unloads the child from its container before parenting it into a slot, and the pickup hook cancels the departure the unload opened. | `Body.cs:1393-1401` (`item.TryGetParentContainer` → `container.UnloadItem` → `SetParent(this.slots[slot])`); `PickupSync.OnPickedUp` (`_dropState.TryCancel(item, out var pickedOp, out var departure)`) |
| 2 | The same cancellation is reached by the RADIAL wear — a carried item with the wear encoding, not the applied family. | `Body.cs:1500-1508`; `ItemSlotSync.OnItemWorn` ("the same carried-fact report with the limb wear encoding"); `LimbItemLocationRules` ("an applied item is reached by no inventory gesture") vs `PlayerCamera.ApplyWoundItem` (`useLimbAction`, the applied entry) |
| 3 | The report that survives the early return is the slot move's, which the kernel applies as DATA only. | `ItemSlotSync.ReportCarried` → `ItemService.SendItemSlot` → `WireCommandKind.ItemUpdateState` → `UpdateItemStateCommand` → `ItemDataUpdatedEvent` (location kept) |
| 4 | A container's children are the CONTAINER's kernel rows, so an arrival that nobody states leaves a `Contained` row behind. | `ItemDomainModule.DecideSyncContainer` (`ItemLocation.Contained(command.Actor, child.ParentItemId)`); `ItemArbitration.AdoptCarriedContents` (spawn contained) |
| 5 | The parent's next report destroys the descendants it does not name, and Terminal is one-way. | `DecideSyncContainer`'s stale walk (`TerminalKind.ReplacedBy`); `DecideUpdate`/`DecidePickup` refuse a terminal row; `ItemKernelAuthority.TrySyncContainerFacts` logs the dropped ids |
| 6 | The kernel's relocation entry already exists; what was missing was a report whose refusal settles correctly. | `ItemKernelAuthority.TryPickup` → `PickUpItemCommand` → `ItemRelocatedEvent(old → Carried)` |
| 7 | A container child's `Contained` row keeps its creation-time owner when the container changes hands, so an eligibility rule must read the row the child SITS IN. | `ItemLocationChain.HolderOf`; `KernelProtocolCommandHandler.TryRefuseUnclaimedContainer` (`home.Value.Location.Owner`); `CreationBeforeOperationTests.AContainerSync_AfterTheContainerChangedHands_StillAcceptsTheRecordedContents`; `done/member-report-bounds.md` |

Whole-family audit: the family is "a carried-container child leaves the container by a gesture that is not a
container load and not a world drop". Two gestures reach it — the re-pick and the radial wear — and both are
covered by the same policy call (`ContainerArrivalPolicy`) at their own seams. The third gesture in the same
neighbourhood, the wound-view apply, is a DIFFERENT destination (the applied family) with its own native
entry (`PlayerCamera.ApplyWoundItem`) and is ticketed separately; the review caught the first draft of this
change mis-filing it as the radial wear.

## 2. The change

- `WireCommandKind.ItemContainerTakeOut` (31) + its payload, mapped to `PickUpItemCommand`; member-authored
  owner-predicted; re-reported by the pending-command window.
- `ContainerArrivalPolicy` (Runtime, pure): the cancelled departure's home → the report (`CarriedContainer` →
  take-out, `World` → pickup, a body slot → none).
- Both adapter seams send it before the slot/carried fact: `PickupSync.OnPickedUp` and
  `ItemSlotSync.ReportCarried`.
- `KernelCommandGateway.MayTakeIntoOwnHands` for BOTH pickup-shaped kinds, reading the home row for a
  contained item.
- `KernelProtocolCommandHandler`: an unjudged take-out is answered `UnclaimedItem` (no heal, nothing created);
  a refused creation's tombstone still answers first.
- `ItemCommandRejectionRouter`: a refused take-out settles by the carried-set statement, `InvalidTransition`
  excepted (terminal/installed rows keep the family's item reject).

## 3. The independent review, and what it changed

FULL-tier adversarial review, fresh context, frozen tree. Its findings and their disposition:

| Finding | Disposition |
|---|---|
| BLOCKER: the follow-up ticket filed the radial wear under the APPLIED family; the wear is a CARRIED item and needs this report. | Fixed: the wear's seam (`ItemSlotSync.ReportCarried`) now sends the policy's report, the ticket was rewritten as the WOUND-VIEW apply (a genuinely different entry), and the policy doc that said "the re-pick and the wear both cancel a departure there" is now the code. |
| BLOCKER: the new bound's `Contained` arm read the child's own owner, so it refused the honest take-out out of a bag that changed hands. | Fixed: the arm reads `ItemLocationChain.HolderOf` (the row the child sits in), with a positive changed-hands test; under the child-owner rule that test fails, under the home-row rule it passes (both observed). |
| MAJOR: the World home was sent as the take-out, whose settlement (re-state the carried set) is wrong for a looted world item — a refusal left a local ghost. | Fixed: the World home reports the plain pickup, so a refusal keeps the world rollback. The policy's table states it, and the world-container child's relocation is pinned. |
| MAJOR: the self-check page the ticket cited did not exist. | This page. |
| MAJOR: the plain `ItemPickup` kind bypassed the new bound (an alias hole: a pickup could take an item out of another member's bag). | Fixing: the bound now covers both pickup-shaped kinds and ANSWERS (so the pickup's rollback still runs); a test pins the refusal and the unchanged row. Red observed on the unmodified gateway. |
| MAJOR: no test proved the adapter decision was wired. | A source-shape pin on both call sites (the adapter has no harness); the runtime half stays the acceptance row, which the ticket states. |
| MAJOR: the "pre-fix failure observed" claim was misattributed (it belonged to the abandoned re-assertion route). | The ticket now separates the observed reds (the alias hole, the changed-hands rule, the re-assertion route) from the defect that has no unit red. |
| MINORs: "the kernel's only location entries" (false — the limb pair and the transfer also relocate); the census row omitted Terminal and the tombstone-first answer; the I5 row's new decision had no evidence anchor; the f-batch line attribution; the world-item test used a top-level world item; the send order is unproven; the historical self-check page carried an added status sentence. | All fixed or corrected in the text above / the files named. |

## 4. What this cycle did not verify

- No session: the adapter's actual gesture, the host's kernel row after it, the bag's next report, the
  peers' clones and the three-client row are the acceptance batch's (the ticket names the recipe shape).
- `dotnet format` was run by the implementer after the review's fixes (the reviewer is forbidden to run it,
  since it rewrites files); the clean run is recorded in the commit's log.
