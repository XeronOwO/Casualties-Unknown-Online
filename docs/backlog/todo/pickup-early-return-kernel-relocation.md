# A pending-drop pickup reports only a slot re-home, so a contained kernel record can survive

- Status: Todo
- Priority: Low-Medium
- Category: Item sync / kernel locations (the host's own carried-fact carriers)
- Source: the independent adversarial review of the container-move kernel-fact fix (2026-10-06), which
  proved the mechanism from the code and could NOT prove reachability — recorded rather than fixed on a
  guess, per the 2026-10-06 ruling that a defect the repository's own history introduced is fixed or
  ticketed.
- Related: `done/container-move-snapshot-only-sync.md` (the change that made the consequence reachable
  from more paths), `todo/remote-inventory-native-parity-rework.md` (the gesture family)
- Evidence: `docs/evidence/selfchecks/items/container-move-kernel-fact-selfcheck.md` §4

## The mechanism

`PickupSync.OnPickedUp` has an early return for the drag sequence the game itself produces (the same
input frame drops and re-picks the item): when the pending drop is cancelled (`_dropState.TryCancel`), the
hook reports ONLY the slot re-home — `_slotSync.OnItemRehomed(item)` — and returns, deliberately, because
the cancelled drop must not travel as a world drop.

That report reaches `ItemService.SendItemCarriedSync` with an item whose `Contents` are empty, which takes
the `TryUpdateState` branch: the kernel learns the item's DATA, never its LOCATION. An item the kernel
still held as `Contained` when the gesture ran keeps that record.

## Why it matters

A surviving `Contained` record stays invisible until the next container report for that parent arrives.
The report does not name the child, so `ItemDomainModule.DecideSyncContainer` takes it to
`Terminal (ReplacedBy)` — and Terminal is one-way: every later command that touches the item is refused
("terminal item … cannot update payload"), and a report that DOES name it rejects the WHOLE container sync
("terminal child … cannot re-enter container"), so that container stops syncing on both ends. Since
2026-10-06 that branch logs the ids it drops, which is how a session would show it.

## What is NOT proven

Reachability. Every path read so far reports the LEAVING child when a container empties, and its own
drop/pickup command relocates it in the kernel first (`SendItemDropped` → `TryDrop`, `SendItemPickedUp` →
`TryPickup`, both producing `ItemRelocatedEvent`); a container take-out additionally runs
`ContainerItemSync.OnUnloadedFromContainer`, which cancels the pending drop under its own trace label, so
the early return above may never see a contained item. No session has been run against this question, and
the tree carries no failing case.

## What a fix would look like

The branch needs a kernel-LOCATION fact that the host accepts, which is not the world-drop report the
early return exists to suppress (reporting that made the host refuse an unknown creation and roll the item
back out of the inventory). Separating "the item's place changed" from "the item entered the world" on
this branch is the design question; the alternative answer is to prove from the native call sites that a
contained item cannot reach the early return and pin that with a test.

First step for either answer: a session that drags items out of containers into slots and back, read
against the new `[ContainerSync] … no longer names …` warning.

## Readings

Batch `20261005-e` (2026-10-05) drove that first step on a three-client session and the warning did not
fire. The run moved a carried item into a container, took it back out onto a ring slot (the
`PickUpToSlot` shape whose replay unloads the child through `Body.PickUpItem`), took it out into the
world, and put it back, in both owner directions, then read the owner's, the operator's and the third
peer's logs from byte marks taken before the first gesture: no `[ContainerSync] … no longer names …` line
and no `Terminal` line on any of the three. The same session's traffic lines show the container report
travelling as the intended wire kind (`Send/Receive ItemContainerSyncCommand`). Reachability therefore
stays unproven on this gesture set; the next attempt should either widen it (a container emptied by a
gesture this run could not drive — the expansion kind is blocked for the driver, see
`done/container-move-snapshot-only-sync.md`) or answer the question from the native call sites, as the
section above suggests.
