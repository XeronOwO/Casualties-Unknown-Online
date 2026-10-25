# Medical panel: a patient's body on the operator's screen

The panel that shows a teammate's wounds and offers the treatments.

## What the player does

Opens a teammate's medical panel, looks at their limbs and wounds, selects a limb, and applies a treatment. The player
expects the panel to be the PATIENT's body — not the viewer's — and the actions it offers to be the ones that apply to that
patient right now.

## What it is

A panel showing the viewed body's limbs, their wounds and their condition, with the treat actions the game offers for the
selected limb, the CPR control while the patient needs it, and a header naming the patient.

## Rules and states

- The panel belongs to the viewed body: it shows the patient's limbs and values, and the header names the patient.
- A viewer's own state never appears in the panel except as a reason an action is refused (the operator's own strength for
  CPR, the operator's own stamina).
- The selected limb is the one every wound action applies to; the panel refuses an action that would need a limb it does
  not have selected.
- Actions that exist only locally (an action only the patient could take) are not offered.
- The panel stays on its patient: when the patient leaves the world or dies, the panel follows the state it is given
  rather than continuing to act.

## Refusals

| Condition | The player reads |
|---|---|
| no patient selected | `use.refused.no_patient_selected` |
| the treatment cannot be applied to a wound | `use.refused.cannot_apply_to_wound` |
| the item is not the operator's | `use.refused.not_yours` |
| the operator cannot compress | `medical.cpr_too_weak`, `medical.cpr_exhausted` |

## Who judges

The panel draws what the patient's state says. Each treatment is judged by its own family's rules and executed by the
patient's client; the panel itself decides only what to offer.

## What travels

The patient's limb and wound state, as part of the character data every client holds; a treatment travels as its own
request.

## What the player sees and hears

The patient's limbs with their wounds, the offered treatments, and the game's own feedback for each treatment as it lands.

## Records

Accepted: `../../backlog/done/remote-player-medical-panel.md`,
`done/remote-medical-panel-alarm-follows-viewed-player.md`, `done/remote-medical-panel-header-shows-viewer-identity.md`,
`done/remote-medical-panel-hide-local-only-actions.md`,
`done/remote-medical-panel-acceptance-issues.md`.

## Gaps

- The panel's exact layout (which limb list, which values, which order) is not recorded here yet; the acceptance records
  above hold what was judged.
