# Container moves reach the viewer as a snapshot, not an event

- Status: Todo — batch `20261006-a` read the drop row after decision 236 in BOTH owner directions and the
  wearable half no batch had ever driven, both with the operator's and the third peer's monitor at ZERO
  over a full quiet cycle (record `docs/evidence/acceptance/container-move-snapshot-only-sync-20261006-a.md`);
  row A1g is all that is left, and its blocker CHANGED on 2026-10-06: a held key IS reachable in process,
  so the driver capability is now ordinary work instead of an impossibility — see the A1g bullet under
  "What remains"
- Priority: Medium
- Category: Item sync / call identity (the item-fact report carriers)
- Source: agent acceptance batch `20261005-c` (2026-10-05) — the monitor warned on every remote
  container move that run drove; raised to work by the user's 2026-10-06 ruling that a defect the
  repository's own history introduced is fixed or ticketed.
- Related: `review/remote-inventory-native-parity-rework` (the path that produced the warnings),
  `done/carried-inventory-registration-re-report.md`, `docs/architecture/remote-inventory-native-parity.md`
- Acceptance record: `docs/evidence/acceptance/container-move-snapshot-only-sync-20261006-a.md`
  (accepted except the blocked row); `…-20261005-e.md` (rejected) and `…-20261005-d.md` stand as history

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

## Read by batch `20261006-a` (2026-10-06)

