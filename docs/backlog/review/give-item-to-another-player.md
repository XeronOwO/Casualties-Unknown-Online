# Give an item to another player (the push direction)

- Status: Review — row 4's item loss is fixed (a container refusal places the object it materialized in a slot
  on the receiver's body; a body that cannot take it reports the item's end, loudly and on both sides) and its
  re-read is owed to the next acceptance batch. Rows 1 (both shapes), 2 and 6 were read whole in earlier batches;
  row 3 was read on its refusal half only (the "receiver's view closes mid-drag" variant is still unrun); row 5
  passed in batch `20261021-a`. The fix changed no wire member. Record:
  `docs/evidence/acceptance/give-item-to-another-player-20261021-a.md` (the rejection this answers).
- Priority: Medium
- Category: Remote inventory / native interaction parity / new capability
- Source: User question (2026-10-06), asked right after the occupied-slot swap fix landed — "is there a way to actively put my own item into another player's backpack, e.g. drop it on the ground, open their backpack and drag it in?" The same day the user promoted it from `future/` into work. The gesture and presentation decisions below are the user's to answer at implementation time.
- Related: `done/remote-inventory-native-parity-rework.md` (the rework this extends), `todo/remote-backpack-access-policy.md` (the host policy that must gate this gesture too), `done/local-item-into-remote-display.md` (the refusal that stands in for it today; its own non-goals name this ticket), `docs/architecture/remote-inventory-native-parity.md` (the intent vocabulary and the `TransferToBody` row), decisions 217/218 (one mutation site, the wire carries the native intent, the viewer never mutates a proxy), `docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md` §4.3, `todo/restore-refused-child-keeps-its-id.md` and `todo/placement-outcome-never-returns.md` (the two findings this ticket's row-4 fix left to their own tickets).

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

### The user's ruling, restated 2026-10-18 (it is a DECISION, not a ticket-local preference)

Asked again in the same words, the user answered and these are the three answers the implementation owes:

1. **A release on ANY slot of the other player's displayed backpack counts as giving.** An empty slot takes the
   item; a release onto an item shown there is decided by the RECEIVER's own container rules (so "into that
   container when it fits" and "not into it when it does not" are both correct outcomes, and neither is a silent
   no-op).
2. **The receiver sees exactly what it would see picking the item up itself** — no hand-over animation is built.
3. **When the destination cannot take it, the receiver's own rules refuse it and one observable line is written.**

That ruling is the acceptance bar for rows 1–4 of the table above. It lives here, in the ticket, because it is
this ticket's design; a goal or a session note is not where a decision is recorded.

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
## The release seam, surveyed in detail (2026-10-17 — start here next time)

Everything the classification half needs was located, and one gap was found. In the order they have to be solved:

1. **The target the release points at carries only a sentence.** `PlayerCameraDragUsePatch.ResolveDisplayProxyTarget`
   returns a two-field record (`OwnerSteamId`, `Description`) built at three places: an item shown in the
   displayed inventory (`$"display proxy item {item.id}"`), a body slot (`"a slot of the displayed inventory"`),
   and the container window's back panel (`$"display proxy container {containerItem.id}"`). The operands are all
   there at construction time and are lost into the string — an item's instance id, and, for the body-slot case,
   `InvButton.slot` (`reversing/Assembly-CSharp/Assembly-CSharp/InvButton.cs` declares `public int slot`; the
   patch already reads the component as `InvButton` and tests `button.isBody`). Widening that record with the
   item id (0 for a slot) and the slot index (-1 for a container) is the first edit, and it is mechanical.
2. **The seam has to answer a question, not just report.** `IRemoteBackpackPatchBridge.ReportLocalReleaseOntoProxy`
   currently returns `void` and writes the refusal line itself. It becomes `bool` (true = classified and sent,
   false = refused) and gains the two operands, so the refusal line stays where it is and the caller only decides
   whether to clear the drag. The seam is pinned as a whole-string census in
   `PatchBridgePortShapeGateTests`, so the interface, the bridge's forwarding member and that pin move together.
3. **The giver's own item instance id is reachable, and it is NOT the proxy query.** The proxies' ids come from a
   marker component (`RemoteDragProxyQuery.InstanceId` reads `RemoteInventoryItemId`), which a LOCAL item does
   not carry — so in this branch, where `dragItem` is deliberately the local item, that query answers 0. The
   right call is `OperationTrace.IdOf(item)`: the tracing helper reads the item's OWN `ItemInstanceId` component
   and returns 0 only when the item genuinely has none. That 0 is exactly the refusal condition this family
   already uses for an unoperable item, so classification reads: a resolvable id, a target owner that is not this
   client, and a destination that is a slot index or an item id. Anything else keeps today's refusal line, which
   is what the acceptance table's row 3 reads.
