# CPR and revival

Restarting a stopped heart on another player's body.

## What the player does

Opens a patient's medical view, presses and holds the CPR control on that patient, and keeps compressing. The player
expects each press to cost their own stamina, the compressions to advance the patient towards revival, and the patient to
come back with the state a revived body has.

## What the game itself does

The compression is the game's own CPR minigame: the patient's own client runs it against the patient's body, and the
operator's presses arrive as the operation's input. The native gate on the operator is the game's own strength rule
(`skills.STR * handMaxTotalForce` against the hands' own total force), which is why a weak operator cannot compress at all.

## Rules and states

- The operator's own body is asked FIRST, before anything is sent: if it cannot compress, or has no stamina for another
  press, the gesture is refused on the operator's own client and nothing leaves it.
- Each press costs the operator stamina; the patient advances only while presses keep arriving.
- Revival is the positive outcome of the operation; a patient whose body cannot be revived stays in the medical state the
  operation reports.
- The CPR control is only offered where it can act: it belongs to the medical view of a patient, not to the operator's own
  body.

## Refusals

| Condition | The player reads |
|---|---|
| the operator is too weak to compress | `medical.cpr_too_weak` |
| the operator has no stamina for the press | `medical.cpr_exhausted` |
| the target is dead | `use.refused.target_dead` |
| no patient selected | `use.refused.no_patient_selected` |

## Who judges

The operator's own client refuses a press its body cannot make. The patient's own client runs the compression and decides
whether the body advances; the host arbitrates the operation's frames and holds the resulting body state.

## What travels

The patient, the operation's identity, and each press as the operation's input. The patient's side reports the body's
revived state, which the host applies.

## What the player sees and hears

The operator sees the compression animation and the patient's body respond; both sides hear the game's own CPR sounds. A
refusal is one line in the operator's language plus the game's deny sound.

## Records

`../../architecture/surfaces.md` (medical view row), decisions 184 and 154. Accepted:
`../../backlog/done/remote-medical-cpr.md`.

## Gaps

- The number of presses and the stamina cost per press come from the game's own minigame and are not recorded here yet.
- The interaction between CPR and a body that dies during the operation is recorded only in the minigame's own outcome;
  it belongs in this page once read.
