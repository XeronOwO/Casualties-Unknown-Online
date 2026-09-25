# Remote medical panel: hide the local-only action controls

- Status: Review
- Priority: Medium
- Category: Remote medical / native WoundView reuse
- Source: User ruling (2026-09-21): while viewing another player's medical panel, do not show the sleep, workout (exercise) and main/off-hand switch controls at all — today only the sleep button is disabled. Hide them.
- Related: `review/remote-player-medical-panel.md`, `review/remote-medical-panel-acceptance-issues.md`, `review/remote-fentanyl-injection-and-medical-panel-desync.md` (its "nap button visibly disabled" wording is superseded here), `review/remote-medical-treatment-operations.md`

## What landed (2026-09-25 cycle)

A remote medical focus is a READ-ONLY view of another player's body, so the local-only action
controls are no longer presented, and the hand switch is no longer reachable from it:

- **The surfaces are hidden, not disabled.** `RemoteMedicalLocalControls` (GameAdapter/Character) is
  the one place that knows the set — the nap control (`WoundView.napbutton` with its `sleepImage`
  state icon), the workout list (`WoundView.workoutList`) and the HUD main/off-hand switch control
  (`PlayerCamera.handSwapImage`) — and it is re-asserted every frame the focus is open, because the
  native panel keeps writing `napbutton.interactable` and a prefab animation can re-activate a
  surface.
- **Every surface returns exactly as it was.** The first hide pass remembers the visibility each
  surface had when the focus opened (a pure `SurfaceMemory`, unit-tested), and the focus's single
  close path (`RemoteMedicalView.Close`) writes those values back — before that path's own early
  return, so even a display body destroyed under the focus cannot leave a hidden control behind. A
  surface whose object is gone is skipped, with its memory consumed.
- **Both hand-switch entry points are blocked.** The HUD control's own method
  (`PlayerCamera.SwitchHands`) and the keyboard path (`PlayerCamera.HandleInput` →
  `Body.SwitchHands`) are different methods; blocking one would have left the other reachable.
  `BodyItemPatches.SwitchHandsPatch` owns the block for its method and passes the "nothing ran"
  verdict to its own postfix, so a blocked swap reports no inventory change and no slot move
  (a prefix that swallows a write must not let the postfix report it).
- **The nap action keeps its existing block** (`WoundView.TakeANap` prefix); its control now
  disappears instead of sitting there greyed out. The workout list's controls are the only entry to
  `PlayerCamera.DoBodyWorkout`, so hiding the list removes the action with it.
- **No wire, protocol or save change**: nothing about the shared state moves; a peer never learns
  that this viewer looked at a panel.

## Control set (resolved 2026-09-25)

The ruling's three controls are: the nap control (`WoundView.napbutton` with its `sleepImage` state
icon), the workout list (`WoundView.workoutList`) and the HUD main/off-hand switch
(`PlayerCamera.handSwapImage`, reached by the HUD control's own method `PlayerCamera.SwitchHands`
and by the `switchhands` key binding → `Body.SwitchHands`). The panel's armor/health VIEW toggle
(`WoundView.modeToggleImage` → `ToggleMode`/`armorMode`) is NOT one of them and stays visible: it is
a read-only read of the displayed body, which this ticket's own non-goal protects.

History, so a reader who saw the earlier wording knows what replaced it: this ticket's first
evidence bullet named `modeToggleImage` for the third control. That mapping was wrong (the field is
the armor/health view toggle, not a hand switch); the assembly puts the ruling's words on
`handSwapImage` and its two switch paths, and the user confirmed the HUD switch when asked.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Open a remote player's medical panel (Players page and right-click menu) | The sleep control, the workout list and the HUD main/off-hand switch control are not visible at all |
| 2 | The same panel while viewing yourself | Unchanged: every native control visible and working, including the armor/health view toggle |
| 3 | The hidden actions | Not reachable from the remote focus by mouse or by a keyboard shortcut (the `switchhands` key does nothing, no swap, no `switch` item sound, no off-hand warning) |
| 4 | The read-only readouts and the allowed remote medical operations | Unchanged and still working |
| 5 | Closing the remote focus | The local panel's own controls return in the same frame, and the HUD switch works again |
| 6 | Inventory reporting around a blocked window | Unaffected: a hand switch just before the focus and again right after it still updates the peer's view of both slots |

## Evidence

- Self-check: `docs/evidence/selfchecks/players/remote-medical-local-only-controls-selfcheck.md`
- Tests: `tests/CasualtiesUnknownOnline.Tests/Patching/RemoteMedicalLocalOnlyControlsTests.cs` — observed
  red 7/7 against the pre-fix tree (`%TEMP%/cuo-red.txt`), green after the fix (`%TEMP%/cuo-focused.txt`).
  The reporting rule ("a blocked swap reports nothing") is pinned on the COMPILED postfix, and the
  independent review's own evasion of the earlier text pin (report statements moved above the verdict
  check) was re-applied by hand as a negative control and observed failing
- Full suite with build and the normative gates on the frozen tree: `%TEMP%/cuo-full-verify.txt`
- Code: `src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteMedicalLocalControls.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteMedicalView.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/RemoteMedicalPatches.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyItemPatches.cs`

## Acceptance status

Code-complete and awaiting the user's release-cycle dual-client run: this cycle was instructed not to
deploy, so no real-machine run is claimed here. The visual half (a control really gone, the local
panel really untouched, the key really inert) is the session's to confirm; the self-check's §5 lists
the rows.

## Non-goals

- Not building a CUO-side medical panel; the native `WoundView` stays the only surface.
- Not hiding read-only information that describes the remote body (that is why the armor/health view
  toggle stays).
- Not changing the allowed remote medical operations, their authority, or the wire.
