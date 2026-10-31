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
| 1 | The decided behaviour on a sleeping target, per family: eat, drink, wear, bandage, splint, injection — each read once with the target asleep | **PARTIAL PASS 2026-10-27** (deployed): EAT — the host logs `<guest> is asleep — the SolidFood proceeds (decision 270 …)`, the guest's own client runs the game's own eat (`ran the game's own eat of dogfood … out of the giver's hand`), its hunger rose 75.74 → 81.53 and it stayed asleep. DRINK — the gate passes the same way (`<guest> is asleep — the Drink proceeds`), and the refusal that followed was the container's own rule (`carried no dose to drink`), not sleep. **Wear, bandage, splint and injection are RESIDUALS**: each needs an item and a gesture the preset does not hand the batch (the treatment families go through the wound view's limb release) |
| 2 | Whatever the refusal is, the operator's line names the actual state (asleep vs unconscious) in their own language | **PARTIAL PASS 2026-10-27**: the operator-side line names the state exactly — `<id> is asleep — the SolidFood proceeds` / `the Drink proceeds` — and it is a keyed sentence; the HUD frame that wraps it was NOT read this cycle (no frame taken), so its language half stays a residual |
| 3 | Applying a treatment to a sleeping teammate does not require waking them, and the patient's own client shows the state it applied | **RESIDUAL**: the treatment families reach the patient through the wound view's limb release, which this batch cannot drive; the eating family is the one judged above, and it did not wake the patient |

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

## The fact path is judged on the machine (2026-10-26 20:44, the paradise batch)

The precondition of decision 270 — that the host can TELL asleep from unconscious, from the target's own client — was read on the deployed build:

1. the guest napped through the game's own entry (`body-call kind=nap` → `Body.TakeANap`), and its own body then read
   `conscious=False sleeping=True consciousness=10`;
2. the host's stored snapshot of that guest read the same: `alive=True conscious=False sleeping=True consciousness=10`.

So `CharacterHealthMsg.Sleeping` round-trips through the wire and the host holds it. `health-read` gained the `sleeping` field for both sources so
this is re-readable in any batch.

**Still owed**: rows 1-3 (an actual cross-player USE on the sleeping target, per family) — no in-process driver sends a use request whose target is
another player yet, so those rows need either that driver or a hand on the mouse.

## The eat family on a sleeping target is judged (2026-10-26 20:45, clean re-test)

The first attempt was ambiguous — the target woke before the use, so no exemption fired — and the clean re-test settles it:

1. the guest napped again (`body-call kind=nap`) and its own body read `conscious=False sleeping=True consciousness=10` immediately before;
2. the alt sent one cross-player use at it through the product''s own sender (new recipe `use-request`, which calls
   `IPlayerInteractionControl.SendUseRequest` by reflection);
3. the host''s gate logged the exemption this ticket added, in the same second:

```
20:45:09.472 [ItemUse] … is asleep — the SolidFood proceeds (decision 270: sleep is not a refusal condition).
20:45:09.472 [ItemUse] … offers dogfood (id 14451443822) to …
20:45:09.513 [ItemUse] … ate dogfood (id 14451443822) of …
```

4. after the feed the target read `conscious=False sleeping=False hunger 102.95 → 107.35`: the food was consumed, and the game''s own eat ended the
   SLEEP state without restoring consciousness — CUO invented none of that, the eat runs on the eater''s own client.

**Judged**: row 1 for the eat family (a sleeping target is a valid target and the consuming gate exempts sleep). **Owed**: row 1''s other families
(drink, wear, bandage, splint, injection) and rows 2-3; `use-request` now makes those drivable, one item id at a time.

## The drink family too, and where this driver stops (2026-10-26 20:46)

With the target's energy forced to 0 (`body-force energy=0`) so the sleep holds, the nap sticks (`conscious=False sleeping=True energy 0.96`) and the
alt drank at it:

```
20:46:49.939 [ItemUse] … is asleep — the Drink proceeds (decision 270: sleep is not a refusal condition).
20:46:49.941 [ItemUse] … used waterbottle (id 23041378414) on …
```

**Judged**: row 1 for eat and for drink — the two CONSUMING families, i.e. exactly the ones decision 270 exempted.

**Where `use-request` stops**: a bandage through the same request is refused by name — "analgesicgauze … is empty or is not a family this [gesture]
carries" — because the medical families are decided by the DRAG GESTURE (the wound view's own cast), not by a bare request. So wear/bandage/splint/
injection still need the gesture path, and rows 2-3 with them; the consuming pair is what this driver can judge.

## How the batch got a sleeping body (2026-10-27)

