# CPR and revival

Compressing a patient's chest to restart their heart, and giving them air.

## What the player does

Opens a patient's medical chart, presses the CPR control on that patient, keeps compressing in a steady rhythm, and
inflates after an interval to give the teammate air. The player expects each press to cost their own stamina, the patient
to come back when the compressions succeed, and a poor rhythm to be visible to them.

## Where the design comes from

CPR is **CUO's own enhancement**, not a native action. The accepted ticket states the fact plainly: "Native
`Assembly-CSharp` does not contain a native `CPRMinigame`", a reference co-op mod "ships custom `CPRMinigame` and
`CPRHandler`", and "this is therefore not a native parity item; it is an optional KrokMP-style enhancement". The user
promoted it on 2026-10-07 with the mechanics they wanted added: "inflate after an interval to give the teammate air, an
oxygen-style mechanic if the game's own model can carry one, and an explicit complexity assessment".

The reference mod's handler "already carries most of what the user asks for as a reference: a strength/intelligence gate on
the healer, a press cadence with a stamina cost per press, a hand-force term that scales the push, and rib damage as a side
effect of over-pressing. It does **not** show a ventilation/inflation step or an oxygen model, so those two are CUO's own
design."

## Rules and states

- The operator's own body is asked FIRST: a strength and intelligence gate decides whether they may compress at all, and a
  press they cannot make is refused on their own client before anything leaves it.
- Each press costs stamina; the hand's own force scales the push, so a stronger operator pushes harder.
- Over-pressing damages the patient's ribs as a side effect.
- The compression has a rhythm: presses spaced by the game's own clock are judged, and the verdict is `Good` when the
  spacing is right — the accepted record's reading is "five consecutive 0.55–0.56 s gaps each produced `verdict Good at INT
  15`", while "the same presses at intelligence 7 produced `verdict None`, which is the intelligence screen working as
  designed".
- Ventilation gives the patient air after an interval; the oxygen model is CUO's own, because the game has none.
- AED is a separate procedure with its own item and rhythm, and is explicitly out of this mechanic's scope.

## Refusals

| Condition | The player reads |
|---|---|
| the operator is too weak to compress | `medical.cpr_too_weak` |
| the operator has no stamina for the press | `medical.cpr_exhausted` |
| the target is dead | `use.refused.target_dead` |
| no patient selected | `use.refused.no_patient_selected` |

## Who judges

The operator's own client refuses a press their body cannot make. The patient's side carries the outcome of the
compressions, and the acting side judges on its own picture and timeline — the accepted ticket names that rule when it
lists the decision register among its records.

## What travels

The patient, the operation the compressions belong to, and each press as that operation's input. The patient's revived
state is what the host applies.

## What the player sees and hears

The compression plays on the patient's body with the game's own feedback, the rhythm verdict is shown to the operator, and
a refusal is one line in the operator's language plus the game's deny sound.

## Records

Accepted: `../../backlog/done/remote-medical-cpr.md` — all four stages landed ("stage 1's restart, stage 2's oxygen
channel, stage 3's rib and AED arbitration, stage 4's reference rhythm band, per-press costs, strength gate and intelligence
screen"), with the rhythm verdict read in batch `20261025-a`. Parent: `../../backlog/review/remote-medical-native-minigame-parity.md`.

## Gaps

- The number of presses a revival takes, and the stamina cost per press, are CUO's own numbers; they are held by the
  implementation and its acceptance records rather than stated here yet.
