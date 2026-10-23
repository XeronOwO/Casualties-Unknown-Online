# A limb component installed straight out of a bag leaves a contained kernel record behind

- Status: Review — the seam rule fixed 2026-10-24 (a contained child whose container chain is rooted in the
  sender's own carried inventory is admitted; the owner-field sibling in `MayReportOn` and the handler's home
  check were aligned in the same cycle); the machine row below is owed, and the WORLD-item half is a decision
  point (see below).
- Priority: Low-Medium
- Category: Item sync / kernel locations (the applied family's eligibility rule)
- Source: found while fixing `done/pickup-early-return-kernel-relocation.md` (2026-10-20), which took the
  radial-wear half of the same cancelled departure and left this one — the WOUND-VIEW apply, a different
  native entry and a different kernel destination.
- Related: `done/pickup-early-return-kernel-relocation.md` (the carried destinations: re-pick and radial
  wear, both accepted by batch `20261025-b`), `done/container-move-snapshot-only-sync.md`
- Evidence: `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:739-763` and `:1656-1659`,
  `src/CasualtiesUnknownOnline.Application/Kernel/KernelCommandGateway.cs` (`MayInstallOnOwnLimb`,
  `MayReportOn`), `src/CasualtiesUnknownOnline.GameState/Domains/Items/LimbItemLocationRules.cs`
  (`DecideApply`), `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemLocationChain.cs` (`HolderOf`)

## The mechanism, with the input sequence corrected

The wound view applies an item through `PlayerCamera.TryPerformSpecialUIAction`'s `WoundViewLimb` branch →
`ApplyWoundItem(this.dragItem)` (`PlayerCamera.cs:1656-1659`) → `item.Stats.useLimbAction(this.selectedLimb,
item)` (`:754`); the adapter reports it from `LimbItemTransform.OnItemAppliedToLimb` →
`IItemControl.SendItemAppliedToLimb` → `WireCommandKind.ItemApplyLimb`.

**A player cannot drag an item straight out of a bag's panel onto the wound view.** The bag panel and the
wound view are mutually exclusive in `PlayerCamera`: `OpenContainer` has one caller, `HandleReleaseDragging`,
which sets `radialOpen = true` and calls `ToggleWoundView(true)` to close the wound view when it is open;
`ToggleWoundView` refuses to open while `radialOpen` and drops it; and `HandleRadialMenu` calls
`CloseContainer` when the radial closes. The only overlap is the ~0.2 s lerp window between two key presses —
not a player entry. The review that found this (2026-10-24) is recorded below.

The player-reachable shape is a **lagging kernel row**: the item is in a hand or a slot — the take-out or slot
arrival was refused or lost — while the record still says `Contained` (the residue the sibling ticket
records). The wound view then applies it. Nothing in the native path unloads the item from its container
(unlike `Body.PickUpItem`/`Body.WearWearable`, which call `Container.UnloadItem` first), because
`useLimbAction` destroys the carried object; so the only statement is the applied relocation, and the old
seam refused it: `MayInstallOnOwnLimb` required `Carried` (or `Applied` on the same limb) and answered
`false` for `Contained`, IGNORING the report. The kernel kept the contained row while the item became the
limb's component; the bag's next report then took the unnamed descendant to `Terminal (ReplacedBy)`, and the
later take-off found no `Applied` row at all (it logs the "two-carriers" divergence and mints a new id) —
the removal is never reported, not merely refused.

## The fix's rule

The domain already accepts the move (`LimbItemLocationRules.DecideApply` refuses only terminal rows and
checks the revision, owner and limb), so the gap was the seam. The rule is the one the take-out already
states one family over: an item the record holds inside the member's OWN container is one that member may
install on its own limb — **the home row decides, never the child's own recorded owner** (a container that
changed hands keeps its children stamped with the previous holder). `ItemLocationChain.HolderOf` is that
fact: it walks the parent links and returns the actor whose carried inventory roots the chain, 0 for a
world-rooted chain, an applied/terminal root, an unknown parent or a cycle (all fail closed).

## What landed (2026-10-24) — the chain, and the family

- **The seam arm.** `KernelCommandGateway.MayInstallOnOwnLimb` gained
  `ItemLocationKind.Contained => ItemLocationChain.HolderOf(current, _items.FindItem) == sender`, so a
  contained child whose chain is rooted in the sender's own carried inventory is admitted and relocated by
  `DecideApply`; another member's bag, an applied/terminal root, a missing parent, a cycle and a world-rooted
  chain are refused. The refusal line now names both admitted shapes: `item N is not carried by the sender
  (nor inside a container the sender holds), or the report installs it on somebody else's limb L`.
