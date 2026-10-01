# Acceptance record — The Online UI's layout and input detail pass

- Ticket: `online-ui-layout-and-input-detail-pass` — verdict: **back to `todo/`**
  (`- Status: Todo — Rejected (a text box's value area and a dropdown's caption are not laid out: the
  player cannot see what the box holds)`)
- Batch: `20261001-j` — tickets `online-ui-layout-and-input-detail-pass`,
  `online-ui-art-and-controls-overhaul`, `cuo-launcher-button-obscures-the-view`,
  `online-ui-panels-request-alpha-blend-false`, `remove-the-online-ui-console-page`,
  `official-game-name-in-window-title`
- Commit: `cdc604cd` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+cdc604cdf4b00929307fe8637f171299dc231738` (`deploy.ps1` then `verify-deploy.ps1`, exit 0)
- Run: 2026-10-01 08:21 → 08:35 (+08:00) · Host: physical machine (evaluator `18590`) · Guest: none —
  the window is a local surface and every row judged here was reachable on the host alone; the rows
  that need a second peer or a world are named `unproven` below
- Dependencies: preflight `11 present`, exit 0; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`
- Artifacts: the `batch-j` directory under `acceptance-artifacts-dir`, cited by file name below

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Any page scrolled to the top: the first row sits below the tab strip | machine + visual | **pass** | `Tabs` world rect `[312,538.7 → 968,562.7]`; the page's first active line `[312,512.9 → 968,532]` — 6.7 world units of gap, no overlap (`j01-window-home.png`, `j02-preferences-zh.png`) |
| 2 | Every control is one height, close to the tab strip's | machine | **pass** | live census: `GameSettingLanguage(Clone)` 17×36.0, `GameSettingInt(Clone)` 2×36.0, `GameSettingDropdown(Clone)` 2×36.0, `Tabs` 36.0 — the title bar's own × control is 22 and is not a page control |
| 3 | An English page: wrapped text keeps its own row; nothing is cut mid-word | visual + machine | **fail** | the `Current:` label beside a dropdown/field is 0 wide (`[316,417 → 316,441]`), the dropdown's own value text sits hundreds of thousands of units off-canvas, and the field's `Text Area` is 0×0 — see `j-probe-texts.txt` and `j-probe-inputfields.txt`; `j03-preferences-en.png` and its zoom `j03-zoom-top.png` show the empty boxes and the stray glyphs over the headings |
| 4 | Open the log-level/language dropdown: the list draws above the window, options click, the choice applies | visual + machine | **pass** (the option click is driven through the dropdown's own value/`onValueChanged` path, not a pointer) | `j04-dropdown-open.png` (the list over the whole window); log level 2→3→2 and the language zh→en applied and read back (`j-probe-dropdowns.txt`) |
| 5 | Click any text box: it takes the input and typing edits the value | machine + visual | **fail** | the edit path works (`prefs.profile_name` reads back `batch-j-test`), but the field's internals are not laid out — `Text Area` 0×0 and the field rect reading 324→232,691 wide (`j-probe-inputfields.txt`), so the typed value is not painted; `j03-preferences-en.png` |
| 6 | Pick a palette colour, then Auto: the hex field shows the carried colour's canonical hex | machine + visual | **fail** (the value mirrors; the field cannot show it) | the hex text followed every pick: `#E54D47` → `#4D8CF2` (blue) → `#B873E5` (Auto) → `#40FF80` (typed, read back in `j-probe-inputfields.txt`); the field and the `Current:` line are invisible for the row-3 reason |
| 7 | The game's own border is visible around the frame | visual | **pass** | `j08-border-topleft.png` (zoomed): the frame's light border along the top and left edges |
| 8 | The launcher over the medical panel stays readable | visual | **unproven** | needs a second peer and the remote medical panel; this run brought up the host alone |
| 9 | The title bar's drag clamp | machine | **unproven** | no OS-pointer drag path exists in this run's driver; not executed |
| 10 | A canvas smaller than the frame clamps the frame | machine | **unproven** | `PlayerCamera.main` is absent on the title screen, so the UI-scale path this row needs is unreachable in this bring-up |
| 11 | No regression: the launcher, all six pages, the quick panel, the context menu and the overlays | visual + machine | **fail** | six page frames (`j01`, `j02`, `j03`, `j09-home`, `j09-players`, `j09-network`, `j09-admin`, `j09-worlds`); the Preferences page's controls are unreadable (rows 3/5/6), and the quick panel/context menu/overlay half needs a session |

## Run facts

- Offline chain of this batch: `dotnet build` 0 warnings / 0 errors; full suite 300 normative gates +
  4,529 tests passed; the focused `FullyQualifiedName~OnlineUi` filter 407/407 passed; `deploy.ps1`
  and `verify-deploy.ps1` exit 0 with the artifact identity above.
- Bring-up: host through Steam, the window opened with the driver's `open-window`, pages switched with
  `goto-page`, controls driven with `click` / `set-text`, live rects read through ad-hoc evaluator
  snippets.

## The failure, in one paragraph

CUO sizes the control's own box, but the game prefab's internals keep their authored rects: a text
field's `Text Area` is 0×0 (so its text and caret are never painted), a dropdown's caption text sits
hundreds of thousands of units off-canvas, and a field's own rect reads hundreds of thousands of
units wide (all three in `j-probe-texts.txt` / `j-probe-inputfields.txt`). The
player therefore sees empty boxes; the model still carries the values (the colour followed each pick,
the typed name stayed in the model). Row 3/5/6 and the Preferences third of row 11 fail on this one
shape.

## Residuals for the user

- Row 8's picture (the launcher over the medical panel) needs a run with a second peer; this record
  does not claim it.
- Rows 9 and 10 are setup gaps, not judgements.

## Limits

- One client judged the window: it is a local surface, and rows that need a second peer or a world
  are `unproven`, never guessed.
- Driver interactions go through the control's own intent path (the Online UI's registered actions),
  never OS input; row 4's option click and row 5's typing were driven that way and are recorded as
  such.
- `format` was not re-run: this batch changed no source file, and the row's build/tests/gates half is
  what this run produced.
- The failure repeats in both languages (`j02` Chinese shows the same empty boxes), so it is not an
  English-layout special case.
