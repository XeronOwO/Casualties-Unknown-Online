# A second drop report at the same position loses its world object

- Status: Todo
- Priority: Medium
- Category: Item sync / world materialization (the drop report's adopt path)
- Source: acceptance batch `20261006-h` (2026-10-06), rows 3 and 4 of
  `todo/drop-pending-single-slot-overwrite.md` — the run that finally produced two same-frame departures
  read this on both of them. Filed by the repository's own rule: a batch run does not change code, and a
  finding a run turns up is filed with its evidence.
- Related: `todo/drop-pending-single-slot-overwrite.md` (its producer: R5's container expansion refusing
  every admitted child, which is what makes two drop reports share one position in one frame),
  `review/runtime-entity-markerless-bind-absorption.md` (the runtime-ENTITY sibling of this pattern — a
  record bound to a markerless same-prefab copy by position; this ticket is the item-domain instance),
  `done/container-move-snapshot-only-sync.md` (the drop carrier and the position it reports)

## Problem (evidence)

Batch `20261006-h` drove the gesture `todo/drop-pending-single-slot-overwrite.md` asks for — the owner
carries a `duffelbag` holding two `dogfood`s, a `plasticbag` target sits inside the owner's carried
`trashbag`, and the operator releases the source bag's proxy onto the nested target's panel button with the
game's own `expanddesc` bind held. The owner's own client is correct: both children's departures register in
one frame and both commit, and both of its own objects stay in the world (`[ItemDrop] … present — re-placing
at (3.1,490.3)` for both, `Destroy=0` in its own `[ItemTraffic]` lines).

The two VIEWERS lose the second child. On the operator and on the third peer alike, in two independent runs
(the first fixture, then fresh light items):

```text
[ItemDrop] dogfood (id 43852695189) not present — requesting materialization at (3.1,490.3), container 0, parentPos (0.0,0.0).
[ItemSpawn] materializing dogfood (id 43852695189) at (3.1,490.3), vel (0.0,0.0), container 0.
[ItemDrop] dogfood (id 39557727893) not present — requesting materialization at (3.1,490.3), container 0, parentPos (0.0,0.0).
[ItemBind] bound existing dogfood at (3.1, 490.3) to id 39557727893 (no materialization).
[ItemTrace] op=54 item=39557727893 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
Kernel command rejected by host for item 39557727893: InvalidTransition.
```

and the host's authoritative read afterwards (`w2-host-tables-after-repeat.json`) holds the first child in the
world table and the second in `terminal` (`worldCount: 242`, `terminalCount: 2` — the other terminal entry is
the first run's second child, `26672826005`). So a report that was SENT and COMMITTED leaves its item with no
standing object on either peer, and the guest's own `ItemDestroy` command for it is refused as
`InvalidTransition (terminal item … cannot be destroyed again)`.

## What the run established, and what it did not

- **Established.** The drop reports themselves are correct and both commit on the owner. The failure is on
  the receiving side, in the report's materialization step: the second report found no local object, asked
  for materialization, and instead ADOPTED an id-less same-prefab world copy within
  `RemoteItemSceneOps.FindExistingAt`'s tolerance (`AdoptTolerance = 1.5f`, which by its own filter excludes
  every object already carrying `ItemInstanceId`, excludes remote clone proxies, and requires
  `ItemWorldSync.IsWorldItem`). That adopted copy was destroyed ~10–30 ms later with its id still set
  (`origin=OnItemDestroyed` names the id), which is NOT the shape of the adapter's own remote kill
  (`KillRemoteItem` zeroes the ids of the whole subtree BEFORE destroying, precisely so a remote deletion
  does not echo back as a local destroy report).
- **Established, and it is why the two children differ.** The first report of the pair materialized a NEW
  object and the second adopted an existing one, 8 ms apart, at the same reported position. The adopt
  target therefore appeared between the two reports, at a position where nothing id-less was found for the
  first.
- **Not established — this ticket's first job.** Which code path destroyed the adopted copy, and where the
  id-less copy it adopted came from. Candidates the evidence does not separate yet, all with their pointers:
  the materialization bookkeeping that remembers the frame an object was materialized in
  (`RemoteItemSceneOps._materializedFrame`) and the deferred destroy next to it; the state application the
  adopt path runs over a pre-existing object (`BindExistingItem` → `ApplyAuthoritativeState`, which restores
  condition and contents, where the adapter's fresh-object path stamps an id instead — a condition the game
  destroys at zero would behave differently on the two paths); and CUO's own item-destroy application for
  the same id arriving after the adopt. The fix cycle attributes it before changing anything.
- **Why it matters beyond this row.** Any drop report whose position coincides with an id-less same-prefab
  world copy can take this path. The same-frame pair is what makes it DETERMINISTIC here (every R5
  expansion unloads all of its children out of one source container, so they all report the same position),
  not what makes it possible.

## Goal

A drop report that names an item must end with that item standing as its own world object on every
receiver, or with an explicit, reported refusal — never with the reported item destroyed and no object.

## Acceptance

- **Unit / pinned behaviour.** The adopt path's tie-break is pinned: an id-less same-prefab world copy at
  the reported position is either adopted and SURVIVES, or is refused and the row materializes beside it.
  Whatever the fix chooses, the choice is a named rule with a regression case, and a mutation shows the case
  fails without it.
- **Runtime (three clients).** Re-run batch `20261006-h`'s fixture (`docs/evidence/acceptance/
  drop-pending-single-slot-overwrite-20261006-h.md`): one gesture, two same-frame children, and BOTH ids in
  the host's world table, BOTH materialized on the third peer (an `[ItemSpawn]` or an explicit bind line
  each), no `terminal` entry for either, and no `InvalidTransition` for either — while the owner's own
  reading stays as this batch recorded it (two departures registered in one frame, both committed).
- **The producer's own row** (`todo/drop-pending-single-slot-overwrite.md`) is judged against this fix: its
  world half was the row that failed here, and its machine half already passes 2/2.