4. **Then the three halves land together** (the kind at 16, the host validation mirroring
   `HandleRemoteBackpackTransferToBody` with the ownership read the other way round, and the receiving client's
   branch in `RemoteIntentApplier`), because a new kind with no dispatcher is a dead mechanism no gate can see.

**A note the first survey got wrong, corrected above**: the giver's item id was recorded as an open question. It is not — the tracing helper already answers it, and the answer's zero case is the refusal this family already makes. The remaining work is written in the four steps above.
## The implementation landed (2026-10-17), reviewed, and what it still owes

The intent is real end to end now: `GiveToBody = 16` is the mirror of `TransferToBody = 7`, the release seam
classifies instead of refusing, and the host runs the SAME custody core with the two ends swapped — so the two
halves of one hand-over cannot drift apart about who owns the item.

**An adversarial review of the frozen change set found the kind was DEAD and my own test was agreeing with it.**
The host's entry guard assumed `owner != requester`, which is the take direction's rule; the give direction
sends exactly `owner == requester` by construction, so every give was refused before its own case ran and the
host half never executed. The test that "proved the branch was reached" passed for the same reason — it would
have passed with no case at all. Both are fixed: the guard is scoped by direction (keeping the rule it exists
for — a client may not claim an item it does not carry), the destination body is checked in world separately
because it is a different player from the requester, and the mirrored host-half case is back and GREEN, which
is the evidence the first attempt could not produce.

Also fixed from the same review: the new `bool` on the seam had no reader (the caller clears the drag either
way, so it is `void` again — a return value nothing reads is a guess), a stale summary was left stacked on the
new one, and the intent's own doc claimed a receiving-side native rule that this path does not run.

**What it still owes, and why the ticket stays in `todo/`:**

- **The container half is not implemented.** A release onto an ITEM shown in the receiver's inventory names that
  container, and the host refuses it by name because the mirrored core takes a body slot. The user's own answer
  covers both targets of the displayed backpack, so this is a scope cut of a user-visible behaviour and is
  recorded as one.
- **A refusal after the send is invisible to the giver.** The actor's client writes a positive line and nothing
  else; every later refusal — the policy, the worn item, the destination not being in world — lands on the
  HOST's log, and nothing on the wire answers a refusal. Acceptance row 3 reads the actor's screen, so it needs
  a status line on that side before it can pass.
- **The receivers' own rules never decide anything.** The host computes both snapshots and the receiver applies
  the fact through a path whose placement bypasses the native pickup checks, so "the receiving client's slot,
  weight and tag rules decide whether it fits" — which the ticket's design states and the new kind's doc
  repeated — is not what happens. Acceptance row 4 is unreachable as written.
- **The wire message's own member docs and the vocabulary page are stale** (`TargetBodySteamId` still says
  "the requester's own SteamId", `docs/architecture/remote-inventory-native-parity.md` §3.3 has no row for this
  kind and still says the hole at 16 was deliberate).
- **Every acceptance row is unrun**; the table needs three clients, and rows 1–4 and 6 need the two gaps above
  closed first.

## Readings taken on the machine, one live session (2026-10-18)

The gesture was driven by a function call rather than a cursor: the release seam is
`ReportLocalReleaseOntoProxy`, and the recipe `give-probe` calls the same method the drag patch calls, with a real
`Item` from the local body. That is what made these rows reachable at all — the run has no mouse, and the tooling's
own rule says a cursor API is a rule question.

Both ledgers were read through the product's own console (`/data get`), not through a log line:

- **Row 1, free destination — PASSES.** The giver's `bandage` (#9492956821) aimed at slot **5** arrived in the
  host's slot **5**: the host then listed `#9492956821 bandage [slot 5]` and the giver no longer listed it.
