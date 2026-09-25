# Remote medical panel: hide the local-only action controls

- Status: Todo
- Priority: Medium
- Category: Remote medical / native WoundView reuse
- Source: User ruling (2026-09-21): while viewing another player's medical panel, do not show the sleep, workout (exercise) and main/off-hand switch controls at all — today only the sleep button is disabled. Hide them.
- Related: `review/remote-player-medical-panel.md`, `review/remote-medical-panel-acceptance-issues.md`, `review/remote-fentanyl-injection-and-medical-panel-desync.md` (its "nap button visibly disabled" wording is superseded here), `review/remote-medical-treatment-operations.md`

## Evidence

- `RemoteMedicalPatches` blocks the nap action with a `WoundView.TakeANap` prefix and its `Update`
  postfix only sets `__instance.napbutton.interactable = false`. The control therefore stays
  visible, and the panel's other local-only surfaces are untouched.
- The native `WoundView` carries those surfaces as fields: `napbutton` and `sleepImage`, the workout
  list `workoutList`, and the hand/mode switch control (`modeToggleImage` and the switch it drives).
  The implementation cycle confirms the exact control set against the game assembly before hiding
  anything.

## Requirement and acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Open a remote player's medical panel (Players page and right-click menu) | The sleep control, the workout list and the main/off-hand switch control are not visible at all |
| 2 | The same panel while viewing yourself | Unchanged: every native control visible and working |
| 3 | The hidden actions | Not reachable from the remote focus by mouse or by a keyboard shortcut |
| 4 | The read-only readouts and the allowed remote medical operations | Unchanged and still working |
| 5 | Closing the remote focus | The local panel's own controls return in the same frame |

## Non-goals

- Not building a CUO-side medical panel; the native `WoundView` stays the only surface.
- Not hiding read-only information that describes the remote body.
