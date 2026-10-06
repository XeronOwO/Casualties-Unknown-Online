# Container Reports Carry The Child's Place — Self-Check (2026-10-06)

Delivery fact sheet for backlog item `review/container-move-snapshot-only-sync.md`: a container move
reaches the peers as ONE kernel fact that names the parent AND where each moved child sits inside it, so
the clone fact table has nothing left for the 1 Hz snapshot to carry. Batch `20261005-d` rejected the
ticket because the monitor still warned — one millisecond after it logged the event applying — and the
reading that located the missing half is this page's starting point.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The owner's client owns the report: a move INSIDE the carried inventory reports the top-level carried ROOT in full, contents included. | `src/CasualtiesUnknownOnline.GameAdapter/Items/ContainerItemSync.cs` `OnLoadedIntoContainer` ("the parent container's FULL fact is one operation = one message") and `FindCarriedRootItem` |
| 2 | A peer never applies that report. The committed batch is projected, and a container's contents are listed from the KERNEL's contained children. | `src/CasualtiesUnknownOnline.Runtime/Session/Items/KernelBatchItemProjection.cs` `EmitCarriedFactsForBatch` → `BuildFullItem` → `BuildContents` (`Location.Kind == Contained && Location.ParentItemId == parentId`) |
| 3 | So a report committed as a bare state update reached the peers as an EMPTY container while the moved child kept its previous kernel location — precisely the pair the monitor printed. | `ItemService.SendItemCarriedSync` (the `TryUpdateState` branch, before the change) and `CloneFactTable.WarnOnDivergence` (the `nested container contents changed` + `left the inventory` pair); batch `20261005-d` artifacts `cm13-guest-insert-dogfood.json`, `marks-cm1.txt` |
| 4 | The guest's wire path already did it right: `ItemContainerSync` maps to `SyncContainerItemsCommand`, whose decision relocates every reported child and destroys the children the report no longer names. | `src/CasualtiesUnknownOnline.Application/Kernel/KernelWireMapper.cs` (`WireCommandKind.ItemContainerSync`) and `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemDomainModule.cs` `DecideSyncContainer` |
| 5 | The receiving side already pruned the old top-level copy when a parent's fact arrived with contents — the receiver was never the defect. | `CloneFactTable.ApplyCarriedSync` (`RemoveMovedDescendantDuplicates`) and `tests/CasualtiesUnknownOnline.Tests/Patching/CloneFactTableNestedCarriedSyncTests.ApplyCarriedSync_WhenParentGainsMovedChild_RemovesOldTopLevelCopy` |

Whole-family audit: the job had TWO implementations, and the wrong one is gone rather than fixed twice.
`ItemContainerSyncWriter` / `ItemKernelAuthority.SyncContainerContents` was dead in production (only tests
called it) and relocation-blind — it updated a child only when the kernel already held it `Contained` under
that same parent, so a carried child moving in stayed where it was: the same defect, one caller away from
reappearing. It is deleted, and both surviving paths commit the same command. The other three carriers on
`SendItemCarriedSync` (`PickupSync`, `ItemSlotSync`, `ItemUseSync`) were checked to pass full recursive
captures, which is what lets the contents-carrying branch cover them without a second mechanism.

## 2. The change

- `ItemKernelAuthority.TrySyncContainerFacts` builds the `SyncContainerItemsCommand` for a locally
  observed container report: the parent's data plus the flattened child facts in one atomic batch. It also
  reads the report's own stale set before the command runs and WARNS with those ids when the accepted
  command makes them `Terminal` — the branch is one-way (a Terminal child rejects the whole container sync
  from then on) and it used to run silently.
- `ItemService.SendItemCarriedSync` routes a report that carries contents through it; a report with no
  contents keeps the spawn/update path.
- `ItemContainerSyncWriter` and `ItemKernelAuthority.SyncContainerContents` deleted; the two tests that
  pinned them now pin the command path.

Structure, because the fix's own kernel write pushed a class over the gate: the split seams
`docs/backlog/watchlist/architecture-watchlist.md` had already named were taken in the same round —
`ItemKernelProjectionWiring` (157) out of `ItemService` (599 → 536) and `KernelDomainCommands` (195) out of
`ItemKernelAuthority` (577 → 449; the fix alone had carried it to 616, which the gate refused). All call
expressions of the 14 non-item command entry points are unchanged; twelve test files needed one namespace
import for the extension surface.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| The host's own container report lands in the kernel as a contained child, and the fact a peer rebuilds from that batch carries it inside the parent | `tests/CasualtiesUnknownOnline.Tests/Session/ContainerSyncProtocolTests.HostCarriedContainerReport_ProjectsTheMovedChildInsideTheContainer` — RED before the change (`Expected: Contained, Actual: Carried`), green after |
| A report naming a child the kernel does not have still creates it inside the parent, and a child the report drops is destroyed | `tests/CasualtiesUnknownOnline.Tests/GameState/ItemContainerSyncTests` (both rows, now on `TrySyncContainerFacts`) |
| The destroy branch is observable: it warns with the dropped ids, and stays quiet when the report names every child | `ItemContainerSyncTests.SyncContainerFacts_DestroysStaleChildren` (asserts the `[ContainerSync]` warning and the id) and `SyncContainerFacts_CreatesContainedChildrenAsKernelItems` (asserts its absence) |
| The receiver still prunes the parent's old top-level copy, and a move only the snapshot carries still warns | `CloneFactTableNestedCarriedSyncTests` and `CloneFactTableDivergenceMonitorTests` (unchanged, green) |
| The touched classes are under the 600-line gate | gate project `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` (passes) with the measured sizes 536 / 449 / 157 / 195 |

## 4. Limits

- No session ran in this cycle. The real-client half — the operator's and the third peer's monitor at ZERO
  over at least one full periodic cycle — is row A1, named in the ticket for the next batch, and it must
  include the guest-owner direction (the wire path) beside the host-owner direction the rejected batch
  drove.
- The unit test drives the host-local path and a real broadcast between two in-process clients; it does not
  exercise the native `Container.LoadItem` capture whose contents the report carries, so "the report
  carries the child" rests on `ItemStateCodec.CaptureItem` reading the container's `transform.childCount`
  plus the batch's own `[ContainerLoad] … root content event up to {RootType}` line.
- A container report that carries NO contents is still committed as a state update, and that split is a
  capability difference between the two paths rather than a cosmetic one: the guest's wire report can still
  deliver an empty container (whose stale kernel children the command destroys) while the host-local path
  no longer can. Deliberate for now, and read live by the next batch's take-out row.
- The destroy branch is newly reachable from the host's own pickup/slot/use carriers. The paths that empty
  a container report the LEAVING child, whose own drop/pickup command relocates it in the kernel first
  (`SendItemDropped` → `TryDrop`, `SendItemPickedUp` → `TryPickup`, both producing `ItemRelocatedEvent`) —
  one path was NOT excluded by reading: `PickupSync.OnPickedUp`'s pending-drop early return reports only a
  slot re-home (`_slotSync.OnItemRehomed`) and nothing relocates a contained record there. Reachability is
  unproven; the mechanism, the consequence and the fix direction are
  `todo/pickup-early-return-kernel-relocation.md`, and the new `[ContainerSync]` warning is what a session
  would show if it is real.
