# Acceptance record — The Online UI's art and controls are placeholders

- Ticket: `online-ui-art-and-controls-overhaul` — verdict: **stays in `review/`** (row 2 is
  `unproven`: the pointer/OS-key close paths could not be produced)
- Batch: `20261001-j` — tickets `online-ui-layout-and-input-detail-pass`,
  `online-ui-art-and-controls-overhaul`, `cuo-launcher-button-obscures-the-view`,
  `online-ui-panels-request-alpha-blend-false`, `remove-the-online-ui-console-page`,
  `official-game-name-in-window-title`
- Commit: `cdc604cd` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+cdc604cdf4b00929307fe8637f171299dc231738` (`deploy.ps1` then `verify-deploy.ps1`, exit 0)
- Run: 2026-10-01 08:21 → 08:35 (+08:00) · Host: physical machine (evaluator `18590`) · Guest: none
- Dependencies: preflight `11 present`, exit 0; the machine gate before launch `active=cuo`,
  `game-running=false`, `launch=ok`
- Artifacts: the `batch-j` directory under `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The surfaces read as this game — panel shapes, palette, typography, control look | visual / residual | **residual** | frames: `j01-window-home.png`, `j02-preferences-zh.png`, `j03-preferences-en.png`, the six pages `j09-*`; the game's own prefabs, font and frame carry the look, and the subjective judgement is the user's |
| 2 | Choice controls behave like real controls — opens, tracks the pointer, closes on click-away, Escape and selection; the colour input takes any colour | machine + visual | **unproven** | proven: the list opens over the whole window (`j04-dropdown-open.png`); a selection applies (log level 2→3→2, language zh→en); the colour field takes an arbitrary value (`#40FF80` read back). Not proven: pointer tracking, click-away close and Escape close — this run has no pointer or OS-key path |
| 3 | No regression: actions, hotkeys, layout anchors, translation keys, the launcher's fade, the blended frames; pins green | machine + visual | **pass** (hotkeys not exercised — see Limits) | six page frames; `open-window` / `click` / `set-text` actions answered; the launcher's fade read `0.12` and toggled (`window.close` + `open-window`); the console overlay blended (`j05`/`j06`); the full suite is green (300 normative gates + 4,529 tests) and the focused `OnlineUi` filter 407/407 |

## Related defect (not re-judged here)

The Preferences page's control internals are unlaid-out — a text field's `Text Area` is 0×0, a
dropdown's caption sits off-canvas, and the boxes read empty. That is the failure recorded in
`online-ui-layout-and-input-detail-pass-20261001.md` (rows 3/5/6), seen on the same build and the same
frames (`j02`, `j03`); this ticket's row 1 cannot call the surfaces "reading as this game" while a
value the player types is invisible, but the repair belongs to the sibling ticket.

## Residuals for the user

- Row 1 — whether the migrated surfaces read as this game at the player's scale: look at
  `j01`/`j02`/`j03` and the six `j09-*` frames.

## Limits

- Row 2's pointer/OS-key halves are a capability gap, not a finding: the driver may not inject OS
  input, so those sub-rows stay `unproven` and the ticket stays in `review/`.
- The hotkeys named in row 3 (the Online UI's own keys) were not exercised; the actions were driven
  through their registered control ids instead.
