# Give an item to another player (the push direction)

- Status: Todo — **user-promoted (2026-10-06)**: recorded as future work earlier the same day, then promoted when the user ruled it "matches intuition — it should be done". The receiver-initiated half is landed; this is the giver-initiated half.
- Priority: Medium
- Category: Remote inventory / native interaction parity / new capability
- Source: User question (2026-10-06), asked right after the occupied-slot swap fix landed — "is there a way to actively put my own item into another player's backpack, e.g. drop it on the ground, open their backpack and drag it in?" The same day the user promoted it from `future/` into work. The gesture and presentation decisions below are the user's to answer at implementation time.
- Related: `done/remote-inventory-native-parity-rework.md` (the rework this extends), `todo/remote-backpack-access-policy.md` (the host policy that must gate this gesture too), `done/local-item-into-remote-display.md` (the refusal that stands in for it today; its own non-goals name this ticket), `docs/architecture/remote-inventory-native-parity.md` (the intent vocabulary and the `TransferToBody` row), decisions 217/218 (one mutation site, the wire carries the native intent, the viewer never mutates a proxy), `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md` §4.3.

## What exists today (both directions of one hand-over, only one carried)

- **The receiver takes it — landed.** The operator opens the other player's backpack, drags a display proxy and
  releases it with the ring back on its OWN body (the double-Tab flow): the window classifies that release as
  `TransferToBody` (`RemoteDragIntentCapture.CapturePickUp`, taken when `DestinationIsOwnerBody` is false), the
  host validates and runs the landed custody transfer
  (`PlayerInventoryTakeService.HandleRemoteBackpackTransferToBody`), and the destination body then runs R9's own
  sequence on its own slots. So a hand-over works today as "the other player takes it out of my backpack".
- **The giver pushes it — refused, observably.** With another player's backpack open, releasing THIS client's
  own item onto anything of that displayed inventory — a ring slot, an item shown in it (their bag included), or
  the container window's back panel — is cancelled at the release seam and answered with one Warning line
  (`PlayerCameraDragUsePatch.ResolveDisplayProxyTarget` → `IRemoteBackpackPatchBridge.ReportLocalReleaseOntoProxy`).
  The refusal is deliberate and read on the machine (batch `20261006-e` rows E1/E2, with E3 as the local
  control): the native container branch would load the item INTO the display proxy, and the clone rebuild then
  destroys it — the reported item loss of batch `20261006-d`. No member of the frozen intent vocabulary
  (`RemoteInventoryIntentKind`) names "give my item to that player" either, so the family's rule — the viewer's
  backpack renders the owner's facts and its gestures produce intents, never a half-applied local move — leaves
  the gesture refused.
- **The routes that do work today**: the receiver takes it (above), or the item is dropped and the other player
  picks it up (or auto-picks it up by walking over it) — normal world pickup, host-validated as always.

## What it would take

- **A new intent kind** whose operands are the item, the destination body and the destination slot or container —
  the mirror of `TransferToBody`, where the item's owner is the ACTOR and the destination body belongs to the
  other player. The wire changes; pre-release that ships with this commit and this ticket (the number is a frozen
  baseline until the first official release — decision 241), and the handshake stays the compatibility boundary
  either way, so no dual shape is kept.
- **The owner-side half stays native and two-sided** (decisions 217/218): the giver's release mutates nothing on
  a proxy, and the RECEIVING client runs the native pickup on its own body, so its slot rules, its container
  weight/tag rules, its animations and its sounds decide whether the item fits. The host-side mirror edit is what
  the rework deleted and must not come back.
- **The host validates** permission, membership, the ownership fact (the item is the requester's, the destination
  body is not), the operands and the destination, arbitrates the item first-writer-wins and records the resulting
  fact; it still never models inventory contents.
- **The gesture itself is CUO's own.** The native game has no remote backpack, so there is no native release
  branch to inherit here — unlike every other row of the rework. The nearest native precedent is the trader
  hand-in (`TraderScript.GiveItem`, already carried as `GiveToTrader`), which is a one-sided give to a world
  entity; this one is a two-sided custody move between two clients' item domains.

## What a fix has to decide (user-visible, so the user's call)

1. Which release means "give": onto a ring slot of the displayed backpack, onto an item shown in it (so their bag
   takes it), onto their body in the world, or all of them.
2. What the receiver sees and hears. There is no native counterpart to inherit, so this is a presented decision:
   the item simply appears in the slot its own native rules accepted (with the native pickup feedback), or a
   hand-over presentation (the giver's throw, a catch). The 2026-09-21 ruling's target — indistinguishable from
   operating your own inventory — cannot be inherited here and has to be answered on purpose.
3. What a full or refusing destination does: the native weight/tag/slot rules refuse it, with the native feedback
   and an observable line — the same "refused, never a silent no-op" rule the rest of the family follows.

## Acceptance (the rows a run must read)

A three-client run (operator = the giver, owner = the receiver, third peer = a watcher):

| # | Scenario | Expected |
|---|---|---|
| 1 | The giver drags its own item and releases it on a ring slot of the receiver's displayed backpack | The item leaves the giver's slot, appears in the receiver's slot (or the receiver's own native rules place it), the third peer sees the same, and the giver's log carries the intent plus the receiver's replay |
| 2 | The same, aimed at an item of the receiver's that is itself a container | The item enters that container when the receiver's own `Container.CanHoldItem` allows it, and stays put (no vanish) |
| 3 | The same, with the receiver unwatching / not in world, or the giver mid-drag when the view closes | Refused with one observable line, and nothing moves on either side |
| 4 | The receiver's slot or bag refuses the item (occupied slot, weight/tag rule) | Refused on the RECEIVER's client by its own native rule, with the native feedback and an observable line |
| 5 | The local control: the same release onto the giver's OWN backpack | The native local move, unchanged |
| 6 | A host with the remote-backpack access policy set to deny | The gesture is refused by the policy, naming it |