- **Row 1, occupied destination — PASSES, with the placement the native rules give.** The giver's `emergencylight`
  (#5197989525) aimed at slot **3** arrived in slot **0**, and the host logged
  `[Take] destination slot 3 is not free for <receiver>; using slot 0 instead.` The slot really was occupied by an
  item this client had never recorded, so the fallback was correct rather than a silent move to a wrong place —
  and the requested slot is named in the line.
- **Row 6, policy off — PASSES.** With `/hostrules {"allowRemoteInventoryTake": false}` on the host, the giver's
  release was refused before it was sent —
  `[RemoteIntent] refused GiveToBody: host has disabled remote inventory manipulation` — and the host's item list
  was byte-for-byte what it had been. So this row needs no post-send status line: the rule arrives announced and
  the refusal happens on the actor's own side, which is where the row reads.

**Still unrun, and why:** row 2 needs the container half the section above records as not implemented; row 3 needs
a receiver that leaves the world mid-gesture; row 4 needs the receiver's own rules to decide, which the same
section records as not happening; row 5 is the local control (the same release onto the giver's OWN backpack),
which is the untouched native path.

**One thing these readings add to the section above:** the two intra-slot placements show the transfer is already
correct on BOTH sides at the ledger level, so the remaining gaps are about WHICH slot and WHOSE rules decide —
not about whether the item moves.

## The container half, surveyed: the wire and the receiver already carry it (2026-10-18)

The first gap above ("the container half is not implemented") is smaller than it reads, and the survey says exactly
where it lives:

- **The wire already has the field.** `PlayerInventoryTransferMsg.TargetParentItemId` is documented as "the
  authoritative destination container's instance id. 0 = place directly into an inventory slot", and
  `CharacterItemMsg` carries `Contents` recursively, so "inside that container" is expressible without a new
  message or a new member.
- **The receiving client already does the native thing, and already refuses the native way.**
  `PlayerInteractionApply.OnPlayerInventoryTransfer` branches on `msg.TargetParentItemId`: a same-owner move re-homes
  the existing item through `TryMoveExistingItemToLocalContainer` and, when the native container refuses, leaves the
  item where it is and logs
  "same-owner container move refused by the native container: … stays in its current location; no destructive
  rebuild was performed"; the cross-player branch calls `AddCarriedItemToLocalContainer(body, item,
  msg.TargetParentItemId)`. So the receiver's own container rules are the judge on this path already.
- **The host is the only side that refuses.** `PlayerRemoteInventoryIntentService`'s `GiveToBody` validation refuses a
  container destination by name — "a container destination (item …) is not carried yet: only a body slot can be
  named" — and `PlayerInventoryTakeService.HandleTakeCore` resolves a body slot unconditionally, so there is no
  container destination to name in the first place.

**So the change is one side's worth of work, not three**: validate a container destination against the receiver's own
snapshot (the container must be one of ITS carried items), put the item into that container's `Contents` in the new
snapshot instead of the top-level list, and set `TargetParentItemId` on the transfer message. The receiving client
then loads it with its own `Container` rules and refuses it there if it does not fit — which is the user's ruling
("the receiver's own container rules decide") rather than a new behaviour.

**Two things to be careful about when it is implemented**, both learned the hard way in this family:

- The kernel's container facts are DESTRUCTIVE for children the report does not name (`ChildrenTheReportDrops`
  takes them to a terminal state that no later command may revive), so a container destination must not be reported
  by sending the container's contents minus anything — the fact to report is the arrival, and the container's
  contents must name every child it still holds.
- The slot half's bookkeeping (`AdoptTransferredItem` / `CommitCarriedToHost`) records a carried item at a SLOT; a
  container destination has to record the nesting instead, or the two sides' ledgers will disagree about where the
  item is even though both show it exists.

## The container half, LANDED (2026-10-18)

Implemented, with the adversarial review of the frozen change set run first and its findings fixed in the same
change:

- **The operand validation stopped refusing a container destination.** `GiveToBody` now accepts exactly one
  destination — slot OR container — and refuses both-or-neither; the self-containment check moved next to the other
  operand checks (it needs nothing but the message). Whether the named item exists, and whether it is a container,
  is decided against the RECEIVER's snapshot in the host core, by name.
- **The host core nests the item and names the container.** `HandleTakeCore` grew a container operand: it finds the
  destination in the receiver's cloned snapshot, appends the item to that item's `Contents` (never the top-level
  list), and publishes the transfer with `TargetParentItemId` set.
- **The ledger entry states "in a container" (-1), not a slot.** One item fact was serving two conventions: the
  snapshot wants a container child's `SlotIndex` to be its PARENT's slot, while the transfer table and the kernel
  read a `-1` as "carried, no slot of its own". Claiming a slot put the item in that slot on every clone — the bag
  and its new child competing for one slot — and, for a wearable container, would have been read as a worn-limb
  index. The message keeps the snapshot shape; the ledger copy states -1.
- **A refusal by the container's own rules is now answered instead of losing the item.** `RestoreContent` answers
  the item it materialized (today as `RestoredContent`, which also says whether the container took it and why it
  did not), and it checks `Container.CanHoldItem` BEFORE loading, because the native
  `LoadItem` returns silently when the weight or tag rule fails: the blind load left a materialized item with no
  parent, carried by nobody and named by no report, and the next character snapshot swept it away — the item was
  gone from both players. Now the receiving side logs why it did not fit and places it in a slot instead.
- **The stale protocol docs are fixed**: `TargetParentItemId` no longer claims to be for same-owner moves only,
  `TargetContainerInstanceId` and `TargetSlotIndex`/`TargetBodySteamId` name `GiveToBody` and the end each operand
  belongs to, and the vocabulary page has a `GiveToBody` row instead of "the enum keeps the hole at 16".

**Still owed, and why the ticket stays in `todo/`:**

- **A refusal on the host still does not reach the giver.** Every refusal after the intent is sent (no such
  container, item worn, destination not in world) lands in the host's log only; the giver's client has already
  written a positive line and nothing follows. The existing gap now covers one more refusal, and the fix is the same
  one: answer it on the wire (`CommandRejected`) and print it where the player acted. **Landed since**: the
  refusal answer is `review/intent-refusal-never-reaches-the-actor.md`, whose own record reads its row 2 as passing;
  what remains open in that shape is not this ticket's.
- **Row 2 is still unrun on the machine.** The code is there and the unit-level red went green, but "an item
  released onto an item shown in the receiver's inventory enters that container and stays put" needs the live
  reading, and so does its refusal shape (a container that cannot hold it falls back to a slot, observably).
- **The same-owner container branch in `PlayerInteractionApply` is dead code** — it requires `From == To`, and the
  only publisher refuses that case, so it and its "refused by the native container" log can never run. Recorded
  rather than deleted here because deleting it moves a behaviour the family may still want to route through.

### Row 2, read on the machine (2026-10-18)

One live session on the deployed artifact, driven step by step rather than by a script:

- The receiver's `trashbag` was put in its slot 0 through the game's own grant (`item-provide type=trashbag`), and
  the giver held a `bandage` and an `emergencylight` from its own supply.
- The giver's release was aimed at that bag — `slot = -1`, `container = <the bag's instance id>` — which is exactly
  the operand shape the drag patch produces for a release onto an item shown in the other player's inventory.
- **The two lines that answer the row**, in this order:
  `[Take] <receiver> takes emergencylight (id 5197989525) from <giver>` and then
  `[PlayerInteraction] placed emergencylight (id 5197989525) into container trashbag (1241566529475).`
  The second line is printed only after `Container.CanHoldItem` accepted the item, so the bag's OWN rules said yes.
- **The receiver's ledger agrees with the nesting**: it lists the item as `emergencylight [carried]` with no slot,
  beside `trashbag [slot 0]` — the -1 convention this change introduced, doing what it was introduced for (a bag
  and the item inside it no longer compete for one slot).
- The giver's ledger no longer lists it at all.

**So row 2 PASSES.** What is still unrun in its family: the refusal shape (a bag that cannot hold the item → the
fallback to a slot, with the line naming why). That needs an item too heavy or wrongly tagged for the bag, which is
row 4's own scenario and is still owed together with it.

### The refusal shape, read — and it found a real gap (2026-10-18)

Driven in the same live session, aiming a give at an item the receiver carries that is NOT a container (a spare
`bandage`, so the receiving side's container lookup fails at `GetComponent<Container>()`):

- The receiving client answered exactly as designed:
  `[Warning] [PlayerInteraction] bandage (id 9492956821) not placed into container 1245861496771 (the target is not
  a container) — falling back to a slot.` — and the item did NOT vanish; it is in the receiver's body.
- **But the host's ledger never learned where it actually went.** Both handed-over items read
  `emergencylight [carried]` / `bandage [carried]` — "carried, no slot" — and re-reading a minute later still does.
  For the emergencylight that is correct (it is inside the bag); for the bandage it is WRONG: the receiver put it in
  a slot, and nothing tells the host that.

**Root cause, and it is one mechanism for two of the owed items.** The host takes the container path on the
sender's word that the named item is a container; whether it really is one, and where the item ends up when it is
not, is decided on the RECEIVING client, and the receiving client's answer never travels back except through the
character re-report — which updates the character snapshot but not this ledger entry. So:

- the ledger states a location the receiver did not use (this finding), and
- a host-side refusal still reaches nobody (the gap listed above).

Both are the same missing edge: **the receiving client's placement outcome has to travel back to the host**, and it
is also what should answer the giver. The wire already has the shape for answering a refused report
(`CommandRejected` with a `RefusedKind`); what it does not have is a positive "it went here instead" correction.
That is the next piece of work on this ticket, and until it lands the two `[carried]`-without-a-slot readings are
the evidence that it is needed.

### The design for that edge, decided by reading the two ends (2026-10-18)

Two routes were on the table. The one that lands is the cheaper AND the more honest, and it was found by asking
what each side actually KNOWS:

- **Rejected: let the receiver report its placement back and have the host patch its ledger.** It needs a new wire
  message for the success case, and it leaves the host briefly believing a location that is wrong — a fact with two
  owners until the correction lands.
- **Chosen: the GIVER says whether it pointed at a container.** The giver's client is the only side that ever knows
  this: the release resolves a display proxy whose `Item` really is (or is not) a `Container`, while the host's
  snapshot has no "is a container" flag at all. So the operand gains one bit — `TargetIsContainer` on
  `RemoteInventoryIntentMsg` — and the host refuses a destination that claims to be a container but is not, and
  refuses a container-named destination that does not claim to be one, BY NAME and before it records anything.

That removes the wrong nesting at its source (the host never again puts an item inside a plain item), and it leaves
exactly one gap behind, the one already recorded: a host-side refusal still has to be answered to the giver.

**What wiring it needs, surveyed so the next attempt starts at the right place:**

- `RemoteInventoryIntentMsg` — the next free `ProtoMember` after the limb operand; the number is a pre-release
  baseline, so it rides this ticket's own commit (decision 241).
- `PlayerCameraDragUsePatch`'s `DisplayProxyTarget` and `RemoteDragIntentDispatcher.ReportLocalReleaseOnsProxy` —
  the target struct has to carry the bit from the proxy's own `Container` component; the dispatcher currently
  receives only the target's instance id, so the flag has to be threaded through with it.
- `PlayerRemoteInventoryIntentService`'s `GiveToBody` operand validation — the two refusals above belong here,
  beside the self-containment check, because both need only the message.
- The `HandleTakeCore` container branch keeps its `FindCarriedItem` existence check (the receiver's snapshot is the
  authority on what it carries); what it no longer has to do is guess whether the named item is a container.

**Until that lands**, the honest reading of the live run is: the container destination works, and a destination
aimed at a plain item still ends with the receiver placing the item in a slot and saying so — with the host's ledger
unaware of the slot. That is the symptom the bit removes, not a reason to call row 2 unverified.

### It landed smaller than planned: the classification is the giver's, and needs no wire field (2026-10-18)

`ResolveDisplayProxyTarget` already has everything the decision needs — the hit `InvButton` (so the ring slot) and
the item shown in it (a REAL object with or without a `Container`). So the release now classifies at the seam:

- the hit object IS a container → name it as the destination (unchanged), and
- it is NOT → name that RING SLOT instead, and no container at all.

No `TargetIsContainer` bit, no new `ProtoMember`, and the host can never again be told to nest an item into a plain
item. A peer that lies anyway still lands somewhere safe: the receiver's own lookup refuses it by name and places it
in a slot, which the previous section's livetest already read.

**Verified on the machine, in the still-running session**: a release encoded the way the classification now encodes a
non-container target (`slot = 2`, `container = 0`) put the item in a REAL slot on the receiver — the destination was
substituted to slot 4 because 2 was taken, exactly as `ResolveDestinationSlot` documents — and the receiver's ledger
states `bandage [slot 4]`. That is the shape the container path cannot give (it states `[carried]`), so the two
encodings are now visibly different in the ledger, which is the whole point.

**What is still not drivable**: the classification itself lives in the drag seam, so reading it end to end needs a
real drag and therefore a cursor. The seam's own inputs are pinned by the operands it now sends, and the host-side
half is covered by the encoding check above.

### Row 3, read on the machine (2026-10-18)

The receiver left the world first, then the giver released its own item at it. Two things had to be right before
this row could read anything, and the first attempt got the first one wrong:

- **A release needs an item WITH an instance id.** The first attempt picked a starting-supply item, which has none,
  and was refused at the giver's own seam — "it cannot be named to that player" — which is a correct refusal of a
  different thing. The probe now prefers an item that carries an id, so a reading describes the case under test.
- **Then the host refused, by name and observably**:
  `[Warning] [RemoteIntent] refused GiveToBody: <giver>, <giver> or the destination body <receiver> is not in-world.`
- **Nothing moved**: the giver's ledger still lists the item, and the receiver's client (out of world) never saw a
  transfer.

**So row 3 PASSES** on its "refused with one observable line, nothing moves on either side" half. What it does not
cover — because the receiving client is not in a world at all — is the "the receiver's view closed mid-drag" variant
of the same row, which needs the drag seam.

**Rows now read: 1 (both destination shapes), 2, 3, 6.** Row 4's refusal shape is read in the container form (a
target that is not a container → named fallback to a slot); its weight/tag form is read by batch `20261021-a`
below, and it found item loss. Row 5 is the local control; batch `20261021-a` read it and it still runs the
native path.

## Row 4 read, and it found item loss (2026-10-19, batch `20261021-a`)

Two clients on the deployed `0.1.0+c9769177…` (receiver = host, giver = member). The receiver's `plasticbag`
(`maxWeightPerItem 3.501 g`, measured live) refuses the member's 4 g `toolbox`, and the receiver's own client
writes the rule and the fallback:

```text
[Take] <host> takes toolbox (id 9492956821) from <member> (remote-backpack allowConscious=True, container 1043998033859).
Restore: toolbox (id 9492956821) does not fit plasticbag(Clone) — refused by the container's own rule, not loaded.
[PlayerInteraction] toolbox (id 9492956821) not placed into container 1043998033859 (the container's own rules refused it) — falling back to a slot.
[PlayerInteraction] placed toolbox (id 9492956821) in local slot 2.
```

**But the item is gone from both clients seconds later.** In the same second the receiver logs
`[ItemTrace] op=6 item=9492956821 origin=OnItemDestroyed result=Committed(1)`, the fact table reports
`[CharSync] divergence … left the inventory without an event sync`, the standing-object materializer retires
it, and the host's kernel stores the id as TERMINAL (`container-read mode=host`: `terminal: ["9492956821"]`,
the item in neither `carried` nor `contained`). The receiver's own tree and the giver's tree both no longer
list it, and the giver's `ItemDestroy` for the id is refused
`InvalidTransition (terminal item 9492956821 cannot be destroyed again)`.

