# Acceptance record — The Online UI's paired controls and its pages' scroll affordance

- Ticket: `online-ui-paired-controls-and-page-scroll` — verdict: **moved to `done/`** (every row judged
  from this run's own readings; the two judgements a person owns are listed as residuals).
- Batch: `20261015-d` — tickets: this one; the run also re-reads the shell the layout pass left
  (`Tabs`, the page band, the one-line rule rows), which a later batch may reuse.
- Commit: `ce9db539` · Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion
  `0.1.0+ce9db53971ad2495b002a86c8501e9ddccfb8dc5` (`tools/verify-deploy.ps1`, exit 0). The ticket's
  first reading was taken on `+cc729fc6…`, which is the same code path with the slider's handle
  untouched (see Limits).
- Run: the same evening as the reading the ticket starts from · Host: the physical machine (one client,
  the Admin page of a hosted lobby) · no guest: the ticket's rows are one client's geometry.
- Dependencies: `game`, `deploy`, `steam`, `hotrepl`, `capture`, `input`, `logs`, `artifacts` — all
  present (`tools/acceptance/preflight.ps1`: `RESULT: OK - a full two-client run is possible`).
- Artifacts (in the directory named by `acceptance-artifacts-dir`, batch `20261015-d/`):
  `d3-admin-dump.json` and `f5-admin-dump.json` (the UI dump, recipe `cuo-ui-dump`),
  `g3-admin-dump.json` (the same dump with the recipe's anchor/pivot reading added),
  `f6-admin.png` (the Admin page, window-level), `d4-admin.png`, and the run logs `run.log`,
  `rerun.log`, `rerun3.log`, `anchors.log`.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The paired row's box is the page's own width | machine | **pass** | `f5-admin-dump.json`: the row `Window/Scroll/Viewport/Content/Line/GameSettingFloat(Clone)` reads `rect=984.00x36.00 span=-492.00..492.00`, and the page's own `Content` reads `span=-492.00..492.00` — the same box. The reading the ticket starts from had this row at `132.00` |
| 2 | The value box sits 6 units inside the page's right edge | machine | **pass** | `f5-admin-dump.json`: `.../GameSettingFloat(Clone)/GameSettingInt(Clone)/Input` reads `span=366.00..486.00`, the page's right edge is `492.00`, so the value box ends exactly 6 units inside it. The field's own row is flush with the page's edge (`352.00..492.00`) and its 6-unit room inside is that margin. Before: the field was drawn 268 units past a 132-unit row |
| 3 | A paired row keeps the page's control column | machine | **pass** | `f5-admin-dump.json`: the row's own label takes the column (`250.01`, `span=-486.00..-235.99`) and the slider starts at `-227.99` — the same column the toggle rows' controls start at (`-227.99..-195.20`) |
| 4 | The slider reads as a slider | visual | **pass** | `f6-admin.png` (read, and zoomed for this judgement): the track spans the control with both ends inside the page, the filled fraction carries the page's warm tone up to the value, and the value box sits at the row's trailing edge — one row, not two floating widgets. The reading agrees part by part: `Background 571.99x28.00` (grey, alpha 1), `Fill` (the warm tone, alpha 1), and a light `Handle`; the handle's own rect is the Limits entry below |
| 5 | The value is the game's own field and stays bound to the slider | machine | **pass** | `f5-admin-dump.json`: `.../Input` reads `input={text:"0.80", type:Standard, focused:false}` inside `GameSettingInt(Clone)`, i.e. the game's own field, one box, at the value both directions read (S2's own two-way write was judged in batch `20261015-c`) |
| 6 | The paired control is the two-line template | machine | **pass** | `f5-admin-dump.json`: the name is an element of its own (`CUO Online UI Label`, `text="背负重量系数"`, `984.00x28.00`) on one `Line`, and the row that carries the control (with the value inside it) is on the NEXT `Line` |
| 7 | A page that does not fit says so, and a page that fits does not | machine | **pass** | `f5-admin-dump.json`: `Content` `644.00` against `Viewport` `600.00`, with `CUO Online UI Scrollbar 8.00x596.00` and its handle `8.00x555.28` both present; the same recipe on a page that fits reports no bar (batch `20261015-c`) |
| 8 | No regression: the shell, the tab strip and the one-line rows | machine | **pass** | `f5-admin-dump.json`: `Tabs rect=984.00x36.00` with six `112.00`-wide tabs ending inside the window; a toggle row reads its old widths (`Label 250.01`, `Toggle 32.79` at `-227.99..-195.20`); the dropdown and field rows keep the column; the window's own bands are unchanged |

Judged from this run's own readings against the deployed artifact named above; no row is a pass by an
earlier batch's evidence.

## Residuals for the user

1. **The value box's right 6 units sit under the page's scrollbar when the page overflows.**
   `f5-admin-dump.json`: the value box ends at `486.00` and the page's bar spans `482.00..490.00`, so
   4 units of the box are under the bar's track (a click there is the bar's jump, not the field). The
   ticket's criterion asks for exactly those 6 units, so this is a consequence of it — whether page
   content should keep clear of the bar is a taste call that belongs to the user.
2. **Whether the slider now reads as one control with the value beside it** — `f6-admin.png` is the
   frame to look at. The run reads it as one row (track, filled fraction, value at the margin); a
   person's eye is the judge.

## Limits

- **The slider's handle geometry is not what the rule intends.** `g3-admin-dump.json`: the handle reads
  `rect=607.99x9.00` with `anchor="0.00,0.50..1.00,0.50"` — the engine's own place for a handle (its
  container's anchors, driven as the value moves) is NOT driven in this page, while the fill's own
  anchors ARE (the frame shows the fraction), so CUO's size write lands on a stretched rect and the
  handle is 36 units wider than its container. The frame still shows the thin line the user asked for,
  which is why row 4 passes; the geometry itself belongs to the next cycle, and the likely lever is
  asking the engine to lay its own slider out (`ICanvasElement.Rebuild(CanvasUpdate.Prelayout)`).
- **The two panels' half of the shape has no drawer that reaches it**: `OnlineUiPanelView` asks the same
  rule, and no panel drawer uses `NumberRule`, so it is aligned by construction and has no on-screen row.
- **A paired row's own minimum width is `282 + 2 × columnWidth`** (the row's label and the value field's
  own label both keep the column): a page whose name column exceeded 351 units would push the value past
  the page's right edge. The Admin page's column is ~250, so this is written down rather than guarded.
- **One client, one page**: the host's Admin page is the whole run. The other pages carry no paired row
  today, and the guest's and a third peer's views of the same page are not covered.
- The row-over-row rhythm, the value's two-way binding under the player's own typing, and the page's
  scrollbar under a dragged handle are the earlier batches' rows; this run re-read them, it did not
  re-run their scenarios.
