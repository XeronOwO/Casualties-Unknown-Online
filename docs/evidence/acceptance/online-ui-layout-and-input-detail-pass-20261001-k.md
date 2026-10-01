# Acceptance record — The Online UI's layout and input detail pass

- Ticket: `online-ui-layout-and-input-detail-pass` — verdict: **pass on rows 3, 5, 6, 11's
  Preferences third; rows 1, 2, 4 and 7 re-confirmed; rows 8–10 stay unproven for their own batch**
- Batch: `20261001-k` — tickets `online-ui-layout-and-input-detail-pass` (the only ticket this batch
  judges; it is the one that failed batch `20261001-j` and went back to `todo/`)
- Commit: `968edee8` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+968edee8f61d9672509c2596f88391eee4b71692` (`deploy.ps1` then `verify-deploy.ps1`, exit 0,
  `Deployment matches this tree's build output`)
- Run: 2026-10-01 09:20 → 09:40 (+08:00) · Host: physical machine (evaluator `18590`) · Guest: none —
  the window is a local surface; the rows this batch judges are reachable on the host alone
- Dependencies: preflight `11 present`, exit 0
- Artifacts: the `batch-k` directory under `acceptance-artifacts-dir`, cited by file name below

## What the failure was, and what it is now

Batch `20261001-j` rejected this ticket because a text field's value area read **0×0** while its own
rect read hundreds of thousands of units wide, so a picked or typed value was never painted. The probe
of this batch pinned the mechanism: uGUI measures a stretched child by the rect it already has, so
asking a layout group for a field's preferred size reads the width the group itself just wrote. The
measurement fed itself — the same control measured **690,623** units wide on the baseline probe and
**849,678** on a reading minutes later, doubling every frame or two inside a 984-unit page.

The fix declares the width instead of measuring it, and places a control's insides one rect at a time.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Any page scrolled to the top: the first row sits below the tab strip | machine + visual | **pass** | `final-preferences.png`: the tab strip at the top of the frame with the page's first heading clear of it; the page's first row is below it in every page frame of this batch |
| 2 | Every control is one height, close to the tab strip's | machine | **pass** | every control reads `36` tall in `final-internals.json` (`GameSettingDropdown(Clone)` 984×36 with its `Dropdown` 150×36, `GameSettingInt(Clone)` 177.1×36 with its `Input` 157.1×36); the tab strip is the same 36 |
| 3 | An English page: wrapped text keeps its own row; `Preferences` fits its tab; no value cut mid-word | visual + machine | **pass** | `final-preferences.png`: `Preferences` inside its tab with room on both sides (tab widths from `final-buttons.json`: Home 76.6, Players 110.1, Network 111.8, Admin 81.6, Worlds 97.5, Preferences 169.2), and each row's caption ends before the control it names (the row label is 814 wide, the control starts after it) |
| 4 | Open the log-level/language dropdown: the list draws above the window and a choice applies | visual + machine | **pass** (option click driven through the dropdown's own `onValueChanged` path, not a pointer) | `style2-preferences.png` and `final-preferences.png` show both dropdowns holding their value (`Information`, `English`); the popup path is unchanged by this batch and is covered by the S3 pins |
| 5 | Click any text box: it takes the input and typing edits the value; the hex box shows the picked colour | machine + visual | **pass** | `final-values.json`: the hex field reads `#40FF80` with its text area **137.1×16** and `areaRaycast=True`, and a typed profile name reads back `batch-k-check` with its area **220×16**; `final-field-edit.png` shows the value painted inside the box |
| 6 | Pick a palette colour, then Auto: the hex field shows the carried colour's canonical hex | machine + visual | **pass** | the field mirrors the model (`#40FF80`, `final-values.json`) and is painted (`final-field-edit.png`); the mirror rule itself is the S5 pin, unchanged here |
| 7 | The game's own border is visible around the frame | visual | **pass** | every frame of this batch shows the frame's light edge; the batch changed no frame art |
| 8 | The launcher over the medical panel stays readable | visual | **unproven** | needs a second peer and the remote medical panel; this run brought up the host alone |
| 9 | The title bar's drag clamp | machine | **unproven** | no OS-pointer drag path exists in this run's driver; not executed |
| 10 | A canvas smaller than the frame clamps the frame | machine | **unproven** | the UI-scale path this row needs is unreachable on the title screen (`PlayerCamera.main` is absent) |
| 11 | No regression: the launcher, all six pages, the quick panel, the context menu and the overlays | visual + machine | **pass for the window half, unproven for the session half** | six page frames (`final-preferences.png`, `final-admin.png`, plus the `style2-*` set for Home, Players, Network, Worlds) and the census `final-census.txt` (`controls=4 worstControlWidth=200 zeroInteriors=0 windowWidth=1000`); the quick panel, the context menu and the world overlays need a session and are `unproven` |

## Run facts

- Offline chain of this batch: `dotnet format`; `dotnet build` 0 warnings / 0 errors; full suite
  **300 normative gates + 4,538 tests, all green**; focused `FullyQualifiedName~OnlineUi` 411/411;
  `deploy.ps1` and `verify-deploy.ps1` exit 0 with the artifact identity above.
- Bring-up: host through Steam, the window opened with the driver's `open-window`, pages switched with
  `goto-page`, the text edit driven with `set-text`, live rects read through ad-hoc evaluator snippets.
- The style this batch settled (the user's own calls of 2026-10-01): one control shows ONE box. The
  game's inner box art is hidden and the control paints a dark fill with a hairline inside its edge, so a
  field reads as the same family as a button on every page. The first attempt drew that edge with the
  game's own nine-slice sprite and failed visibly — the sprite's BODY is opaque, so the box came out
  white with no text (`style1-preferences.png`) — and the edge is four one-pixel lines now.

## Residuals for the user

- Rows 8, 9 and 10 are setup gaps, not judgements: they need a second peer, an OS-level drag, and a
  canvas smaller than the frame respectively.
- The row labels beside a control (`Current:`, `Hex:`) sit at the left edge of their row and read as
  small muted captions; whether they should be dimmer or moved is a look call, not a defect this run
  found.

## Limits

- One client judged the window: it is a local surface, and the rows that need a second peer or a world
  are `unproven`, never guessed.
- Driver interactions go through the control's own intent path, never OS input; row 5's typing was
  driven that way and is recorded as such.
- A pointer click on a field was not executed (no OS input in this run); what is proven is the STRUCTURE
  the click needs — the control's own box carries a raycast target, its text area is a raycast target,
  and both have a rect with room in it (`final-values.json`).
