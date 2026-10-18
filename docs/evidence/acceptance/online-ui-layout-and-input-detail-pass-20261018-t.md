# Acceptance record — The Online UI's layout and input detail pass

- Ticket: `online-ui-layout-and-input-detail-pass` — verdict: **moved back to `todo/`**: row 4 (an open
  dropdown's list) and row 9 (the drag clamp's top edge) **fail**; rows 1, 2, 3, 5, 6, 7, 8, 10 and 11 pass.
- Batch: `20261018-t` — tickets `online-ui-layout-and-input-detail-pass`,
  `in-game-command-console-interactive`, `command-console-esc-not-intercepted`,
  `online-ui-paired-controls-and-page-scroll`, `enter-key-opens-plain-chat` (scope
  `docs/evidence/acceptance/20261018-t-scope.md`)
- Commit: `5566b095` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5566b0954c12084df9a35fdf01faee47835d8686` (`verify-deploy.ps1` exit 0; `prelude-verify-deploy.txt`)
- Run: 2026-10-18 10:59 → 11:31 (+08:00) · Host: the physical machine (one client for the window rows) ·
  Member: the `Steam1` sandbox, in world for row 11's session half
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `input`, `logs`, `artifacts`
- Artifacts: the `20261018-t` subdirectory of the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Any page scrolled to the top: the first row sits below the tab strip, nothing under the tabs | machine + visual | **pass** | `a5b-admin-probe.txt`: the tab strip is `984.00x36.00` at `268.00..304.00`, the page viewport starts at `258.00` and its first line is `230.00..258.00`, so the first row clears the strip by 10 units; `a6-admin-zh.png` and `a11-admin-en.png` show the same gap |
| 2 | Every control is one height, close to the tab strip's | machine | **pass** | `a5b-admin-probe.txt`: every control row is `36` tall — a toggle `32.79x36`, the dropdown `120.00x36`, the paired value field `120.00x36`, the slider `820.00x36`, the same as the `36`-unit tab strip |
| 3 | An English page: wrapped text keeps its own height; `Preferences` fits its tab; no value cut mid-word | machine + visual | **pass** | `a10-admin-en-probe.txt`: the six tabs are 112 / 132.14 / 133.80 / 112 / 119.46 / 191.15 units on a `984.00` strip, ending at `328.55` — inside the band; `a13-prefs-en-probe.txt` has the English wrapped hint as its own multi-line rows, and `a11-admin-en.png` shows every tab caption whole |
| 4 | Open the log-level or language dropdown: the list draws above the whole window, is not clipped, every option can be clicked; choosing one applies at once | machine + visual | **fail** | `a92-dropdown-open2.txt`: the language dropdown reports `expanded=1 options=2`; `a94-popup-detail3.txt`: the open list is `980.00x-20.00` with anchors `0.00,1.00..1.00,1.00` at world `313.3,600.0..966.7,586.7` — a NEGATIVE height at the canvas's top edge, not under the field; `a93-popup-dpi.png` (the corrected full-window capture) shows no list at the language row. The log-level dropdown repeats it (`a23`). The layer itself is right — sorting `30001`, its own raycaster, no masked ancestor (`a16`), and a value chosen through the dropdown's own `onValueChanged` applies at once (`a8`: `before=1 after=0`) — but the list is not drawn and its options cannot be reached where the field is |
| 5 | Click any text box: it takes input and typing edits the value; the hex box mirrors a picked colour in the same frame | machine + visual | **pass** (the pointer click itself is a limit) | `a35`/`a36`: typing `#123456` into the hex field reads back `input=#123456`, the preview swatch `18,52,86`, the current line `#123456`; `a38`: the profile-name field reads `batch-t-name`, `ray=1`, `221.53x36`; the fields' boxes and text areas are raycast targets in `a13`; `a14-prefs-en.png` shows the values painted. The run drove the edit through the field's own intent, not an OS pointer click |
| 6 | Pick a palette colour, then Auto: the hex field shows the canonical hex of the carried colour both times; the "current" line says which | machine + visual | **pass** | `a29`/`a30`: clicking `prefs.player_color.swatch.red` reads the field `#E54D47`, the current line `Red`, the preview `229,77,71`; `a32`/`a33`: clicking `prefs.player_color.auto` reads `#B873E5`, the current line `Auto`, the preview `184,115,229` (the automatic colour for the host's id — the initial `#40FF80` was a stored colour from an earlier run, not the automatic one); `a31`/`a34` are the frames |
| 7 | The game's own border is visible around the frame | visual | **pass** | `a6-admin-zh.png`, `a11-admin-en.png` and `a100-medical-dpi.png` all show the frame's light edge around the dark surface |
| 8 | The launcher over the medical panel: it fades to a ghost, the panel's readout stays readable; hovering brings it back, and it stays clickable in both states | machine + visual | **pass** (hover is a residual) | `a95`/`a99`: the launcher's `CanvasGroup.alpha` is `0.120`, `blocksRaycasts=1`, `interactable=1`, `imageRay=1`; `a100-medical-dpi.png` is the DPI-correct frame of the WoundView readout with the launcher at the top-right over the game's STAT readout, and `a100-launcher-crop.png` shows the caption as a grey ghost with the readout's green values readable around it; `a49`/`a50`: the faded launcher's own `onClick` opens the window (reached through its production button while the medical panel was up). The hover restore needs a real pointer, which this run does not drive |
| 9 | Drag the title bar towards an edge: the window follows, and a strip of it always stays on the canvas so it can be grabbed again | machine | **fail** (the production handler, a synthetic delta) | `a51-drag-test.txt`: the four drags move the window to `1412,0`, `-1412,0`, `0,842`, `0,-842` — it follows and clamps. At the UP clamp the window's world box is `307,688..973,1155` against a canvas of `0,0..1280,720`: the 32-unit strip that stays inside is the window's BOTTOM, and the title bar (the only drag target) is at `312,1131..968,1149`, 411 units ABOVE the canvas top. Left, right and down keep the title bar inside. A window dragged to the top cannot be grabbed again |
| 10 | A canvas smaller than the frame clamps the frame instead of hanging off it | machine | **pass** | `a55`/`a56`: with the game's own `CanvasScaler.referenceResolution` set to `900x500` the canvas reads `888.89x500.00` and the window fits `856.89x468.00` — 16 units of room on every side; the scaler was restored (`a58`/`a59` back to `1000x700` on a `1920x1080` canvas); `a57-small-canvas2.png` is the frame |
| 11 | No regression: the launcher, all six pages, the quick panel, the context menu and the world-space overlays behave as before; the gates and the pins stay green | machine + visual | **pass** for the window and session halves | Six pages were opened and captured: Home (`a3`), Players (`a62-players`), Network (`a62-network`), Worlds (`a62-worlds`), Admin (`a11`), Preferences (`a14`). The launcher's alpha and click are `a95`/`a49`; the quick panel opens through its production toggle, lists the member and all five actions, and is fully visible in `a89-dpi-1280.png` (the docked corner; `a75` is the same panel centred while the capture still cropped the corner); the in-world menu opens on the member (`a200`/`a201`, panel `240x36` at `1058,638`; `a202` reads its one name label) — its action list is empty because the target is not in line of sight (`CanSee=false`), which is the policy, not a regression; the world-space marker `Kaguya (小号) 27 m` is visible in `a197-notification-strip.png`. The dump's control column is `318.97` wide where batch `20261015-e` read `250.01`: the Admin rule caption `Allow member cheat verbs (switch)` is a longer name, and the column is the widest name (a content change, not a layout regression) |

## Residuals for the user

- **Row 8's hover restore.** The launcher's ghost state is `alpha=0.120` and its click works while faded; the
  hover half needs a real pointer. Frames: `a100-medical-dpi.png`, `a100-launcher-crop.png`.

## Limits

- **Row 4's pointer half.** The list's failure is read from the popup layer's own live state and a
  DPI-correct frame; a real pointer click on an option cannot be performed by this run. The list is not
  drawn where the field is, so there is nothing to click.
- **Row 5's pointer half.** The edit path is the field's own intent; the OS pointer click that focuses it is
  not driven. The field's box, text area and raycast targets are read in the dump and the probe.
- **Row 9's gesture.** The production `OnlineUiWindowDragHandler.OnDrag` was called with a synthetic
  pointer delta; the pointer-down that starts a drag is OS mouse input. The clamp's arithmetic and the
  title bar's position are the row's observable, and they are machine-read.
- **The dump recipe.** `cuo-ui-dump` answered `truncated` on the Admin page (the evaluator's value cap), so
  the readings above are from a compact ad-hoc probe of the same window tree (`snippets/ui-probe.cs`),
  cross-checked against the recipe where it answered (`a16`).
- **The capture's DPI.** The standard window capture cropped the right/bottom of the canvas on this
  display; the frames named above are from the DPI-aware helper (`capture-dpi.ps1`). The window rows are
  inside the old crop as well; the edge rows (8, 11) needed the fix.
