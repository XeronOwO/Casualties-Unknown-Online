# Acceptance record — The Online UI's panels asked for alphaBlend: false

- Ticket: `online-ui-panels-request-alpha-blend-false` — verdict: **moves to `done/`**
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

The ticket's observable claim is the blended draw. Since S2b/S5/S6 migrated the window, the quick
panel and the context menu to uGUI, the only themed IMGUI surface left to see is the console overlay;
the census row is the pin suite's own contract.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The themed frame actually blends: the world shows through and the text stays readable | visual | **pass** | `j05-console-open.png`, `j06-console-help.png` — the open console overlay renders its history, suggestions and input over the page and the world, with the panel translucent behind them |
| 2 | Every themed draw asks for blending (the five-surface census) | machine | **pass** | `OnlineUiLauncherFadeTests` holds the blended frame draw, the blended overlay and two mutation rows; it is inside this batch's green full suite (300 + 4,529) and the focused `OnlineUi` filter (407/407) |

## Residuals for the user

- None: the visible half is in the frames; the code half is the pin suite's contract, run here.

## Limits

- The blended window/quick-panel/context-menu draws the ticket's `What landed` table lists no longer
  exist as themed IMGUI surfaces (the uGUI migration retired them); this run could only observe the
  console overlay, which is the one themed surface left, and it is the surface whose appearance the
  ticket's own Limits said might change.