**Root cause.** `PlayerInventoryPlacement.AddCarriedItemToLocalContainer` calls
`ItemStateCodec.RestoreContent`, which INSTANTIATES the child with its `ItemInstanceId` and then checks
`Container.CanHoldItem`; on a refusal it logs and calls `Object.Destroy(child.gameObject)` (deferred to the
end of the frame). The caller then falls back to `AddCarriedItemToLocalBody`, which instantiates a SECOND
object with the SAME id and places it in slot 2. At the end of the frame the first object's destroy fires and
publishes the id as destroyed; the kernel marks it terminal, and the second object is swept. The refusal is
observable, but the item does not survive its own fallback.

**What the fix must do**: do not destroy a materialized child the fallback is about to own — hand the same
object to the slot fallback, or destroy it without publishing a destroy for its id — and add a regression
case that drives a container refusal and reads the item alive in a slot afterwards.

**Row 5 passes in the same session**: the member's own bandage released onto its own `plasticbag` ran the
native `[ContainerLoad] bandage … moved inside body container plasticbag`, with `castItemProxy: false`,
`castItemOwner: 0`, `viewOpen: false` and no `[RemoteIntent]` line. The record is
`docs/evidence/acceptance/give-item-to-another-player-20261021-a.md`.

## The loss is fixed (2026-10-19, commit on top of `a7981497`)

