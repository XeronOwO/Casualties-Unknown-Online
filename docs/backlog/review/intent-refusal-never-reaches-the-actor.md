# A refused player-interaction intent is silent to the player who performed it

- Status: Review — both answering sites landed and row 2 is read on the machine; the remaining rows are readings, not code
- Priority: Medium
- Category: Remote inventory / player interaction / observability of refusals
- Source: the give ticket's acceptance readings (2026-10-18). Row 3 and row 6 both refused correctly, and both
  showed the same shape from the actor's side: their client had already written a POSITIVE line when it sent the
  intent, and nothing ever answered. Filed as its own ticket because it is not one message's gap — every kind in
  the vocabulary answers in the same way, and the family's rule
  (`AGENTS.md`, "Member reports are bounded, or the gap is named") says a refusal must be answered so both sides
  end up agreeing.
- Related: `review/give-item-to-another-player.md` (where it was found; its readings show the actor's screen),
  `done/remote-inventory-native-parity-rework.md` (the family this applies to), `docs/architecture/member-report-validation.md`
  (the census that asks, per wire member, whether a report is checked and whether the check is answered)

## What is observed

Two live readings, both on the deployed artifact, both with the actor watching:

- **A policy refusal never reaches the actor.** With the host's `AllowRemoteInventoryTake` off (the rule
  is now `RemoteBackpackAccess=deny`; this reading predates the rename), the giver's release
  is refused on its OWN side — `[RemoteIntent] refused GiveToBody: host has disabled remote inventory manipulation`
  — and the player sees nothing: the item does not move, and no line appears on their screen.
- **A host-side refusal never reaches the actor either.** With the receiver out of world, the host refuses by name
  (`[RemoteIntent] refused GiveToBody: … or the destination body <receiver> is not in-world`) and the giver's
  client is never told. Its last word was the positive capture line it wrote when it sent.

The same is true of the worn-item refusal, the "no such container" refusal, and every other host-side "no" in
`PlayerInventoryTakeService`: each is a `LogWarning` on the host, and none travels.

## Why it matters

The player's screen is the only place a refusal can be understood. Today the actor's experience of every refused
gesture is identical to a gesture that did nothing because of a bug: the item stays put and no explanation appears.
That is also why the project's own rule reads the way it does — a report the receiving side contradicts has to be
answered, or the two sides do not "end up agreeing"; they end up with one side believing it acted.

## What a fix has to decide

1. **What carries the answer.** The family already answers with RESULT messages
   (`PlayerInventoryTransferMsg`, `PlayerHealResultMsg`, `PlayerItemUseResultMsg`), while the kernel's own channel
   has `CommandRejected` + `RejectionReason`. A refusal here has to answer a `RemoteInventoryIntentMsg`, which
   neither shape fits directly: the honest choice is a small result message whose payload names the intent kind and
   the refusal, and it lands on the actor's own client as a line (and, where a presentation exists, the native
   feedback the gesture would have produced).
2. **Where the refusal is authored.** Every refusal in `HandleTakeCore` (and the sibling handlers) already has a
   human-readable reason string. The answer should carry that same reason rather than a second wording, or the two
   will drift.
3. **Rate and shape.** One refusal per gesture, never per frame, and never for a refusal the ACTOR's own client
   already made (that one is printed locally and needs no round trip).

## Non-goals

- Not the intent vocabulary itself: no new kind, no new operand.
- Not the host's authority: this ticket adds no validation, only the answer to a validation that already happens.
- Not the give feature (`review/give-item-to-another-player.md`), which its own ticket judges.

## Acceptance (the rows a run must read)

| # | Scenario | Expected |
|---|---|---|
| 1 | The actor's gesture is refused on the ACTOR's side (a policy the host announced) | One line on the actor's screen naming the refusal; nothing moves; no round trip |
| 2 | The actor's gesture is refused on the HOST's side (receiver out of world) | One line on the actor's screen naming the refusal, within the same second, and nothing moves |
| 3 | The host's refusal of a genuinely malformed request | The actor is told, and the host's log keeps its own line |
| 4 | A gesture the host ACCEPTS | No refusal line anywhere; the actor sees the accepted outcome only |

## What landed first, and what it does not cover yet (2026-10-18)

The intent SERVICE's own refusals are answered: the host's policy, an end not in world, a missing character
snapshot, an item the owner does not carry, an invalid operand, and a first-writer-wins loss all travel back as
`NetMsg.InventoryIntentRefused` (145) with the host's own reason sentence, and the actor's client prints it at
Warning. Pinned by `ARefusedIntent_IsAnsweredToTheClientThatPerformedIt`, whose red was observed by sending the
answer under a wrong discriminator.

**Not covered yet, and it is the layer below:** the refusals inside `PlayerInventoryTakeService.HandleTakeCore` —
the item is worn, the receiver carries no such container, the receiver has no empty inventory slot, the transfer is
malformed — still answer only the host's log. The clean way to cover them is the shape this layer already has: make
`HandleRemoteGiveToBody` return the refusal reason (null when it applied) and have the caller in
`PlayerRemoteInventoryIntentService` send it through the same `Refuse`. That keeps ONE sender and one wording.

**So row 2 of the table above is not readable yet.** Driving it needs a host-side refusal of a guest's intent, and
the two candidates available with two clients both trip earlier: "give to oneself" is refused at the release seam
on the actor's own client (the seam will not name the actor as the target), and taking the destination out of world
means taking the host out, which ends the run. The container-the-receiver-does-not-carry case is the drivable one —
and it is exactly one of the refusals listed above as not yet answered. **Cover those, and row 2 becomes drivable
without any new lever.**

### Row 2, READ — and the first reading of it was wrong (2026-10-18)

The take core now answers, so the drivable case ran on the deployed artifact: the giver released its own item at a
container id the receiver does not carry. The host refused as designed, and **the giver's own client printed the
answer**:

`[Warning] [InventoryIntentRefusedHandler] [RemoteIntent] the host refused 16 on item 5197989525: <receiver> carries
no item 424242 …`

**The first reading of this run reported the opposite**, and the mistake is worth keeping because it is a reading
error, not a code one: the search was made in the host's `BepInEx/LogOutput.log`, where the line does not exist
because the two clients do **not** share one log — the member runs inside a sandbox and writes to the sandbox root's
own copy of the game directory. Every "the actor sees nothing" conclusion taken from the host's log is therefore a
false negative, and this one was recorded as a defect before the guest's log was read.

**So row 2 PASSES.** The remaining rows are readings, not code: row 1 (a refusal the actor's own client makes —
printed locally, no round trip) and row 4 (an ACCEPTED gesture prints no refusal line anywhere).
