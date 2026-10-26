# An item cannot be used on a sleeping teammate

- Status: Review — implemented 2026-10-26; the per-family rows need a live session — opened 2026-10-25 from the user's report (translated): "the guest is asleep — why can I not use items on them?".
- Priority: Medium
- Category: Cross-player item use / target gate
- Source: user report (2026-10-25). The measured refusal: `[ItemUse] refused: 76561198863287957 is unconscious and cannot …` and the
  operator's status line was the Chinese refusal frame (`hud.use_refused`) wrapped around an English sentence, while the
  guest's own body read `sleeping=True, conscious=False,
  consciousness=10, alive=True`.

## Design

`docs/design/mechanics/item-use.md` is the page that owns the family gate this ticket changes: "the TARGET's gate is the family's", and the
decision-270 rule now sits under it. The decision itself is 270 in `docs/decisions/active.md`.

## Goal

Using an item on another player works whenever the game's own rules would allow it on that body — and the case the user expects to
work is a teammate who is asleep (a sleeping body can still be treated, and arguably can still be fed or given a drink).

## Current behaviour

`PlayerItemUseExecution` gates the target in two steps: a body must be alive to receive anything, and **only the two families where the
TARGET consumes the item (drink, eat) additionally require a conscious target**. Bandaging, splinting, injecting and topicals are
deliberately allowed on an unconscious teammate — the comment records the user's 2026-10-24 finding that the native gate it was copied
from tests the LOCAL body.

So the refusal the user hit is the rule they asked for on 2026-10-24, applied to a CONSUMING item. The open question this ticket
carries is whether a sleeping (as opposed to unconscious-from-injury) teammate should be exempt:

- a sleeping body is `conscious=False` with a low `consciousness`, and the same refusal fires;
- feeding a sleeping teammate is a plausible expectation (the item is applied BY the operator);
- drinking requires the target's own swallow, which a sleeping body cannot do.

## Open question — DECIDED 2026-10-25 (decision 270)

The user's ruling, given after this ticket was written: **a sleeping teammate is a valid target for every cross-player item use, and the operation
runs the native chain on the target's own client.** Recorded as decision 270 in `docs/decisions/active.md`, with the boundary the ticket needed:

- being ASLEEP stops being a refusal condition for the consuming families (eat, drink), as it never was for the treating ones;
- being UNCONSCIOUS FROM INJURY keeps the 2026-10-24 rule for consuming items — the exemption is sleep, not unconsciousness;
- the judging split does not move: the target's client performs the native call on its own body (its animation, its sound, its state) and reports
  the result, so a wake-on-use the game performs happens there because that is where the call runs — CUO invents nothing;
- whatever refusal remains names which state it refused (asleep vs unconscious) in the reader's own language.

What is left is the code and its rows.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The decided behaviour on a sleeping target, per family: eat, drink, wear, bandage, splint, injection — each read once with the target asleep |
| 2 | Whatever the refusal is, the operator's line names the actual state (asleep vs unconscious) in their own language |
| 3 | Applying a treatment to a sleeping teammate does not require waking them, and the patient's own client shows the state it applied |

## Limits

- The rule the ticket questions came from a recorded user ruling; changing it needs the user's decision recorded in
  `docs/decisions/active.md` before the code moves.

## Implemented (2026-10-26)

Decision 270's code, in three places:

1. **The fact is reported.** `CharacterHealthMsg.Sleeping` (ProtoMember 82) with the body's own `Body.sleeping` captured by
   `RemoteCharacterDisplayProjection.Capture` — the host cannot read a remote body's sleep state any other way, and every other field of the snapshot
   reads identically for asleep and unconscious.
2. **The gate exempts sleep only.** `PlayerItemUseExecution` refuses the consuming families on `!Conscious && !Sleeping`, logs the exemption when it
   applies (`{Target} is asleep — the {Family} proceeds (decision 270: sleep is not a refusal condition)`), and keeps the
   `refusal.patient_is_unconscious` line for the injury case, which is what the remaining refusal names.
3. **The chain does not move.** The call still runs on the target's own client, so a wake-on-use the game performs happens there.

Tested at the gate: `Use_ADrinkOnASleepingTarget_IsAllowed` (26/26 in `ItemUseTests`) sits beside the existing
`Use_ADrinkOnAnUnconsciousTarget_IsRefused`, so the pair differs only in the sleep flag. Written back on `mechanics/item-use.md` and audit ledger
row 186.

**Wire note**: this adds a protobuf field. Before the first release the protocol version does not move (decision 241); the change is recorded here and
in its commit.