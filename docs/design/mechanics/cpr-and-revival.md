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

## What the game itself does

There is no native CPR: the accepted record states that "Native `Assembly-CSharp` does not contain a native `CPRMinigame`", that a reference co-op mod ships custom `CPRMinigame` and `CPRHandler`, and that "this is therefore not a native parity item; it is an optional KrokMP-style enhancement". The native call this mechanic mirrors is the game's own revive, not a compression routine.

## Rules and states

- **The control has three states and appears only in arrest.** `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/CprButtonPolicy.cs` decides `Hidden` / `Start` / `Compress`: hidden with no remote focus, `Compress` when this client already runs a CPR operation on that body, and `Start` only when the patient is in arrest (`Body.inCardiacArrest`, `heartRate < 20`). The compression interval the client sends is never zero — the policy floors it at `0.01` s ("the update refuses a non-positive in…").
- **The rhythm's numbers are anchored on the game's own scales, never invented.** `CprRhythmPolicy.cs`: compressions act on BLOOD
  PRESSURE, ventilation acts on BLOOD OXYGEN, and the heart restart is a third outcome — "first to the host wins, the later
  claimant is corrected rather than replayed". The anchors are the game's: "`Body.brainDying` is
  `bloodPressure < 10 && consciousness < 5` (the arrest CPR is for), `Body.fibrillat[ion]` … `bloodPressure < 88`, and a normal
  pressure sits around 100-120", so a compression lifts pressure to `CompressionCeiling` only while the body is still in arrest
  territory and the restart needs enough perfusion (`RestartPressure…` is the tuning point the ticket records).
- **CPR and the AED cover different hearts, so whichever runs second refuses.** `AedShockPolicy.cs`: "a shock is only advised
  when the heart is fibrillating. That is what makes the shared restart honest — CPR restarts an ARRESTED heart, defibrillation
  a QUIVERING one, and whichever runs second finds its own condition gone and refuses instead of damaging a heart that no longer
  needs it". The real device says "no shock advised" and does not discharge, and the host arbitrates because it applies both
  operations to the same snapshot, where one changes the state the second is checked against.
- **The AED's own numbers: a 4.25 s charge check, battery drain, and a 20 % restart roll.** `AEDMinigame.cs:88-140`: after the
  pads are placed the device waits `4.25f` s and then either proceeds (state 2) or fails with the `aedfail` sound (state 5)
  — it fails only when the pads are on the CHEST and `fibrillationProgress` is already 0, which is the game's own "no shock
  advised"; it then charges for `3.5f` s and discharges, draining `0.14f` of battery. The discharge itself is
  `Item.Defibrillate` (`Item.cs:7146-7168`): every shock costs `skinHealth -= 5f` and `pain += 20f` and ragdolls the body; on
  the chest of a living patient it clears `fibrillationProgress` when the roll succeeds (the AED passes `chance = 1f`) or the
  body is in arrest, and **only an arrested heart restarts, on a further `0.2f` roll** (`heartRate = 200f`), after which the body
  is left with `defibShockedFrames = 20`, `heartProg = -1f` and `bloodPressure = 40f`.

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
