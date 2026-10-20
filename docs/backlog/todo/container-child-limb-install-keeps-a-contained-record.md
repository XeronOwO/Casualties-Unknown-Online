# A limb component installed straight out of a bag leaves a contained kernel record behind

- Status: Todo
- Priority: Low-Medium
- Category: Item sync / kernel locations (the applied family's eligibility rule)
- Source: found while fixing `done/pickup-early-return-kernel-relocation.md` (2026-10-20), which took the
  radial-wear half of the same cancelled departure and left this one — the WOUND-VIEW apply, a different
  native entry and a different kernel destination.
- Related: `done/pickup-early-return-kernel-relocation.md` (the carried destinations: re-pick and radial
  wear, both accepted by batch `20261025-b`), `done/container-move-snapshot-only-sync.md`
- Evidence: `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:739-763` and `:1656-1659`,
  `src/CasualtiesUnknownOnline.Application/Kernel/KernelCommandGateway.cs` (`MayInstallOnOwnLimb`),
  `src/CasualtiesUnknownOnline.GameState/Domains/Items/LimbItemLocationRules.cs` (`DecideApply`)

## The mechanism

An item can be dragged out of a bag's panel onto the wound view: `PlayerCamera.TryPerformSpecialUIAction`'s
`WoundViewLimb` branch calls `ApplyWoundItem(this.dragItem)` (`PlayerCamera.cs:1656-1659`), and
`ApplyWoundItem` runs the item's own limb action — `item.Stats.useLimbAction(this.selectedLimb, item)`
(`PlayerCamera.cs:754`). That is the APPLIED family's entry (the same action a splint or tourniquet takes),
and the adapter reports it from `LimbItemTransform.OnItemAppliedToLimb` → `IItemControl.SendItemAppliedToLimb`
→ `WireCommandKind.ItemApplyLimb`.

Nothing in that path unloads the item from its container (unlike `Body.PickUpItem` and `Body.WearWearable`,
which both call `Container.UnloadItem` first), so there is no departure and no arrival report: the only
statement is the applied relocation, and its admission rule refuses it. `KernelCommandGateway.MayInstallOnOwnLimb`
requires the item's recorded location to be `Carried` — or already `Applied` on that same limb — and answers
`false` for anything else, so a row the kernel holds as `Contained` inside the member's own bag is IGNORED
(unanswered). The kernel keeps the contained row while the item is the limb's component.

The consequence is the re-pick ticket's: the bag's next container report does not name the child, and
`ItemDomainModule.DecideSyncContainer` takes every unnamed descendant to `Terminal (ReplacedBy)`. From then on
the component's own removal is refused too (`ItemRemoveLimb` on a terminal row), so the splint on the leg is
dead in the record.

## What the fix has to decide

The domain already accepts the move (`LimbItemLocationRules.DecideApply` refuses only terminal rows and checks
the revision, owner and limb), so the gap is the seam's eligibility rule, not a new kernel fact. The decision
is the one the take-out already states one family over: an item the record holds inside the member's OWN
container is one that member may install on its own limb — the home row decides, never the child's own
recorded owner (a bag that changed hands keeps its children under the previous holder; see
`done/member-report-bounds.md` and `CreationBeforeOperationTests`'
`AContainerSync_AfterTheContainerChangedHands_StillAcceptsTheRecordedContents`).

Two things to read before tightening or widening anything:

- Is installing a component out of a container the game will actually let a player do (the item's own
  `Stats.usableOnLimb` decides, and `ApplyWoundItem` has its own consciousness and `ActuallyUsableOnLimb`
  gates)? If the gesture cannot happen, the seam needs no relaxation and the ticket is closed by that reading.
- The same question for a WORLD container's child (a splint out of a ground crate): its row is a WORLD row
  carrying the container id, and the same rule currently refuses it. Decide both shapes together, or state
  why only one is reachable.

## Acceptance

A member with a splint inside a carried bag applies it through the wound view, then puts another item into
the bag. The item ends `Applied` on the limb on the host's record, the bag's next report leaves it alone (no
`[ContainerSync] … no longer names …` for it), and taking the component off afterwards relocates the same
instance id — read on the host's log and on a third client's clone.

## Limits

- Not driven on a real machine: the gesture needs the wound view open with a bag child dragged onto it, which
  the driver can only reach in-process (a `Body`/`PlayerCamera` call, or a recipe of `body-call`'s shape).
- Whether the native action even removes the item from its container parent is part of the first reading above:
  `useLimbAction` destroys the carried object and installs the component, so the local container's child list
  may shrink without any unload hook running — that is why this path has no departure to reuse.
