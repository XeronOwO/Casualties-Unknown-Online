# Acceptance record — The Online UI's layout and input detail pass

- Ticket: `online-ui-layout-and-input-detail-pass` — verdict: **moved to `done/`**: rows 4 and 9 **pass**;
  the other rows were judged by the batches the ticket's history names and were not re-run.
- Batch: `20261019-c` — tickets `online-ui-layout-and-input-detail-pass` (the two rows batch `20261018-t`
  rejected; window-only, no session).
- Commit: `c9769177` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c9769177d53b0279daa93f05f75b1c513f53a546` (`verify-deploy.ps1` exit 0; `verify-20261019-c.log`).
- Run: 2026-10-19 19:33 → 19:36 (+08:00) · Host: the physical machine (one client, no lobby) · No member
  client: both rows are window-only, the scope the sixth pass states.
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `hotrepl`, `capture`, `logs`, `artifacts` — the
  preflight reported eleven present (`preflight-20261019-c.txt`).
- Artifacts: the `20261019-c` subdirectory of the directory named by `acceptance-artifacts-dir`.

## Why this batch

Batch `20261018-t` rejected two rows on the deployed build: an open dropdown's list was a negative-height
rect at the window's top edge (row 4) and the drag clamp's top edge kept the window's bottom instead of its
title bar (row 9). The sixth pass fixed both and re-read them on the committed build, but a development
reading is not acceptance. This run re-read the two rows on the deployed artifact with one client, no
session — the rows' own scope.

## Method

The host client was started from the game directory with no lobby and no member. The Online UI window was
opened through the driver's `open-window` action and the `Preferences` page through `goto-page`; the
window-only rows were then driven through the running client's in-process evaluator:

- **Row 4** — the language dropdown was opened and read (`dd.Show()`, the same `TMP_Dropdown.Show` the
  game's own field click reaches), a frame captured before the list normalised, and the list re-read after
  a frame to see its canvas sorting. An option was applied through the item's own `Toggle` — TMP wires the
  item's `onValueChanged` to `OnSelectItem` — and the result read in the same eval and in the next frame.
  The log-level dropdown (seven options) repeated the shape.
- **Row 9** — the production `OnlineUiWindowDragHandler.OnDrag` was called with a synthetic `PointerEventData`
  delta on each of the four axes and the window's and title bar's world boxes were read; the window was
  then left at the up clamp for a frame and the run reset it to the origin.
- Both rows were then restored (the UI returned to Chinese, the list hidden, the window at the origin) and
  the client quit through the driver.

## Verdicts

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 4 | Open the log-level or language dropdown: the list draws above the whole window, is not clipped, every option can be clicked; choosing one applies at once | machine + visual | **pass** | `e1-popup-language.txt`: language dropdown `expanded=1 options=2`; the list is `150.0x193.4` at `y -241.4..-48.0` under its field's `y -48.0..-12.0`, `underField=1 widthMatches=1`, with the viewport `150.0x193.4`, `mask=1 scroll=1`, and both option items `active=1`; `e2-popup-language.png` shows the list over the page's player-name section. `e12-popup-normalised-after-frame.txt`: after one frame the list's canvas sorts `30001`, the popup layer's own order. `e3-select-english.txt`: the list carries its own `GraphicRaycaster` (`listRaycaster=1`), every option item is a `Toggle` with a raycast background (`toggle=1 bgRay=1`), and choosing English applied at once (`value 1->0`); `e4-after-select-english.png` is the English UI in the next frame. The log-level dropdown repeats it with `options=7`, content `150.0x1010.1` and the same mask and scroll (`e5-popup-loglevel.txt`, `e6-popup-loglevel.png`). |
| 9 | Drag the title bar towards an edge: the window follows, and a strip of it always stays on the canvas so it can be grabbed again | machine + visual | **pass** | `e8-drag-test.txt`: on the `1920.0x1080.0` canvas the four production `OnDrag` calls clamp the window to `1412,0` / `-1412,0` / `0,190` / `0,-842`. At the UP clamp the window is `307,253..973,720` and the title bar `312,696..968,715` — wholly inside the canvas; the pre-fix reading had the title bar 411 screen pixels above it. The DOWN clamp keeps the title bar's `312,8..968,27` strip, and left/right keep the reachable strip. `e9-drag-up-position.txt` and `e10-drag-up-frame.png` are the up clamp left in place and its frame, showing the title bar and close button on screen. |

## Residuals for the user

None. Both rows are machine- and frame-judged in this run.

## Limits

- **No OS pointer and no OS wheel.** The option choice rode the item's own `Toggle` (TMP's production
  listener), the drag the production `OnlineUiWindowDragHandler.OnDrag` with a synthetic delta, and the
  seven-option list's longer content is the game's own `ScrollRect` inside the viewport — read, not
  wheel-driven. The same limit batch `20261018-t` recorded.
- **One host, one window, no session.** The rows are window-only, as the sixth pass's own scope states; the
  session-half rows (row 11) were judged in `20261018-t` and are not re-run.
- **The row-10 small-canvas flip was read by the sixth pass, not by this run.** This run's rows are the two
  the ticket was rejected on; the sixth pass's readings for the other rows are development evidence, not
  acceptance evidence.
- **The frames are DPI-aware** (`capture-dpi.ps1`, the helper `20261018-t`'s corrected frames used);
  `PrintWindow` answered `true` for every capture.
- **The host log carries one HotRepl `TypeLoadException`** — `System.ComponentModel.DataAnnotations`
  `RequiredAttribute` inside the evaluator's schema cache (`System.ComponentModel.Annotations` 5.0.0.0).
  It is the tooling's, the plugin loaded and every probe answered; no CUO exception and no `[ERR]` from the
  UI paths appears in the run.
