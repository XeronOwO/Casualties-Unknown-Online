# Medical view: treating another player's body

Opening a teammate's wound view, choosing a limb, and applying treatment to it.

## What the player does

Opens a teammate's medical view from the member menu, sees that body's limbs and their condition, picks a limb, and
releases a treatment over it — a bandage, a splint, a topical, a limb tool, or an injection. The player expects the
treatment to land on the limb they picked, with the game's own medical animation, sound and outcome, and to cost what
that treatment costs.

## What the game itself does

The wound view's release is the native call site for the wound actions: the game's own limb action runs against the
selected limb of the viewed body. An injection is not a wound action: it runs the game's own injection path through a
medical operation the two clients perform together, which is why the drag gesture refuses it.

## Rules and states

- **Which family needs a responsive patient is the FAMILY's rule, and the code states it once**:
  `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/MedicalTargetBodyValidator.cs` requires a responsive target
  only for `Injection` and `Shrapnel` ("a responsive patient is per family, because the rule is the family's"), refuses a
  dead body for every family, and notes that relocation, defibrillation and amputation are exactly what an unresponsive body
  needs. `ItemUseTargetPolicy.cs` mirrors it for the item families: only `Drink` and `SolidFood` need a conscious target, and
  no item family is the defibrillator.

- The view belongs to the VIEWED body: the panel shows the patient's limbs, not the viewer's, and its identity header
  carries the VIEWED player's data. The accepted record names the trap and the fix: "nothing rewrites it afterwards —
  `WoundView.UpdateView` never touches the two labels — so a remote medical focus that does not take the header over keeps
  the VIEWER's identity over the viewed player's body"; the user's ruling is that the header shows the viewed player's own
  data.
- The local-only controls are hidden while a remote focus is open — "the nap control (`WoundView.napbutton` with its
  `sleepImage` state icon), the workout list (`WoundView.workoutList`) and the HUD main/off-hand switch control
  (`PlayerCamera.handSwapImage`)" — because "a remote medical focus is a READ-ONLY view of another player's body".
- The selected limb is the one every wound action applies to; an action that names no limb is refused rather than
  applied to a guess.
- A topical applies a measured dose to the selected limb; a limb tool runs the item's own limb action; a bandage or
  splint starts the item's own minigame.
- **A removal takes the component OFF the body.** Removing a limb component is not only a flag: the component itself must be
  taken off the target's own body on every surface, which is the defect the record closes ("A removed limb component is
  never taken off the target's own body", fixed in `7c5646bd` and accepted with rows 1-6).
- **A state that names no kind is refused, not guessed.** An operation state whose kind is missing must not be read as an
  injection: the recorded defect is "A medical operation state that names no kind is read as an injection", and the rule it
  leaves is that a state carries its kind explicitly and an unnamed one is refused by name.
- **The numbers the panel shows are the game's own scale, and its "badly off" answer is one predicate.**
  `Body.cs:3872-4161` starts a body at `bloodVolume = 100f`, `hunger = 100f`, `thirst = 100f`, `energy = 100f` and
  `consciousness = 100f` — one 0-100 scale for all five, with `happiness`, `sicknessAmount`, `radiationSickness`, `painShock`
  and `temperature` beside them. The game answers "how badly off is this body" with `isCriticallyDying`
  (`Body.cs:330-340`): "`(totalBleedSpeed > 0.02f && bloodVolume < 30f) || bloodOxygen < 50f || (hunger < 0f &&
  limbs[0].muscleHealth < 25f) || septicShock > 82.5f || temperature < 27f || temperature > 41.5f || fibrillationProgress >
  60f || inCardiacArrest || bloodPressure < 70f`" — and with a separate, earlier `isDying` (`Body.cs:320-330`) that judges
  bleeding out by RATE first and volume second (`totalBleedSpeed > 0.134f || (totalBleedSpeed > 0.02f && bloodVolume < 40f) ||
  !breathing || …`). A CUO panel that invents its own thresholds is a different game.
- **A terminal fact is a named latch, per limb and per body.**
  `src/CasualtiesUnknownOnline.Protocol/Wire/WirePlayerLimbState.cs` carries `Broken`, `Dismembered` and `Dislocated` for one
  limb, and `WirePlayerBodyTerminalState.cs` carries `Disfigured`, `EyeGone`, `BothEyesGone` and `HasPulmonaryEmbolism` for
  the body — so a latch the game sets is a field on the wire, never a value a reader re-derives.
- A dead body cannot be treated; a treatment applied to a body that dies mid-action is resolved by the affected side.

## Refusals

| Condition | The player reads |
|---|---|
| the item cannot be applied to a wound | `use.refused.cannot_apply_to_wound` |
| the item is not the operator's | `use.refused.not_yours` |
| no patient is selected | `use.refused.no_patient_selected` |
| the item has no identity | `use.refused.no_network_identity` |
| an injectable was dragged instead of opened | `use.refused.injectable_needs_medical_view` |
| a topical arrived without the wound view's limb | `use.refused.topical_needs_wound_view` |
| a limb tool arrived without a limb | `use.refused.limb_tool_needs_medical_view` |
| the named limb or slot is unavailable | `use.refused.limb_or_slot_unavailable` |

## Who judges

The host validates the request. The treatment's effect belongs to the patient's body, so the patient's own client runs
the game's own action on its own limb; the operator's client shows the gesture and the item's departure. An injection's
operation is performed by both clients together, with the patient's side producing the outcome.

## What travels

The patient, the item's identity and the selected limb; for a measured dose, the dose. The outcome of the treatment is
reported by the patient's side and lands in the item's own state.

## What the player sees and hears

The wound view shows the patient's wounds and the item being applied, with the game's own minigame for a bandage or
splint and the game's own sounds; the patient sees their own body react to the treatment.

## Records

`../../architecture/surfaces.md` (medical view row), decisions 184 and 246. Accepted:
`../../backlog/done/remote-player-medical-panel.md`, `done/remote-medical-panel-alarm-follows-viewed-player.md`,
`done/remote-medical-panel-header-shows-viewer-identity.md`, `done/remote-medical-panel-hide-local-only-actions.md`,
`done/remote-fentanyl-injection-and-medical-panel-desync.md`.

## Gaps

- The exact per-treatment costs and thresholds (what a bandage, splint, topical or injection costs the patient and the
  operator) are not yet recorded here; the game's own tables hold them.
- The shrapnel multi-injury flow's multiplayer behaviour has open acceptance rows; recorded in the review block of
  `../../backlog/`.
