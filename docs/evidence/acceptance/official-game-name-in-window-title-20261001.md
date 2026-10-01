# Acceptance record — The window title must use the game's official Chinese name

- Ticket: `official-game-name-in-window-title` — verdict: **moves to `done/`**
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
| 1 | A Chinese client opens the window: the title reads `未知伤亡：联机` | visual | **pass** | `j01-window-home.png`, `j02-preferences-zh.png` — the window's title bar reads `未知伤亡：联机` |
| 2 | An English client is unchanged: `CASUALTIES UNKNOWN: ONLINE` | visual | **pass** | `j03-preferences-en.png` — the title bar reads `CASUALTIES UNKNOWN: ONLINE` after the window's own language control switched the UI to English |

## Residuals for the user

- None: both rows are readable from the frames this run captured.

## Limits

- The title was judged from window-level captures of the deployed artifact; the string itself is also
  pinned by the localization suite, which is green in this batch (full suite 300 + 4,529).
