# Limb tool: working on a limb

Using a tool whose action is a limb action rather than a use action.

## What the player does

Brings a limb tool (a splint, a saw, a needle-like instrument) to a patient's wound view, selects the limb and applies it.
The player expects the tool's own limb action to run on that limb, with its own timed operation and its own cost.

## What the game itself does

A limb tool has no use action at all: its effect is a limb action, which the game runs against a limb of a body. That is
why the wound view's release — the entry that carries a limb — is its only entry.

## Rules and states

- A limb tool that arrives without a limb is refused, never resolved to the patient's most injured limb: the gesture must
  carry what the action needs.
- The limb the medical view carries is the one the action runs on.
- Some limb actions are timed operations; the patient's own client runs them and the operator's client shows the progress.
- **A bandage is applied by a CIRCULAR gesture, and the game counts it in 20-degree steps.** `BandageMinigame`
  (`BandageMinigame.cs:96-120`): while the bandage is held, the hand's angle is tracked, and when the angle passes 350° and
  comes back below 90° the game sets `didWrap = true`, plays the `bandage` sound and completes the wrap; every 20° of travel
  runs `DoBandageAction()` → `OnUse(0.055555556f)`, so the treatment progresses in visible increments rather than at release.
  The hand is also kept inside a 250-unit ring (`Vector2.Distance(handPos, Vector2.zero) < 250f`), so a treatment the player
  cannot physically perform is not a treatment.
- **A tourniquet stops the limb it is on and every limb DOWNSTREAM of it.** The game's own script (`TourniquetScript.cs`)
  sets `blockedBleeding = true` on the limb it is attached to and, recursively, on every limb whose `distanceToHeart` is
  greater (`GetLowerLimbs`), and a blocked limb contributes nothing to the body's total: `Body.cs` sums
  "`totalBleedSpeed += limb.bleedAmount * limb.bleedSpeedMult * (limb.blockedBleeding ? 0f : 1f)`". Taking it off clears the
  flags and hands the item back to the body with its own `condition` (`TakeOff()` instantiates the tourniquet and calls
  `AutoPickUpItem`).
- The patient must be alive; consciousness is not required.
- **The item only acts on a limb through four gates, and the fourth is its own flag.**
  `PlayerCamera.ApplyWoundItem` (`PlayerCamera.cs:739-760`) refuses a dismembered limb, refuses while the patient is below the
  medical cutoff with the `alerttoounhappy` line unless the definition sets `ignoreDepression`, requires `conscious`, and then
  requires `ActuallyUsableOnLimb(item)` before running `useLimbAction` — with `WaterContainerItem.ApplyToLimb(limb, 100f)` as the
  fallback for a container. A tool that is merely carried acts on nothing.

## What the affected side protects

Two protections guard this entry, and both are read on the machine: the picked limb is SERVED ("an `icepack`'s native limb action lands `ChilledLimb` on limb 2 on the treated side, and the operator's and third peer's displayed copies carry it"), and a request naming a limb the patient does not have is REFUSED by the affected side "with the item named, no limb touched and the item still carried". The refused shape is tested with an invalid index; a limb dismembered between the pick and the run was not staged and stays a gap.

## Refusals

| Condition | The player reads |
|---|---|
| a limb tool arrived without a limb | `use.refused.limb_tool_needs_medical_view` |
| that limb or slot is unavailable | `use.refused.limb_or_slot_unavailable` |
| the target is dead | `use.refused.target_dead` |
| the item is not the operator's | `use.refused.not_yours` |

## Who judges

The host admits the request with its limb; the patient's client runs the tool's own limb action on its own limb and
reports the result.

## What travels

The item's identity, the patient and the limb. The limb's resulting state and the item's condition travel back.

## What the player sees and hears

The tool is applied with the game's own animation and sound; a timed operation shows its progress on both clients, and the
limb's state afterwards is what the patient's own body says.

## Records

Decision 246. Accepted: `../../backlog/done/topical-live-stack-family-order.md` for the family ordering. Open:
`../../backlog/review/remote-medical-stage-2-shrapnel-multiplayer.md`.

## Gaps

- Which items are limb tools in the vanilla table, and the duration of each timed operation, is not tabulated here yet.
