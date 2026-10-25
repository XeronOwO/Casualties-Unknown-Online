# Medical panel: a patient's body on the operator's screen

The panel that shows a teammate's wounds and offers the treatments.

## What the player does

Opens a teammate's medical panel, looks at their limbs and wounds, selects a limb, and applies a treatment. The player
expects the panel to be the PATIENT's body — not the viewer's — and the actions it offers to be the ones that apply to that
patient right now.

## What it is

The game's own medical UI — its `WoundView` chart — opened with a REMOTE focus. The ticket that accepted it states the
shape: "`OpenRemoteMedical(steamId, displayName)` opens the game's own WoundView medical panel for an in-world remote
player", a display-only body copy is created "from the `Experiment` template" and "projects the remote player's latest 1 Hz
`CharacterDataMsg` (health, limbs, skills) onto it", and "`RemoteMedicalPatches` keep the native view read-only while it is
focused on a remote player: nap, radial use/wear, and special limb actions are blocked". It is not a CUO panel, and the
live remote render clone is never mutated by this path.

## Parts and states

- The chart belongs to the viewed body: the display body carries the patient's health, limbs and skills, and the header
  names the patient.
- The view is READ-ONLY while focused on a remote player: nap, radial use/wear and special limb actions are blocked, so
  nothing the viewer does in the chart acts on their own body.
- The viewer's own state never appears in the chart except as a reason an action is refused (the operator's own strength
  for CPR, the operator's own stamina).
- The chart closes on session, world or remote exit, and the display body is torn down with it.
- The identity header carries the VIEWED player's data, not the viewer's. The accepted record states the mechanism: the
  game fills the header once, from the local character, and "nothing rewrites it afterwards — `WoundView.UpdateView` never
  touches the two labels — so a remote medical focus that does not take the header over keeps the VIEWER's identity over
  the viewed player's body"; the user's ruling on it is "the header must show the VIEWED player's own data".
- The local-only controls are HIDDEN, not disabled: "the nap control … the workout list … and the HUD main/off-hand switch
  control" are removed while the focus is open and re-asserted every frame, and the hand switch key is refused while the
  focus is open, because "a remote medical focus is a READ-ONLY view of another player's body".
- A viewer's own changes do not reach the patient: the chart is display-only, with no protocol and no authority change.- **The chart never sits on a LIVE body.** The display copy is created inactive so nothing runs on it; the recorded storm came from a live body under the focus — the ticket's own words: "The code does instantiate it there — and that copy is created INACTIVE, so nothing runs on it — but that is not what produced the storm. The storm's body was a LIVE one". The rule is therefore about which body the focus may attach to, not only about the copy.

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