The three-client run this ticket was waiting for. On the deployed `0.1.0+ad6f73ee…` the operator's and
the third peer's clone-fact monitor read **ZERO** across the whole gesture window and across a closing
quiet cycle: insert, take-out, slot release onto an empty owner slot, the guest-as-owner container
direction, a remote-driven `DropItem` in BOTH owner directions, and a remote-driven `DropWearable` in
both directions. The drop that rejected `20261005-e` is now the fix's own log line — `a drop report is
pending and announces it on the next frame, so no immediate re-report.` followed ~30 ms later by
`origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]` — while the container and slot kinds
keep their immediate re-report inside the same window, so the polarity is visible in one log. The third
peer's own world read returns every dropped id, both owner directions. Record:
`docs/evidence/acceptance/container-move-snapshot-only-sync-20261006-a.md`.

Rows 3, 4 and 14 of `review/remote-inventory-native-parity-rework` are still the ones this change's own
evidence must be read beside, because their verdicts came from owner-side probes.
- **The drop row needs a PEER-side world read, in both owner directions.** A dropped item must be found
  in the world by a client that is neither the owner nor the operator (the third peer), for a host owner
  and for a guest owner. Row 5 of `20261005-c` read only the owner's own pickup of its own dropped item,
  which is exactly the half that stays true while a peer's copy never appears. **Read by batch
  `20261005-e` and it passes**: the third peer's own world read returns the dropped id at the drop cell in
  both owner directions (`cm17-alt-world-after-drop.json`, `scrapmetal` beside the guest-owned
  `waterbottle`). What is left of this row is the monitor half, which that same batch read as a failure and
  which the ordering fix below answers.
- **The drop row's own reading after the ordering fix** (decision 236) — **read by `20261006-a` and it
  passes** in both owner directions: the operator's and the third peer's monitor at zero for a
  remote-driven `DropItem`, plus one full periodic cycle of quiet. Nothing in this fix can be read from
  the unit side alone — the failing pair was two log lines 30 ms apart on the owner's own client.
- **A remote-driven `DropWearable` reading, which no batch has driven yet** — **read by `20261006-a` and it
  passes in both owner directions**. The patch layer re-reported that kind unconditionally
  (`Patches/BodyPatches.cs`, found by this cycle's independent review and now guarded at the same entry
  point), so the row that proves the fix for `DropItem` said nothing about the wearable half. The fixture
  it needs is recorded with the record: `PlayerCamera.TryPerformWorldActions` calls `Body.DropWearable`
  only when `this.body.GetWearable(dragItem.id)` answers non-null, and `RemoteDragPredicatePatches`
  answers that query from the body the ring shows, so BOTH owners have to wear the same wearable type
  before the operator's release can produce the kind — staged through
  `tools/acceptance/recipes/item-wear.cs`.
- **The one-slot pending machine** (`todo/drop-pending-single-slot-overwrite.md`) is the defect this rule
  makes louder: when two drops land in one frame the first report is overwritten, and with the re-report held
  back the peers then get neither the event nor an immediate snapshot. It is filed with its evidence; a slot
  release onto an OCCUPIED destination slot is the gesture that reaches it.
- **`PickUpToSlot`'s drop-then-pickup pair** is the one site whose new reports interact: the replayed
  slot release drops the item from its slot and picks it back up in one call bracket, which is the
  native shape a local drag-to-slot has (`PickupSync` cancels the pending drop and reports the move as
  a slot re-home). The run reads the operator's monitor over that gesture specifically.
- **Row A1g (container expansion) is the ONE row still open.** The native branch needs
  `Input.GetKey(KeyBinds.GetBind("expanddesc"))` held. **Its blocker changed on 2026-10-06, after batch
  `20261006-a` closed**: a held key IS reachable in process — posting the client's own window a
  `WM_KEYDOWN` with the key's scan code makes `Input.GetKey(KeyCode.LeftShift)` read true on the next
  frame, and a `WM_KEYUP` with the scan code and the transition bits clears it, while `GetAsyncKeyState`
  stays 0 the whole time (no OS-level key state, no focus or foreground change, so the `input`
  dependency's rule is not touched — `AcceptanceDriverGateTests.TheAcceptanceToolsNeverUseOsLevelInput`
  still governs OS input and is not what this is). What is left is therefore ordinary work: build the hold
  into the committed driver (today it exists only as an ad-hoc probe, `.acceptance/…/probe-key-*.cs`) and
  judge the row in a three-client batch. No viewer in any batch has yet seen this kind, and it must not be
  guessed from the other five.
- The same batch tried the other route first and it did not land: the game's own radial-centre gesture as
  a way to dress a body in process. `mode=hover` does set `camera.radialOpen = true`, but
  `HandleWhileDragging` re-closes the ring on the game's own next frame when
  `|radialMenu.position.x − Input.mousePosition.x|` exceeds `600 × uiScale`, and the run may not move the
  OS pointer. That is not a claim that the gesture is undrivable — the committed
  `mode=release cast=-2` forces the ring's scale inside the same call, which is how batch `20261005-d`
  drove its radial row — but it is not this kind's route either way, because this kind needs a KEY held.
- **The battery half was not re-staged by `20261006-a`**: `A1f` (battery unload) was read `pass` by
  `20261005-e` and decision 236 does not touch the battery branch, but this run's fixture set carried no
  installed-battery receiver, so no battery row is judged here.
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

## Fixed by the kernel-side report (2026-10-06)

The rejected batch read the failure site correctly and the fix follows that reading exactly. The peers
never apply the owner's message: the committed batch is projected, and a container's contents are listed
from the KERNEL's contained children (`KernelBatchItemProjection.BuildContents`), so a report committed
as a bare state update reached them as an EMPTY container while the moved child kept its previous kernel
location — precisely the pair the monitor printed, one millisecond after `applied`.

One container report is now ONE command on both sides: `ItemKernelAuthority.TrySyncContainerFacts` builds
the `SyncContainerItemsCommand` (the parent's fact plus the flattened child facts, one atomic batch) that
the guest's `ItemContainerSync` wire kind already mapped to, and `ItemService.SendItemCarriedSync`
commits a contents-carrying report through it. The host's own container move — a local drag or a peer's
intent replayed on its items — therefore writes the child's place, not only the parent's data.

The job had two implementations, and the wrong one is gone rather than fixed twice:
`ItemContainerSyncWriter`/`SyncContainerContents` was dead in production and relocation-blind (it updated
a child only when the kernel already had it under that parent), so it is deleted and its two tests now pin
the command path. The two classes the change touched were also split along the seams
`docs/backlog/watchlist/architecture-watchlist.md` had already named — `ItemKernelProjectionWiring` (157)
out of `ItemService` (599 → 536) and `KernelDomainCommands` (195) out of `ItemKernelAuthority`
(577 → 449; the fix's own kernel write had first carried it to 616, which is what the 600-line gate
refused) — so neither carries the next item-domain change over the gate. All call expressions of the 14
moved command entry points are unchanged; twelve test files needed one namespace import for the extension
surface.

One consequence of committing the report as the wire path's command is recorded rather than left implicit:
the host's own pickup/slot/use carriers now reach `DecideSyncContainer`, whose stale-children branch takes
a child the report does not name to `Terminal` — a one-way door that rejects the whole container sync from
then on. That verdict is right for a container that really is empty (the paths that empty one report the
LEAVING child, whose own drop/pickup command relocates it in the kernel first), and wrong for a child that
moved without its own relocation fact. One path could not be excluded by reading: `PickupSync.OnPickedUp`'s
pending-drop early return reports only a slot re-home (`_slotSync.OnItemRehomed`) and would leave a
contained record behind if that gesture ever sees a contained item — its own ticket records the mechanism.
The branch is no longer silent: `TrySyncContainerFacts` warns with the dropped ids when the command it
just committed makes children Terminal, and `ItemContainerSyncTests` pins both the warning and its
absence.

Proof in the tree: `ContainerSyncProtocolTests.HostCarriedContainerReport_ProjectsTheMovedChildInsideTheContainer`
was RED before the change (`Expected: Contained, Actual: Carried` for the moved child) and now reads the
host's own report through the committed batch into the guest's carried fact, which contains the child.
Behavioural suite 4621/4621, gate project 346/346, `dotnet format` clean, no wire member added.

What row A1 still needs is its own reading: the operator's and the third peer's monitor at ZERO over at
least one full periodic cycle, on a three-client batch against the deployed artifact, over the gesture set
the rejected batch drove (insert, take-out, slot release, drop, container expansion, battery load/unload)
— with the guest-owner container move included, since the wire path and the host-local path are now the
same command and both are claimed silent.

## Rejected by batch `20261005-e` (2026-10-05)

The fix's own claim holds for every container kind it touched. On the deployed `0.1.0+cdd93044…` the
three-client run read the operator's and the third peer's clone-fact monitor at zero through insert,
take-out, slot release and battery unload, host-owner and guest-owner direction alike: the owner's
committed report carries the moved child (`[ContainerLoad] dogfood (id …) moved inside body container
trashbag — root content event up to trashbag`), and the next snapshots show no divergence. Batch
`20261005-d`'s pair of warnings — one per container move, one millisecond after `applied` — did not
reproduce once in this run.

**Row A1 still fails, on the drop kind, and the failure is an ordering race inside the owner's own
client.** A remote-driven `DropItem` ends the apply in `RemoteIntentApplier` with
`domains.CharacterDataSync.ReportInventoryChanged(body)` ("The owner's own scene changed: the immediate
re-report makes every clone … converge now"), which sends a character snapshot that no longer carries
the item; the drop's own item report is committed later by the pending-drop flush (~30 ms in this run).
The peers apply the snapshot first, see an item vanish while its event is still in flight, and warn —
the wording `left the inventory without an event sync`, on the operator and on the third peer, in both
owner directions (`scrapmetal` at 22:50:37, `waterbottle` at 22:53:21).

Two controls in the same run attribute it to that re-report rather than to the drop carrier: a **local**
drop of the owner's own item produces no immediate re-report and no warning at all, and a world-driven
burst that dropped three of the host's items inside 2 ms warned only about the two whose reports were
still pending — the one already flushed at 22:56:12.448 was silent. So the carriers this ticket fixed are
not the remaining gap; the re-report `RemoteIntentApplier` sends for a `DropItem` intent is, because it
reaches the peers ahead of the drop report it announces. (The peers do materialize the dropped item from
that report: `[ItemDrop] scrapmetal … not present — requesting materialization at (1.1,483.2)`, and the
third peer's own world read returns the id at the drop cell.)

The one kind this run could not put in front of the monitor is container expansion
(`MoveContainerChildren`): the native branch that produces it needs
`Input.GetKey(KeyBinds.GetBind("expanddesc"))`, which reads `LeftShift` and `false` on the live client,
and this run's driver is in-process only — by its own contract it never synthesises OS-level key state.
That row is `blocked` on that capability, named rather than guessed.

Evidence: `docs/evidence/acceptance/container-move-snapshot-only-sync-20261005-e.md`.

## Fixed by the drop report order (2026-10-05)

The failure the batch read at its site is an ordering race inside the owner's own client, and the fix is that
order rather than a new carrier. `RemoteIntentApplier` ended every applied discrete intent with
`domains.CharacterDataSync.ReportInventoryChanged(body)`; for a `DropItem` that snapshot already omitted the
item while the drop's own report was still parked in `ItemDropState` — the carrier waits one frame on purpose
so the game's `DropItem` → `ThrowItem` pair can set the final velocity (`DropPendingState.TryFlush` refuses a
same-frame flush, because a zero-velocity report materialized a ghost). The peers applied the snapshot first
and warned, exactly as `CloneFactTable.WarnOnDivergence` documents ("A change whose event is still in flight
trips the warning too").

The rule is now asked of the item domain's PENDING STATE at BOTH places an owner can send an inventory
snapshot: `ItemWorldSync.HasPendingDropReport` (`_dropState.Current == ItemDropState.Phase.Dropped`) is the
fact that a drop report is registered and not yet sent, and while it holds, neither `RemoteIntentApplier` nor
`GameAdapterBridge.OnInventoryChanged` — the patch layer's ONE entry point — re-reports. Asking the state
rather than the kind also covers a kind whose native call left a drop behind on a path that did not land (R9's
slot release drops the two slot occupants before its own pickup may refuse), and it was that patch-layer entry
point which made a kind-shaped rule inert for `DropWearable`: an unconditional `Body.DropWearable` postfix
(`Patches/BodyPatches.cs`) re-reported that drop from inside the applier's own native call — found by this
cycle's independent adversarial review, which is also why the two drop branches now read their native call back
(`body.HoldingItem(item)` / `body.GetWearable(item.id) != null`) and log a refusal like every other kind. The
drop report IS the announcement, and it is what removes the clone entry on the peers (`ItemDropped` →
`CloneFactTable.RemoveCarriedItem`), so one operation stays one message. Every other discrete kind keeps the
re-report, because its carrier (`ContainerItemSync`, `PickupSync`, `ItemSlotSync`, `ItemUseSync`) sends inside
the apply scope and the snapshot then only speeds convergence; the two kinds whose carrier is deliberately
suppressed keep it as well and must not be "aligned" away — a remote-driven `CombineItems` (decision 234) and
`GiveToTrader`.

Both rejected alternatives are recorded rather than left implicit: forcing the flush inside the apply would
report the drop without its throw velocity (the ghost the pending state exists to prevent), and deferring the
re-report behind the flush would put two messages on one fact.

Proof in the tree: `RemoteIntentReportOrderGateTests` was RED on the pre-fix tree ("never asks
`HasPendingDropReport`") and now pins BOTH entry points' guards, their polarity and the query's own state; the
deferral it leans on is pinned by `DropPendingStateTests` (`TryFlush_SameFrame_Rejected` /
`TryFlush_NextFrame_AliveStandalone_Consumed`). No wire member is added and no protocol number moves. Full self
check: `docs/evidence/selfchecks/items/remote-intent-drop-report-order-selfcheck.md`; decision 236 carries the
rule.

## Non-goals

- Not quieting or re-scoping the divergence monitor: it reported a real gap and stays loud.
- Not adding a wire member: every carrier this change lights up already existed and already had its
  event kind; the missing half was who was allowed to send it.