The fix takes the first shape the note above preferred: the refusal hands the object on, and nothing that a rung
is about to own is ever destroyed. A container refusal now answers the same way in both directions — the
receiver's own rules decide, and the item falls back to a slot on its body with one Warning naming the
container's reason (the user's ruling).

- **The codec does not destroy a refused content item.** `ItemStateCodec.RestoreContent` now answers
  `RestoredContent` — the object, whether the container took it, and the container's own reason — instead of
  `Item?`-or-null-with-a-destroy. Its two refusal points (`CanHoldItem` false, and `LoadItem` refusing after
  it) both return the real object; the only object it still destroys is a prefab instance that carries no
  `Item` (and so no instance id). The log line the acceptance record quotes is unchanged.
- **The placement places that object.** `PlayerInventoryPlacement.AddCarriedItemToLocalContainer` hands the
  returned item to ONE verified slot loop, `TryPlaceInBodySlot`: the slot the transfer named when it is free,
  else every other free slot in index order, each attempt read back with `body.GetItem(slot) == placed`. The
  read-back is not decoration — the game's own `Body.PickUpItem` refuses a native `onlyHoldInHands` item
  outside a hand slot EVEN with `force` (Body.cs:1390), so an unverified attempt would log a slot the item
  never entered (the review of batch `20261021-b` found that in the first version of this rung).
