# Container moves reach the viewer as a snapshot, not an event

- Status: Todo — Rejected (batch `20261005-d`, row A1: the operator's and the third peer's clone-fact
  monitor still warn after a container move, although the owner's event now arrives and is applied)
- Priority: Medium
- Category: Item sync / call identity (the item-fact report carriers)
- Source: agent acceptance batch `20261005-c` (2026-10-05) — the monitor warned on every remote
  container move that run drove; raised to work by the user's 2026-10-06 ruling that a defect the
  repository's own history introduced is fixed or ticketed.
- Related: `review/remote-inventory-native-parity-rework` (the path that produced the warnings),
  `done/carried-inventory-registration-re-report.md`, `docs/architecture/remote-inventory-native-parity.md`
- Acceptance record: `docs/evidence/acceptance/container-move-snapshot-only-sync-20261005-d.md` (rejected)

## Symptom (evidence)

Batch `20261005-c` drove remote container moves through the native intent path
(`MoveIntoContainer`, `TakeOutOfContainer`) in both directions, in a three-client session. Every one of
them made the OPERATOR's clone fact table warn, once per move plus once for the container:

```text
[WRN] [CharSync] divergence for <owner>'s trashbag (id 1147077248963): nested container contents changed
      without an event sync (the 1 Hz snapshot carried it).
[WRN] [CharSync] divergence for <owner>'s dogfood (id 1155667183555): left the inventory without an event
      sync (the 1 Hz snapshot carried it).
```

The counts and the wordings are read from the batch's artifact directory (`acceptance-artifacts-dir`),
whose log excerpts are `r1-guest-log.txt`, `r1-host-log.txt` and `r1-alt-log.txt` — there is no `r4-*`
log. Operated by the guest: **8** warnings. Operated by the host: **7**. Third peer: **15**. Both
directions warned, and all four of the monitor's item wordings appear, not only the container one:

- `nested container contents changed without an event sync` — rows 3, 4 and 14, the trashbag;
- `left the inventory without an event sync` — the dogfood, waterbottle, lantern and soup each leaving
  the top-level carried list as it entered the bag;
- `slot 2 → 5 — a carried move without an event sync` — row 6, the host placing the guest's bandage in a
  ring slot;
- `a new carried item the fact table never saw — a pickup without an event sync` — row 2's insert, read
  on the host-operator side.

Neither client ever warned about its OWN items (the fact table is per remote owner), so the state always
converged; only the carrier differed.

## What the attribution found (2026-10-06)

The three questions this ticket opened, answered from the code rather than from the batch's reading:

1. **A container move is supposed to carry its own event, and the event is the fix.** The owner's
   client owns the carrier: `ContainerItemSync.OnLoadedIntoContainer` turns one native container load
   into one carried-fact event — `SendItemCarriedSync` on a host, `SendItemContainerContent` (the
   guest's report the host relays as the carried fact) otherwise — and its own doc comment states the
   rule as "a move INSIDE the carried inventory ... the parent container's FULL fact is one operation =
   one message". A remote-driven move runs the SAME native pair on the owner's real objects
   (`RemoteIntentApplier.ApplyMoveIntoContainer`: `container.UnloadItem(item, null)` then
   `container.LoadItem(item)`, plus the landed check on `item.transform.parent`), so the event its hook
   produces is a true local fact of the owner's, not an echo. The warning was right.
2. **The whole item-fact family was silent, not only containers.** `ContainerItemSync` (all three
   hooks), `PickupSync.OnPickedUp` and `ItemWorldSync` (instantiate/destroy/drop/throw) each start with
   `if (IsRemoteApply) return;`, so a remote-driven DropItem, TakeOutOfContainer, MoveIntoContainer,
   MoveContainerChildren, PickUpToSlot, UnloadBattery (the new battery) and GiveToTrader (the consumed
   item) reached the peers on the immediate character re-report alone. The batch saw the container
   wordings because container moves are what it drove; the sibling wordings (`a pickup without an event
   sync`, `left the inventory without an event sync` for a drop) are the same defect.
3. **The root cause is one overloaded call identity, not a missing mechanism.** `RemoteApply` means
   "a peer's fact is being replayed here, so this is not this client's action" — the character restore
   re-materializes items through the game's own slot path and reporting those made the host refuse them
   as `Conflict (item … is already carried)`
   (`SourceShapeGateTests.CharacterRestore_MaterializesItemsInsideARemoteApplyScope`), which is why the
   guards exist at all. `RemoteIntentApplier` used the SAME origin for the other situation — executing a
   peer's intent on this client's own items — so the carriers inherited a silence that was meant for
   replays.
4. **The row-6 warning is the pickup half of that same silence, not a slot-report gap.** The slot fact
   of a remote slot release travels through `PickupSync.OnPickedUp` → `_slotSync.OnItemRehomed`
   (`ApplyPickUpToSlot` replays `DropItem`/`PickUpItem`, never `Body.SwapSlots`), so `ItemSlotSync`, which
   has no call-context guard at all, is not on that path. The guarded pickup hook is, which is why row 6
   warned and why converting the pickup hook is what removes it. `ItemUseSync.OnItemUsed` likewise carries
   no guard, but the batch left row 7 `unproven`, so that half is read from the code and not from the run.

Ownership is unchanged from what this ticket recorded: the monitor is `CloneFactTable`'s
(`cddab4fa`), the container wording is `6503abfc`'s, the replay is `RemoteIntentApplier`'s, and the call
identity is `CallContext`'s — all this repository.

## What landed

`CallContext.Origin.RemoteIntentApply`, opened by `RemoteIntentApplier` INSIDE its existing
`RemoteApply` scope, plus `CallContext.IsReplayedRemoteFact` — `IsWithin(RemoteApply) &&
!IsWithin(RemoteIntentApply)`. The two scopes are both load-bearing: the presentation and echo guards
keep seeing a remote application (item sounds, world replays, every "this is not my action" guard),
while the carriers can now tell the two situations apart. The three item-fact carriers ask the new
query; a replay stays silent, and a peer's intent this client executed on its own items reports exactly
as a local gesture's would. Decision 234 records the rule.

Inside the intent scope the sites that actually fire are the container load/unload hooks, the pickup hook
and the drop hook; `OnItemInstantiated` (an `Item.Start` postfix) and `OnItemDestroyed` (an `OnDestroy`
postfix) run after the scope has closed and report either way, so converting them is the same rule stated
consistently rather than a behaviour change. So is the third container hook, `OnUnloadedAll`: the spill is
the container-broke path (`Container.UnloadAllItems`), which no intent reaches. One report carrier for an
intent kind is deliberately left on the bare query and named here rather than left implicit:
`CraftingPatches.BodyCombinePatch` suppresses the craft report for a remote-driven `CombineItems`, because
that fact already travels through the intent's own authoritative re-report and a second report channel for
one intent is what the rework's design forbids — and the divergence monitor does not compare the
ammo/charge state a combine moves, so no monitor symptom follows from it.

One consequence of the drop half is worth a session, and it is wider than the direction the batch ran: a
remote-driven `DropItem` now sends the drop report from the owner's own hook, and that report is the only
carrier either direction had. The peers materialize the item from it (`ItemApplication.OnRemoteItemDropped`),
and it is also what creates the host's world-table row (`ItemProjection.ApplyDrop` / `ApplySpawn`); the
keyframe's `RefreshWorldItemStates` early-returns for an id the table does not already hold, and the 10 Hz
follow stream only moves copies that already exist (`ItemPositionFollow` on a missing copy). The batch's row
5 read the OWNER's own pickup of the dropped item (`r5-host-pickup-soup.json`) with the host as the owner,
so a peer-side copy that never appeared was exactly what that row could not see.

## What remains

- **The acceptance run** (`docs/acceptance/`, a three-client batch): drive the row families this change
  touches — insert, take-out, slot release, drop, container expansion, battery load/unload — and read
  the operator's and the third peer's clone fact monitor as a ZERO-warning row over at least one full
  periodic cycle. Rows 3, 4 and 14 of `review/remote-inventory-native-parity-rework` are the ones this
  change's own evidence must be read beside, because their verdicts came from owner-side probes.
- **The drop row needs a PEER-side world read, in both owner directions.** A dropped item must be found
  in the world by a client that is neither the owner nor the operator (the third peer), for a host owner
  and for a guest owner. Row 5 of `20261005-c` read only the owner's own pickup of its own dropped item,
  which is exactly the half that stays true while a peer's copy never appears.
- **`PickUpToSlot`'s drop-then-pickup pair** is the one site whose new reports interact: the replayed
  slot release drops the item from its slot and picks it back up in one call bracket, which is the
  native shape a local drag-to-slot has (`PickupSync` cancels the pending drop and reports the move as
  a slot re-home). The run reads the operator's monitor over that gesture specifically.
- The monitor itself is deliberately unchanged: a snapshot that carries a change no event announced
  still warns (`CloneFactTableDivergenceMonitorTests`), which is what makes the run's zero-warning row
  mean something.

## Rejected by batch `20261005-d` (2026-10-05)

The three-client run judged this ticket's four prose rows. Three passed: the owner's event now reaches
the peers and is applied (both the operator and the third peer log `[CarriedSync] applied trashbag (id …)
to …'s snapshot — re-rendering the clone.`); a dropped item is found in the world by a third client in
BOTH owner directions (`item-world-read` returned the instance id the owner had dropped, host-owned and
guest-owned); and `PickUpToSlot` stayed silent on the operator's monitor — the one kind whose event
carries the item itself.

Row A1 failed: one millisecond after the `applied` line the same monitor still logs `nested container
contents changed without an event sync` for the container and `left the inventory without an event sync`
for the item that entered it, on the operator and on the third peer, for every container move the run
drove.

Where the run's reading points (not a fix): `CloneFactTable.WarnOnDivergence` compares the PREVIOUS
SNAPSHOT with the incoming one, and `ApplyCarriedSync`'s nested branch replaces the ROOT container's
contents without removing the moved child from the top-level list. A report that moves an item INTO a
container therefore still leaves two facts for the next snapshot to carry uncovered: the child's
departure from the top level and the container's new contents. The slot-release path carries the item
itself and writes its slot, which is why that gesture stayed silent — the contrast is in the record.

Evidence: `docs/evidence/acceptance/container-move-snapshot-only-sync-20261005-d.md`.

## Non-goals

- Not quieting or re-scoping the divergence monitor: it reported a real gap and stays loud.
- Not adding a wire member: every carrier this change lights up already existed and already had its
  event kind; the missing half was who was allowed to send it.
