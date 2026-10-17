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
- **A refusal by the container's own rules is now answered instead of losing the item.** `RestoreContent` returns
  the item it materialized (or null), and it checks `Container.CanHoldItem` BEFORE loading, because the native
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
  one: answer it on the wire (`CommandRejected`) and print it where the player acted.
- **Row 2 is still unrun on the machine.** The code is there and the unit-level red went green, but "an item
  released onto an item shown in the receiver's inventory enters that container and stays put" needs the live
  reading, and so does its refusal shape (a container that cannot hold it falls back to a slot, observably).
- **The same-owner container branch in `PlayerInteractionApply` is dead code** — it requires `From == To`, and the
  only publisher refuses that case, so it and its "refused by the native container" log can never run. Recorded
  rather than deleted here because deleting it moves a behaviour the family may still want to route through.