`Body.TakeANap` would not start on demand — its own `canTakeNap` gate stayed closed with `energy = 0` and an empty room — so the batch could not reach a
sleeping target at all until the driver grew the field. `tools/acceptance/recipes/body-force.cs` now takes `sleep=` and writes the game's own
`Body.sleeping` directly (1 sleeps, 0 wakes, -1 leaves it alone), which put the guest at `sleeping=True, conscious=False, consciousness=10` — the exact
state the ticket's source report described — and made the rows above judgeable.

## The asleep fixture, first reading (2026-11-01)

The medication probe plants the sleeping-pills component on the LOCAL body (`medication-component-probe mode=plant-sleepingpills,target=local`) and answers `ok=true, amount=300`, but its own report field `sleepingPills` comes back **false** and `body-read` three seconds later still says `sleeping=false, conscious=true`. So planting the component is not the same as the body being asleep: the game advances a medication effect on its own ticks, and the body's sleep state is that effect's result, not the component's presence.

Row 3 is the one this ticket can still close with instruments that exist — the bandage chain is proven end to end (a wounded limb treated through the remote chart, `terminal Completed`, the patient's own client applying it), so what it needs is exactly the sleeping state this note is chasing. The next reading is the component's effect over time (`medication-component-probe mode=read` after longer waits, and whether the body's sleep is gated on the day/night cycle).

## The effect takes (the component reports true) and the body still does not sleep (2026-11-01)

A later `medication-component-probe mode=read,target=local` answers **`sleepingPills=True`** — so the planted effect did become active, and the earlier `false` was simply time: the game advances medication on its own ticks. The same moment's `body-read` still says `sleeping=false, conscious=true`.

So "asleep" is not a state the drug sets by itself: the body enters sleep through the game's own resting path (a bed and the rest action), and the pills are one of its inputs. The fixture this row needs is therefore that resting path, not a stronger dose — the next reading is what the game requires for a body to enter sleep (the bed/night conditions in `reversing/`), and then the bandage chain on that sleeping body judges row 3.

## Row 3 READ on the deployed build (2026-11-01): a sleeping patient is treated without waking

The path to "asleep" is now known and it is the game's own: `Body.canTakeNap` is true when `energy < 35 && averagePain < 31 && sicknessAmount < 80`, OR the body carries the `SleepingPills` component (`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs`, the `canTakeNap` getter), and the body enters sleep through the nap coroutine that sets `sleeping = true` after its lay-down animation. So: plant the pills (`medication-component-probe mode=plant-sleepingpills,target=local` — the effect activates on the game's own ticks, which is why an immediate `read` said false), then `body-call kind=nap` (whose own note says it "starts only when canTakeNap holds"). The guest read **`sleeping=true, conscious=false`**.

With the patient asleep, the proven remote chart handled a bandage on a freshly wounded limb 4 (`skinHealth=63.04`): release on limb 4, the game's own minigame up, ended through its exit tip, and afterwards

- the patient was **still `sleeping=true`** — the treatment did not wake them;
- the limb read **from the patient's own client** moved (`skinHealth` 63.04 → 63.841, bleeding 1.872) — the state the treatment applied is visible on their side.

That is row 3 of this ticket, read end to end on the deployed artifact. Rows 1 and 2 keep their earlier partial passes (each family read once with the target asleep, and the operator-side line naming the actual state).

## The splint family needs a free slot (2026-11-01)

The same batch tried the LIMB-TOOL family on the same sleeping patient — the one treatment family left that does not need the drag: give a `splint`, open the chart, release it on limb 4. The give answered "packed into its carried container trashbag" (every slot was taken by the evening's earlier items), and the release then found nothing to drag (`limbLookingAt` empty). So the family is one free slot away, not one instrument away — the same rule the medical batch already learned, now confirmed for this ticket: **one treatment per fresh launch**. The next batch starts clean, re-naps the patient (the pills make `canTakeNap` hold, so a re-nap is one call), and reads the splint the way the bandage was read.

## The SPLINT family on a sleeping patient (2026-11-01)

Clean session, pills + `body-call kind=nap` (`sleeping=true`), a fresh wound on limb 4 (`skinHealth=63.04`) and a splint given into slot 0. The release went through the chart and the host's own lines carry it:

    [ItemUse] <operator> uses splint (id …) on <patient>
    [MedicalView] requested use of <patient> limb 4 with splint (id …)
    [ItemUse] <patient> installed splint (id …)

and the patient, read on their own client, carries the component (**`SplintLimb`**) and is **still `sleeping=true`**. So the second treatment family reads on a sleeping target without waking them, the same way the bandage did.

The families left on row 1 are the ones that go through the give gesture (eat, drink, wear) and the injection (which needs a liquid-filled container); bandage and splint are read.