## Non-goals now

- Not part of the rework's current acceptance matrix: this is a new capability, not a defect, and no row of
  `done/remote-inventory-native-parity-rework.md` depends on it.
- Not a change to the receiver-initiated take (`TransferToBody`), which stays as it is.
- Not the access policy itself: the three-way host rule that must gate this gesture is
  `todo/remote-backpack-access-policy.md`, and it lands separately (this ticket obeys whatever that rule says).
## Design decided (2026-10-17 — the two questions above, answered by the user)

The gesture and the presentation were the user's call because this gesture has no native counterpart to
inherit. Both were answered:

1. **Which release means "give"**: BOTH of the displayed backpack's own targets — a ring slot of the displayed
   backpack (the item goes to the receiver's body) and an item shown in it (the item goes into that container,
   when the receiver's own container rules allow it). This is exactly the mirror of what the receiver-initiated
   half already does, so both directions of one hand-over read the same way and a player learns one gesture.
2. **What the receiver sees and hears**: entirely native — the item simply appears in a slot the receiver's own
   rules accepted, with the native pickup feedback. No new presentation is built: the project's existing target
   for this family ("indistinguishable from operating your own inventory") is what applies, and the game has no
   hand-over animation to reuse.

Question 3 of that list (a full or refusing destination) is already answered by the family's own rule and needs
no decision: the receiver's native rules refuse it, with the native feedback and one observable line.

## Where the change lands (surveyed 2026-10-17, so the next session starts from here)

- **The intent vocabulary**: `RemoteInventoryIntentKind` holds `TransferToBody = 7`, which is the
  receiver-initiated half this gesture mirrors; the new kind takes the next free number, and the wire change
  rides this commit and this ticket (decision 241 — the pre-release baseline stays frozen).
- **The release seam that refuses it today**: `PlayerCameraDragUsePatch` calls
  `bridge.ReportLocalReleaseOntoProxy(dragItem, target.OwnerSteamId, target.Description)` where the drag item
  belongs to THIS client and the target belongs to the other player. That call is the entry point: it becomes a
  classification instead of an unconditional refusal.
- **The classification half**: `RemoteDragIntentCapture` already turns a release onto a proxy into the frozen
  vocabulary (its `CapturePickUp` adds `TransferToBody` with `LocalSteamId` as the destination body). The new
  kind's operands are the item, the DESTINATION player's SteamId and the destination slot or container id — the
  mirror image of that line, where the item's owner is the actor rather than the destination.
- **The host's native-intent half to mirror**: `PlayerInventoryTakeService.HandleRemoteBackpackTransferToBody`
  ("the requester's own ..."), dispatched from `PlayerRemoteInventoryIntentService`'s switch. The new kind's
  host half validates that the item is the REQUESTER's, that the destination body is NOT the requester's, that
  the destination player is a member, and the access policy — then arbitrates the item first-writer-wins and
  records the fact. It still never models inventory contents.
- **The receiving client's branch**: `RemoteIntentApplier`'s switch is where a receiving client runs the native
  operation on its own body. The new kind's branch is the same shape as `TransferToBody`'s — the receiver runs
  the native pickup so its own slot, weight and tag rules decide — with the ownership read from the other side.
- **The refusal that must stay observable**: today's one Warning line is what a run reads in row 3 of the
  acceptance table. If the gesture is refused (unwatching receiver, policy deny, missing membership), the line
  must still be there and name which check refused it.

## What this ticket still owes, and what it does not

- It obeys the access policy of `todo/remote-backpack-access-policy.md`; that ticket lands separately and this
  one does not build the policy.
- The receiver-initiated half (`TransferToBody`) is not touched.
- A new intent kind with a client half on both sides is the largest shape this family has, so it carries the
  whole acceptance table above: six rows, three clients, including the local control (row 5) that proves the
  same release onto the giver's OWN backpack still runs the native move.
## The protocol half, surveyed and measured (2026-10-17 — do this first next time)

Both facts this ticket's first step needs were checked against the tree, and both came out cheap:

- **The number is free**: `RemoteInventoryIntentKind` runs 1..15 and then jumps straight to `GiveToTrader = 17`,
  so **16** is the next free one and no existing value moves.
- **The operands already exist, and the mirror reads correctly**: `RemoteInventoryIntentMsg` already carries
  `OwnerSteamId`, `ItemInstanceId`, `TargetContainerInstanceId`, `TargetSlotIndex` and `TargetBodySteamId`.
  The two halves of one custody transfer read the first and the last from opposite sides — for `TransferToBody`
  the owner is the one being taken FROM and the destination body is the requester's own; for the new kind the
  owner is the REQUESTER giving their own item away and the destination body is the other player's. So the
  change needs no new field, only the two doc comments that currently describe the take-direction only.
- **The protocol number does not move** (decision 241): the pre-release baseline stays frozen and the change
  rides this ticket's own commit.

**Why it is not in the tree yet**: a new enum member with no reader is a dead mechanism by this project's own
rule ("a mechanism needs a named consumer"), and a scan cannot see the difference — the whole suite and every
gate stayed green with the member added and nothing dispatching it. The new kind, its host half and its
receiving-client half have to land together, so the first commit of this ticket carries all three.