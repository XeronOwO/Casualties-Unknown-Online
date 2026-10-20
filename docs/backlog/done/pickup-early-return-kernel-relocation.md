# A pending-drop pickup reports only a slot re-home, so a contained kernel record can survive

- Status: Done (batch `20261025-b`, 2026-10-20: the member took a bag child onto a ring slot, then loaded
  another item into the bag — the bag's report did not charge the child, the host's kernel record stayed
  `Carried`, and a later slot move of it was accepted; the radial wear of a bag child landed on the limb and
  left the same clean record. Rows 1–4 pass; record
  `docs/evidence/acceptance/pickup-early-return-kernel-relocation-20261025-b.md`).
- Priority: Low-Medium
- Category: Item sync / kernel locations (the host's own carried-fact carriers)
- Source: the independent adversarial review of the container-move kernel-fact fix (2026-10-06), which
  proved the mechanism from the code and could NOT prove reachability — recorded rather than fixed on a
  guess, per the 2026-10-06 ruling that a defect the repository's own history introduced is fixed or
  ticketed.
- Related: `done/container-move-snapshot-only-sync.md` (the change that made the consequence reachable from
  more paths), `done/remote-inventory-native-parity-rework.md` (the gesture family),
  `done/member-report-bounds.md` (the container-changed-hands rule the eligibility test below follows),
  `done/container-content-event-gap-on-repick.md` (the same gesture read on the PEERS' fact table — a
  different carrier, closed by batch `20261025-b`), `todo/container-child-limb-install-keeps-a-contained-record.md` (the
  applied family's sibling, found while fixing this one)
- Evidence: `docs/evidence/selfchecks/items/container-take-out-selfcheck.md` (this cycle) and
  `docs/evidence/selfchecks/items/container-move-kernel-fact-selfcheck.md` (the review that filed this
  ticket — its "the destroy branch is newly reachable" note is the reading this change closes)

## The mechanism

`PickupSync.OnPickedUp` has an early return for the gesture the game itself produces: an item is taken out
of a container, and `Body.PickUpItem` unloads it first (`item.TryGetParentContainer(out container)` →
`container.UnloadItem(item, null)`, `Body.cs:1395`) before it parents the item into a slot. The unload hook
opens a pending departure, the re-pick cancels it (`_dropState.TryCancel`), and the hook reports ONLY the
slot re-home — `_slotSync.OnItemRehomed(item)` — deliberately, because the cancelled departure must not
travel as a world drop (reporting it made the host refuse the unknown id and roll the item back out of the
inventory).

That slot report reaches `ItemService.SendItemSlot` → `WireCommandKind.ItemUpdateState`, which the kernel
applies as DATA (`UpdateItemStateCommand` → `ItemDataUpdatedEvent`, keeping the row's location). A
container's children are the CONTAINER's kernel rows (`ItemDomainModule.DecideSyncContainer` creates them
as `ItemLocation.Contained(command.Actor, child.ParentItemId)`), so an item taken out of a bag keeps its
`Contained` row after it is in the owner's slot.

## Why it matters

A surviving `Contained` record stays invisible until the next container report for that parent arrives.
The report does not name the child, and `DecideSyncContainer` takes every descendant it does not name to
`Terminal (ReplacedBy)` — and Terminal is one-way: every later command that touches the item is refused
("terminal item … cannot update payload"), the refusal reaches the adapter as an item reject, and the
pickup/block-drop rollback unparents or destroys the only copy. So one ordinary drag (bag → slot, then
anything else into that bag) is enough to kill the item. `ItemKernelAuthority.TrySyncContainerFacts` logs
the ids of that branch, which is how a session shows it.

## Reachability, proven from a batch's own trace

Batch `20261006-f` (three clients, one lobby) drove a focused owner's container child onto a ring slot
through the remote-inventory intent, and the OWNER's log carries the bracket — the record
`docs/evidence/acceptance/drop-pending-single-slot-overwrite-20261006-f.md` holds the trace lines
`[ItemTrace] op=46 begin item=18082891413 origin=OnItemUnloadedFromContainer event=Unload` and
`[ItemTrace] op=46 … result=Cancelled events=[RePick]`; the full
`[ContainerUnload] dogfood … left its container into the world from the CarriedInventory side — the drop
report waits one frame` line is quoted in the sibling ticket (and other batches' records), not in the f
record itself. So the departure is opened by the container unload and killed by the re-pick: the branch the
ticket was filed about runs on every bag → slot release, for the owner's own drag and for a replayed intent
alike (the replay runs the same native `Body.PickUpItem` on its owner's client).

The same cancelled departure is opened by the RADIAL WEAR (`Body.WearWearable` unloads the container child
first too, `Body.cs:1500-1508`), so the second gesture that releases an item out of a bag needed the same
report — it is included in this change (`ItemSlotSync.ReportCarried`) rather than left to a follow-up.

## The fix: an arrival report of its own kind, chosen by the home the item left

The arrival is the fact the releasing client can state, and it is a RELOCATION: the item is now in its
holder's carried inventory. The kernel already has that entry (`PickUpItemCommand`); what was missing is a
report that says it for these gestures, and a verdict that settles correctly when the host refuses.

`ContainerArrivalPolicy` (new, Runtime, pure) reads the cancelled departure's `Source` and names the report:
a container the body holds → `WireCommandKind.ItemContainerTakeOut` (31) → `PickUpItemCommand`; a container
in the world → the plain `ItemPickup`; a body slot or limb → nothing (the item was already carried in the
record, and a second relocation would be a fact with two owners).

- **Why the two container homes differ**: they settle differently when the host refuses. A world item taken
  out of a ground crate is an ordinary pickup — a refusal must roll the local copy back into the world. An
  item taken out of a bag is IN the member's hands, so a refusal must never unparent it; the take-out's way
  back is the carried-set statement.
- **The sender**: `PickupSync.OnPickedUp`'s cancelled-departure branch (the re-pick) and
  `ItemSlotSync.ReportCarried` (the radial wear) both ask the policy and send the report BEFORE the slot /
  carried fact, so the record is already the owner's row when the data lands. Both call sites are pinned by
  a source-shape test (`ContainerArrivalPolicyTests.TheTwoDepartureSites_SendThePolicysReport`) because the
  adapter has no harness.
- **The eligibility rule**: the pickup family's kinds are one kernel command, so both share
  `KernelCommandGateway.MayTakeIntoOwnHands` — world content, the sender's own carried/installed item, the
  contents of a container the sender HOLDS, an id this host never judged, or a terminal row the domain
  answers. The CONTAINED arm reads the row the child SITS IN (`ItemLocationChain.HolderOf`), never the
  child's own recorded owner: the kernel stamps a child's owner at creation and does not re-stamp it when
  the container changes hands, so a bag taken from another member still records its children under the
  previous holder — reading the child's owner refused the receiver's own bag, which is the same
  terminalization one path over. `done/member-report-bounds.md` and
  `CreationBeforeOperationTests.AContainerSync_AfterTheContainerChangedHands_StillAcceptsTheRecordedContents`
  state the same rule for the container report.
- **The host**: an id this host never judged is answered `UnclaimedItem` with no accept-first heal (a
  relocation has nothing to move and never creates its target), before the creation-before-operation
  invariant's generic protocol-violation answer — the carried-set statement is a cadence, so an item that
  entered a container since the last one legitimately has no judged creation yet. A refused creation's
  tombstone still answers first.
- **The settlement**: `ItemCommandRejectionRouter` settles a refused take-out by re-stating the carried set
  and never by the pickup rollback — except `InvalidTransition`, where the record contradicts the item's
  existence (terminal, or installed on a limb) and no statement can repair it, so that one keeps the
  family's item reject.
- **The re-report**: `GuestCommandReconciliation.IsReReported` includes the new kind, so a swallowed
  take-out is re-sent whole-frame and answered from the operation window.

### Rejected routes, so they are not retried

- **Stating the old parent's contents** (the container report as the carrier): `TrySyncContainerFacts` takes
  every descendant the report does not name to Terminal — it would terminalize the child it was meant to
  relocate, on every re-home. The ticket's own 2026-10-18 correction reached this first.
- **Widening `TryUpdateState` to apply location**: `ItemStateCodec.CaptureItem` stamps a contained child
  with its PARENT's slot, so a state report's slot is not a claim about the item's own place. Reading it as
  one would relocate contained items out of their containers, and it would make every state report a
  location report across every caller.
- **Accepting a same-owner pickup as a re-assertion** (so the plain pickup could carry this report): tried,
  and REJECTED with the failure observed — the re-assertion breaks the documented convergence of a pickup
  behind a LOST drop (`GuestCommandReconciliation`: the refusal is what rolls this side back before the
  older report lands); with it the stale pickup stands and the older report then moves the item to the world
  underneath it. The test that pinned it failed on the pre-fix module and passes once the verdict stays a
  Conflict, which is why the distinct kind exists instead.
- **Leaving the radial wear to a follow-up ticket**: tried and withdrawn in review. The radial wear is the
  same CARRIED item with the wear encoding as its slot — `Body.WearWearable`, not the wound view's
  `useLimbAction` — so it needs this report, not the applied family's. The wound-view apply (a splint out of
  a bag onto a limb) is the applied family's own gap and is ticketed separately.

## What changed

- `WireCommandKind.ItemContainerTakeOut` + `WirePayloadType.ItemContainerTakeOutCommand` (31), the frame
  validator's payload→kind mapping, `KernelWireMapper.FromWireCommand` (the pickup's command),
  `WireCommandAuthorityPolicy`, `KernelProtocolCommandHandler.IsItemOperation`.
- `IItemControl.SendItemContainerTakeOut` / `ItemService` / `ItemMessageFlowService`: a host writes its own
  kernel row locally (`ItemProjection.ApplyPickup`) and reports nothing; a guest sends the command.
- `KernelProtocolCommandHandler` (the unjudged arm) and `KernelCommandGateway` (`MayTakeIntoOwnHands`, both
  pickup-shaped kinds).
- `ItemCommandRejectionRouter` (the take-out's settlement, `InvalidTransition` excepted).
- `ContainerArrivalPolicy` (new, Runtime, pure), `DropPendingState.Source.CarriedContainer`,
  `ItemDropState.TryCancel(… out Source)`, `ContainerItemSync.OnUnloadedFromContainer`,
  `PickupSync.OnPickedUp`, `ItemSlotSync.ReportCarried`.
- Docs: the report-validation census rows, the sync-coverage matrix (row I5, the wire vocabulary index and
  its evidence anchor), the new wear-ticket replacement, this ticket, and the self-check page.

## Verified in this cycle

New behaviour tests, run green on the deployed-artifact commit's tree:

- `ContainerTakeOutTests` (over the wire, three-node simulation): the unjudged answer creates nothing and
  settles by the statement; a contained child's row leaves the container; the child survives the bag's next
  container report; the Conflict settlement for an already-carried item; the bag-CHANGED-HANDS take-out is
  accepted; a plain pickup may not take another member's contained item; a world container's child relocates
  off its world row; the plain pickup keeps its protocol-violation answer for an unjudged id.
- `ContainerArrivalPolicyTests`: the three homes' report table, plus the source pin on the two adapter call
  sites. `ItemPickupRelocationTests`: the kernel pins (the relocation, the Conflict, the other member's row,
  and the child surviving the next container report). `DropPendingStateTests` / `ContainerLoadClassifierTests`:
  the new source's cases.
- Two failures were observed BEFORE the rules that close them: the plain-pickup alias hole is red on the
  unmodified gateway (a pickup of another member's contained item relocated it), and the changed-hands
  take-out is red under a child-owner eligibility rule (and green under the home-row rule).
- The defect itself — a report that is never sent — has NO unit red: the decision lives in the Unity adapter,
  which has no harness. Its evidence is the acceptance row below, which is why the ticket stays in `review/`.
- Full suite: 6113 behavioural tests and 751 normative gates pass; build 0 warnings 0 errors; `dotnet format`
  clean.

## What the session read (batch `20261025-b`, 2026-10-20)

Three clients, one world. On the owner's body, in process (`Body.PickUpItem` / `Body.WearWearable`, the
`body-call` shape the note below names): the member took the bag child out onto a ring slot, then loaded
another item INTO the bag. The host logged no `[ContainerSync] … no longer names …` warning for the child,
the child's kernel row stayed `Carried` (`a1b-host-table.json`), and a slot move after the report was
accepted (`a1c-*`). The third client read the child at its slot, not inside the bag (`a1-alt-clone.json`).
The radial wear of a bag child landed on the limb (`parentAfter: UpTorso`, `origin: limb1`, `wearable:
true`) and left the same clean record (`a2-*`). The wear's window does carry the clone-fact monitor's
in-flight pair — the carrying fact is applied 1–3 ms after the snapshot — which is a separate ordering
defect filed as `review/snapshot-overtakes-item-reports.md`, not a terminal record. Full
reading: `docs/evidence/acceptance/pickup-early-return-kernel-relocation-20261025-b.md`.

## Limits

- The wound-view apply of a container child (a splint out of a bag onto a limb) is the APPLIED family's gap
  and stays ticketed (`todo/container-child-limb-install-keeps-a-contained-record.md`); it is a different
  native entry (`PlayerCamera.ApplyWoundItem` → `useLimbAction`) and a different kernel destination.
- The peers' clone fact table learning the bag's own change from an EVENT, with zero `[CharSync]
  divergence`, is read by the sibling ticket's batch (`done/container-content-event-gap-on-repick.md`):
  its drag rows and the owner-local control are clean; the radial wear's own monitor pair is the ordering
  defect above.
- The send order (report before the slot fact) is a design choice, not an observed requirement: both orders
  end with the data on the owner's row, and no test distinguishes them.
