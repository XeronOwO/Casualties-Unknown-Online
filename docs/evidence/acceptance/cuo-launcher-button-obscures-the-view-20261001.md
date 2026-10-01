# Acceptance record — The CUO Online launcher button covers the play area

- Ticket: `cuo-launcher-button-obscures-the-view` — verdict: **moves to `done/`** (row 2 is a residual)
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
| 1 | No interaction for the idle window: translucent, the world behind it readable | machine + visual | **pass** | three live reads of the launcher's `CanvasGroup` alpha, all `0.12`, about a second apart; `j07-window-closed-launcher.png` (the launcher at its idle floor leaves the corner to the world) |
| 2 | The cursor moves over it: full opacity, immediately | visual | **residual** | the surface polls the OS pointer, which this run may not move; the row needs the user's pointer (or a future pointer-injection path) |
| 3 | Clicked in either state: the window opens and closes exactly as today | machine | **pass** | with the launcher at its idle alpha, `window.close` applied (window hidden) and the driver's `open-window` — the launcher click's own action — returned `visible=true` |
| 4 | The window is open and the mouse is elsewhere: no flicker, no per-frame allocation increase | machine | **pass** | three alpha reads over ~3 s with the window open, all `0.12`, no flip; the label pair is cached in the window state (the allocation half is a source fact, not re-measured) |
| 5 | Solo menu / no session: same behaviour | machine + visual | **pass** | this whole run is the title screen with no session (`role=None`), and the idle reads and click toggles above are from it |

## Residuals for the user

- Row 2 — hover restores full opacity at once: put the pointer on the launcher's top-right corner and
  confirm the button comes back opaque and stays clickable. The idle half is already judged here
  (`j07-window-closed-launcher.png`, alpha `0.12`).

## Limits

- No pointer injection: the hover direction (row 2) could not be produced, so it is a residual rather
  than a pass; the click path was driven through the launcher's own action (the driver's
  `open-window`), which is the click's registered intent.
- The "no per-frame allocation" half is not measured here; it is held by the cached label pair in the
  source, and this run only proves the alpha is stable.