- **The same owner-field defect, one rule over.** The review found `MayReportOn`'s `Contained` arm still
  read `row.Location.Owner` (and the handler's container-sync home check did too), so the changed-hands
  child had the same defect in the state/destroy family: the new holder's report was ignored and the
  previous holder's was admitted. Both now read `HolderOf` (world-rooted content stays reportable by any
  peer, as before). This is the sibling the checklist's whole-family audit had wrongly certified.
- **Tests.** `KernelCommandGatewayTests` gained the own-bag admit case (RED on the old seam,
  `.acceptance/20261024-c/limb-red.log`), the another-member's-bag negative, a real WORLD-row negative
  (`WorldChildOf`, not the unproduced Contained-under-World shape) and a nested-chain negative that a
  one-hop parent read would pass; `KernelItemReportEligibilityTests` gained the changed-hands pair (the new
  holder admitted, the stale owner ignored) and seeded the parent rows its two contained cases had left
  missing; `ContainerTakeOutTests` gained the kernel-level row: a contained child applied to a limb becomes
  `Applied` and the bag's next `ContainerSync` leaves it `Applied`.
- **The WORLD half is a decision point, not a settled line.** A world crate's child is a `World` row (not
  `Contained`), so the switch refuses it by kind — and so does a ground item applied straight to the wound
  view. That gesture is reachable: `ApplyWoundItem` has no parent check, `SendItemAppliedToLimb` is
  unconditional, and the host's identical gesture is relocated because the host bypasses the gateway
  (`ItemProjection.ApplyLimbItem` → `ItemKernelAuthority.TryApplyLimb`), while the peers' projection already
  handles World→Applied (`KernelBatchItemProjection.ApplyRelocatedToProjection` removes the world row). A
  member's report is ignored SILENTLY, so the applier keeps a component the host's record does not know. That
  is player-visible and goes to the user: admit the world shape (the kernel already supports the end state),
  answer the refusal so the applier settles, or decide the wound view is not usable on ground items. Recorded
  as the decision point below; no code widens it here.

## Acceptance (re-scoped to the reachable shape)

A member whose record still holds a bag child as `Contained` while the item is in its hands applies it
through the wound view; the host's row ends `Applied` on that limb, and the bag's next report leaves it
`Applied`. The discriminating reads are the HOST's kernel row (and the peers' clone after the relocation
batch) and the later take-off relocating the same instance id — NOT a `[ContainerSync] … no longer names …`
line, which only the host's own carried-sync path writes (`ItemKernelAuthority.TrySyncContainerFacts`), so a
member's report would never produce it.

## Limits

- **Not driven on a real machine.** The row above is owed. It needs the lagging-record shape staged on a
  client (a lost/refused take-out arrival) plus the wound-view apply; the driver's UI cannot drive the native
  wound view, and the earlier "reflect-set `dragItem` and invoke the apply" plan is NOT the row's driver — a
  seam a probe can drive is not evidence a player can reach it. The ticket's input-sequence reading above is
  the design; the machine half is the staging.
- **The world-item divergence is open** (decision point below), and it is the one place where a reachable
  gesture leaves the applier and the host disagreeing while nobody is answered.
- **The handler's home check is aligned but has no dedicated nested-handler case.** The change reads the
  chain where the handler refused by the home row's owner; the existing container-sync suites stay green, but
  the nested changed-hands refusal is covered at the seam, not end-to-end.
- The child's recorded owner is still kept as-is (the kernel never re-stamps it); every rule that needs the
  holder now reads the chain. A rule that still reads the field is a defect of that rule.

## Decision point (2026-10-24) — waiting on the user

What should a member's wound-view apply of a WORLD item (a ground item, or a world crate's child) do?

1. **Admit it** (recommended if the wound view really accepts ground items): the host's own path already
   relocates World→Applied, the kernel supports the end state, and the peers' projection removes the world
   row. The gate would admit `World` rows whose parent chain is world-rooted — which is any world item, so the
   authority question is only "the sender names it and runs the native action" (the same trust every apply
   report has).
2. **Refuse and ANSWER it**, so the applier's client settles instead of keeping a component the host does not
   record (today it is ignored silently).
3. **Decide the wound view is not usable on ground items** in this build, and close the question by that
   reading (the decompiled path has no parent check, so this needs the game's own UI gating as evidence).

Recommendation: option 1 if the gesture is real, else 2 — never the silent divergence. The choice is
user-visible (what the player sees on the limb) and is not settled here.
