# The game's medical system (1.0)

Every rule below is cited from the 1.0 decompile (`reversing/release-1.0/Assembly-CSharp/`, not in git). The area the
page does not cover is named at the end.

## The body is eleven limbs

`Body.limbs` holds the limbs; each `Limb` carries `skinHealth`, `muscleHealth`, `blood`, `bleedAmount`, `pain`,
`infectionAmount`, `dismembered`, `dislocation`, a `TourniquetScript`, a `SplintLimb` and `prostheticInfo`
(`Limb.cs:805` skin, `:809` muscle, `:825` infected, `:829` infection amount, `:833` pain, `:840` blood, `:948`
dismembered). The body above them carries blood volume/oxygen/pressure, brain health, consciousness, temperature,
hunger, thirst, sickness, radiation sickness, septic shock and trauma (`Body.cs:171-342`).

## Damage and its three states

- A limb's `skinHealth` and `muscleHealth` are the two HP bars the game shows; a prosthetic replaces the skin bar with
  muscle (`WoundView.cs:389`).
- Bleeding: every unblocked limb contributes `bleedAmount * bleedSpeedMult` to `totalBleedSpeed` (`Body.cs:3103`); a
  tourniquet sets `blockedBleeding`, which removes that limb's contribution.
- Pain: `averagePain` is the maximum over limbs of `pain - adrenaline * 0.5` (`Body.cs:3104`); adrenaline is a
  painkiller, not a heal.
- Infection: the per-limb `infectionAmount` sums into the body's septic shock, which rises by
  `sum * 0.00028 * infectionspeed` per second (`Body.cs:3130`); a limb's `disinfectionTime` is the disinfection window
  the wound window shows as a green ring (`WoundView.cs:468`, `:518`). A limb whose infection is present is marked in
  the health panel (`PlayerCamera.showInfection[limbIndex] = true`, `Item.cs:1761`).
- A limb can be dismembered (`Limb.dismembered`, `Limb.cs:948`); the saw's `DoAmputate` only starts the minigame for a
  limb whose index is above 2 (the head, torso and abdomen are not amputable) and otherwise runs the saw minigame
  (`Item.cs:7949-7965`). 1.0 has no infection threshold in this entry; the demo's "infection above 60" gate is gone.

## Consciousness, dying and death

- `alive` is `brainHealth > 0` (`Body.cs:211`); `conscious` is `alive && consciousness > 30` (`Body.cs:221`).
- "Dying" (the game's own flag) is true for heavy bleeding, low blood, not breathing, hunger/thirst below 10, a septic
  shock above 75, out-of-range temperature, heavy radiation, fibrillation, out-of-range pressure/oxygen, an embolism or
  a stroke (`Body.cs:332`).
- Death is the harsher set: bleeding with blood under 30, oxygen under 50, starvation with muscle under 25, septic
  shock above 82.5, temperature under 27 or above 41.5, fibrillation above 60, cardiac arrest, pressure under 70
  (`Body.cs:342`).
- Brain damage accrues from low oxygen (`Body.cs:933`) and at 1.5 per second while the body is in the dying band
  (`:937`); the brain starts at 75-90 (`:1012`). Revival through the game's own path restores blood to at least 50
  (`:1017`).

## Treatments the player performs

- Bandages stop bleeding; splints fix dislocation; tourniquets block a limb's bleeding; disinfectants start a limb's
  disinfection window (`Item.cs:504`, `:575` set `disinfectionTime`); painkillers raise the pain ceiling, not the health.
- Amputation runs the saw minigame (`AmputationMinigame.cs`): the cut window moves from +120 to -380 as the cut
  progresses (`:40`), the progress grows with the hand's horizontal speed (`:110`), and a completed cut sets the limb's
  infection to zero (`:81`).
- Prosthetics: a limb with `prostheticInfo.hasProsthetic` is repaired through `TryProsthesisRepair` and its own
  minigame (`Item.cs:7967`, `ProsthesisRepairMinigame.cs`); a prosthetic limb is amputated instantly instead of cut
  (`Item.cs:7958-7961`).
- Defibrillation: `AEDMinigame` and `ManualDefibMinigame` own the shock rhythm; the manual path is a two-operator
  operation in the release.
- The wound window is the game's own surface for all of this: hovering a limb, the treatment buttons, the nap button
  (`WoundView.cs:178`), the "remove tourniquet/splint" special button (`:542-560`).

## What the mod must respect (not the mod's design)

- Injury is judged by the client that experiences it; the host keeps the world state and the arbitration between
  conflicts (decision 184). A wound action's native call happens on the patient's own client; the host records it.
- A limb's state crosses the wire as the limb's own fields; a report that the game's own rates cannot produce in one
  event is refused by the host (the limb plausibility bound, 2026-11-08).

## Not covered here

- The exact drug list and their per-item effects (adrenaline, opiates, antibiotics, painkillers): the catalogue lives
  in `Item.cs`'s item table; this page reads the body's rules, not every item's numbers.
- The wound window's icon layout and the health panel's layout: `docs/design/surfaces/` covers the mod's surfaces;
  the game's own window geometry is not mapped yet.
- Multiplayer-specific injury timing (who sees the wound first, how the host orders two simultaneous treatments):
  covered by the mod's design pages, not by the game.
