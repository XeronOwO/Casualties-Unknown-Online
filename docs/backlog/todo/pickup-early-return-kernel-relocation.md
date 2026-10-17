# A pending-drop pickup reports only a slot re-home, so a contained kernel record can survive

- Status: Todo
- Priority: Low-Medium
- Category: Item sync / kernel locations (the host's own carried-fact carriers)
- Source: the independent adversarial review of the container-move kernel-fact fix (2026-10-06), which
  proved the mechanism from the code and could NOT prove reachability — recorded rather than fixed on a
  guess, per the 2026-10-06 ruling that a defect the repository's own history introduced is fixed or
  ticketed.
- Related: `done/container-move-snapshot-only-sync.md` (the change that made the consequence reachable
  from more paths), `done/remote-inventory-native-parity-rework.md` (the gesture family)
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
## Read in the code: the location or data decision, and the one fix this shares (2026-10-18)

The mechanism is one literal in `ItemService.SendItemCarriedSync`:

```
if (item.Contents.Count > 0) { _kernelAuthority.TrySyncContainerFacts(ownerSteamId, item, out _, out _); }
else { … FindItem(…) → TrySpawnCarried(…) or TryUpdateState(…) }
```

**A report carries the item's PLACE only when the item has contents of its own.** A leaf item being re-homed out
of a container into a slot has none, so its report takes `TryUpdateState` — the kernel learns the data and keeps
the location it already had, which is `Contained` under the old parent. That is this ticket, exactly.

The kernel offers only two location-taking entries (`TrySpawnCarried` for a top-level arrival, and
`TrySyncContainerFacts` for a parent together with its contents), so there is no "move this one item out" call to
make. The move has to be stated by the PARENT.

**Which makes this ticket and `container-content-event-gap-on-repick.md` the same defect seen from two sides.**
That ticket is the peers' view (the container's fact never leaves this client, so their fact table learns it from
the 1 Hz snapshot and warns); this one is the kernel's view (the old parent keeps the child). One report fixes
both: when an item is re-homed out of a container into a slot, the owner sends the OLD PARENT's updated contents —
the parent no longer holds the child, which states the departure and relocates the child in the kernel — instead
of (or in addition to) the data-only carried report.

Two things to settle when it is implemented, both already named by the sibling ticket: the report must be
idempotent (the snapshot carries the same fact), and the take-out-into-the-world path must keep reporting its own
departure rather than gaining a second one. And the two tickets should be done in ONE change, since doing either
alone leaves the other's symptom standing while adding a second carrier for the same fact — which is the failure
mode this repository calls "a fact with two owners".