- **Both placement paths share that loop.** `ItemStateCodec.MaterializeItem` is split out of `RestoreItem`
  (same steps, id stamped before the object's own `Start`), and `AddCarriedItemToLocalBody` materializes and
  then places through `TryPlaceInBodySlot` — so the slot-destination path verifies its landing too, and the
  two paths cannot drift about what "placed" means.
- **A body with no room is a loud, consistent loss, not a silent one.** When no slot on the body accepts the
  item, one Warning reports the item's end (its destroy carries the id, so both clients agree it is gone).
  The world-drop answer this case wants needs the host to re-record the item — see
  `todo/placement-outcome-never-returns.md`.
- **The first version's drop rung was withdrawn after review.** It reported the dropped object as a new
  world-item spawn, and that report can never be applied: the host records the handed-over id as carried
  BEFORE it publishes the transfer (`HandleTakeCore`'s `AdoptTransferredItem`/`CommitCarriedToHost`, and
  `DeliverCreatedItem`'s `Carry`), so `ItemDomainModule.DecideSpawn` refuses the spawn as `Conflict` and a
  guest receiver's object is then deleted by the next world-item keyframe with no trace. `dropWhenRefused` is
  gone with it: a created item whose live container refuses takes the same slot fallback.
- **The bulk restore's policy is unchanged and named.** `ItemStateCodec.RestoreContentDiscarding` is the one
  place a restored content item is still discarded (the character restore, a world item's contents,
  `RemoteItemSceneOps.ApplyAuthoritativeState`); it keeps its old behaviour and its own ticket,
  `todo/restore-refused-child-keeps-its-id.md`, records why.

**The regression test, and its red.** The behaviour suite cannot host this path — a refusal needs real Unity
objects and a container that really refuses — so the invariant is pinned at the source by
`SourceShapeGateTests.ContainerRefusal_HandsTheMaterializedItemToTheVerifiedSlotRung` (member bodies located by
their DECLARATION in three files, with its own matcher contract in
`TheContainerRefusalMatcher_SeesEveryDiscardShapeAndItsPositiveSample`: seven shapes, including a destroy in the
codec, a `Refused` that claims "loaded", the creating rung back in the refusal branch, a slot loop that creates
instead of placing, one without the read-back, and `RestoreItem` back in the slot path). The red was taken before
the fix, on the first version of the gate: six violations on the old code, including
`ItemStateCodec.RestoreContent destroys the content item it materialized …` and
`AddCarriedItemToLocalContainer still calls a rung that materializes from the message AFTER RestoreContent
materialized the item — two objects, one instance id` (`.acceptance/20261021-b/red-container-refusal.log`); the
independent review reproduced all six against `HEAD`'s three files with the current matcher. Green after the fix:
the focused run in `.acceptance/20261021-b/focused-container-refusal.log` (the filter is named in the delivery
checklist's evidence line).

The self-check behind the change, one row per mechanism:

| Mechanism | Change | Evidence |
|---|---|---|
| `ItemStateCodec.RestoreContent` | a refusal answers `RestoredContent.Refused(item, why)` with the object in hand and `Loaded: false`; no destroy of the item | the gate above, red on the old body (two `Destroy(child…)` sites) and green after; its factory is read too |
| `PlayerInventoryPlacement.AddCarriedItemToLocalContainer` | hands that object to `TryPlaceInBodySlot`; the unmaterialized branch (no parent / not a container) creates through `AddCarriedItemToLocalBody`, and a refusal never reaches the drop rung | the same gate (it fails if the materialized hand-off is replaced, or if the drop comes back) |
| `PlayerInventoryPlacement.TryPlaceInBodySlot` | places the object it is given and reads the slot back; tries the named slot then every free one | the same gate (a creating or unverified loop fails it) |
| `ItemStateCodec.MaterializeItem` / `AddCarriedItemToLocalBody` | materialize and place are separate steps, so both paths share the verified loop | the same gate (`AddCarriedItemToLocalBody` must go through both; `MaterializeItem` must stamp the id) |
| the destroy chain `ItemPatches.ItemOnDestroyPatch` → `ItemWorldSync.OnItemDestroyed` | unchanged, and no longer reached by a refused-but-placeable item: it is what turns a discarded id-carrying object into a TERMINAL kernel row | read in the code: the hook filters replay, generation, teardown, display-proxy and standing-item destroys, and reports every other id |

### The independent review's findings, and what each one changed

The review ran in a fresh context against the frozen tree (batch `20261021-b`). Every finding is recorded here
with the code it named, because a fixed finding that is not written down is one the next cycle re-learns.

- **Blocker — the drop rung's spawn report can never be applied.** `ItemDomainModule.DecideSpawn`:
  `if (state.FindItem(command.Identity.InstanceId) is not null) { return DomainDecision.Reject(RejectionReason.Conflict, $"item {command.Identity.InstanceId} already exists"); }`,
  and the row always exists first: `PlayerInventoryTakeService.HandleTakeCore` calls
  `_items.AdoptTransferredItem`/`CommitCarriedToHost` BEFORE `PublishTransfer`, and `DeliverCreatedItem`'s
  `Carry` does the same. A guest receiver's refused spawn is then deleted by
  `ItemReconcile.OnRemoteItemSnapshot` (absent from the keyframe → `KillNow` zeroes the ids, no destroy report),
  and the host receiver's object is never broadcast. **Changed:** the rung is gone, and the no-room case
  reports the item's end; the host-side answer is `todo/placement-outcome-never-returns.md`.
- **Major — the new slot rung silently no-ops for a native `onlyHoldInHands` item.** `Body.PickUpItem`'s guard
  (Body.cs:1390) is `… && (!item.Stats.onlyHoldInHands || this.slots[slot].isHand)`, which `force` does NOT
  bypass, so the rung logged a slot the item never entered. **Changed:** `TryPlaceInBodySlot` tries the named
  slot and then every free slot, reading each attempt back with `body.GetItem(slot) == placed`
  (`Body.GetItem` at Body.cs:1346), and the unverified hand-off in `AddCarriedItemToLocalBody` was removed by the
  `MaterializeItem` split.
- **Major — the gate could not see the defect inside the rungs it added** (the reviewer put a destroy back into
  `AddRestoredItemToLocalBody` and a creating rung back into the drop path and the matcher still passed).
  **Changed:** it now reads the rung bodies and the `RestoredContent` factory, and its matcher contract carries
  seven shapes, including those two.
- **Major — the drop outcome had no acceptance row and no runtime trace, yet was stated as fact.** **Changed:**
  the outcome is withdrawn, so there is nothing to claim; the no-room case is a limit below.
- **Minors, each addressed:** the "nothing could be created on this client" branch loses the item with a
  Warning and the caller ignores the answer (recorded as a limit below); the codec's "NEVER destroys" wording
  was false for the componentless prefab (fixed); two stale restatements in this ticket (the 2026-10-18
  `RestoreContent` sentence and the "host-side refusal only reaches the host" bullet, both fixed — the second
  one is `review/intent-refusal-never-reaches-the-actor.md`, which reads its own row as passing); the status
  over-claimed row 3 (narrowed above); the checklist's format-evidence line had no exit code (the log now
  appends it), its "unfiltered" wording contradicted the excluded checklist gate (the filter is now named, and
  a separate unfiltered gate run proves the checklist), and its structure line did not say the touched test
  file sits outside the `src/` ceiling (it does now); the receiver's slot substitution was logged nowhere (a
  Warning now names the requested slot); the stale drop pin in this file was replaced by
  `ContainerDestination_TakesTheSlotFallbackInBothDirections`; the focused filter is named in the ticket and
  the checklist.

**What is still owed, and where it went:**

- **Row 4 itself is unrun against the fixed artifact.** The re-read is the next acceptance batch's first row,
  in the same shape as the rejection (the receiver's 3.501 g `plasticbag` refuses the giver's 4 g `toolbox`),
  and it reads four things: the receiver's log lines, the item alive in a slot with its id, no destroy trace
  for the id, and the host's kernel not storing it terminal.
- **The host's ledger still does not learn the receiver's own placement.** It keeps what the message said, so
  a refused container destination reads as "inside that container" on the host while the receiver placed the
  item in a slot — and the "no room at all" case is exactly the drop the host would have to re-record. Filed
  as `todo/placement-outcome-never-returns.md` with the readings it was found from.
- **The restore paths' discard policy** (the same destroy shape, one call site over) is
  `todo/restore-refused-child-keeps-its-id.md`.
- **Two limits this change leaves, named rather than buried.** An item that cannot be created on the receiving
  client at all (its prefab does not load there — mod content, differing builds) is lost with one Warning, and
  `PlayerInteractionApply` ignores the placement's answer; and a slot-destination transfer whose live body has
  no room takes the same loud loss. Both are the "no room at all" shape above, which the host has to answer.

