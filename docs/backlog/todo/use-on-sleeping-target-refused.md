# An item cannot be used on a sleeping teammate

- Status: Todo — opened 2026-10-25 from the user's report (translated): "the guest is asleep — why can I not use items on them?".
- Priority: Medium
- Category: Cross-player item use / target gate
- Source: user report (2026-10-25). The measured refusal: `[ItemUse] refused: 76561198863287957 is unconscious and cannot …` and the
  operator's status line was the Chinese refusal frame (`hud.use_refused`) wrapped around an English sentence, while the
  guest's own body read `sleeping=True, conscious=False,
  consciousness=10, alive=True`.

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

## Open question (needs the user's word before code)

Should the consuming families accept a SLEEPING target, and if so should the game's own wake-on-use behaviour run (the target wakes
as it eats), or should the refusal stay and only its text be made specific (naming "the target is asleep" instead of "the target is
unconscious")?

## Acceptance rows

| Row | The check |
|---|---|
| 1 | The decided behaviour on a sleeping target, per family: eat, drink, wear, bandage, splint, injection — each read once with the target asleep |
| 2 | Whatever the refusal is, the operator's line names the actual state (asleep vs unconscious) in their own language |
| 3 | Applying a treatment to a sleeping teammate does not require waking them, and the patient's own client shows the state it applied |

## Limits

- The rule the ticket questions came from a recorded user ruling; changing it needs the user's decision recorded in
  `docs/decisions/active.md` before the code moves.
