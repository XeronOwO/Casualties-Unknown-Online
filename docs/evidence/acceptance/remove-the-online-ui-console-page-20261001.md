# Acceptance record — Remove the duplicated command console page from the Online UI window

- Ticket: `remove-the-online-ui-console-page` — verdict: **moves to `done/`**
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
| 1 | Open the window: no Console tab; the other six tabs unchanged | visual + machine | **pass** | the live offered-control table carries exactly `tab.home`, `tab.players`, `tab.network`, `tab.admin`, `tab.worlds`, `tab.preferences` and no `tab.console`; `j02-preferences-zh.png` / `j03-preferences-en.png` show the six tabs; all six pages are reachable (`j09-*`) |
| 2 | `/` opens the console with completion, history and suggestions | machine + visual | **pass** (opened through the console's own `Open()` entry point — the OS `/` key is not injectable in this run; the record's Limits says so) | `isOpen=true`, input pre-filled `/`, 11 live suggestions (`ban`, `clear`, `heal`, `help`, …) and the hint text; `j05-console-open.png` |
| 3 | A command or a chat line still works | machine | **pass** (driven through the console's own input path) | `SetInput("/help")` + `Submit()` → `True`, the input cleared, and the help output rendered as the console's command list; `j06-console-help.png` |
| 4 | No unused page, drawer, enum member, state field or catalogue key remains | machine | **pass** | `OnlineUiConsoleDrawer.cs` is absent from the tree; no `tab.console`, `console.title`, `console.send` or drawer reference in `src/`; the pin suite (`OnlineUiConsolePageRemovalPinTests`, two-direction censuses with eight negative samples) is inside the green full suite |
| 5 | Build, tests, gates, format green | machine | **pass** (format note in Limits) | this batch's chain: `dotnet build` 0 warnings / 0 errors; full suite 300 normative gates + 4,529 tests passed; focused `FullyQualifiedName~OnlineUi` filter 407/407 |

## Residuals for the user

- None.

## Limits

- Rows 2 and 3 open and drive the console through its own in-process entry points
  (`CommandConsoleOverlay.Open()` / `ConsoleInputSession.SetInput` + `Submit`), because the run may not
  inject OS keys; the key press itself is not part of this run's evidence.
- `format` was not re-run: this batch changed no source file, so there is no formatting delta to
  produce; build and both suites are this run's own outputs.
