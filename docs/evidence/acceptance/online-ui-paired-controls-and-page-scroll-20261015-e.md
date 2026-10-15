# Acceptance record — The Online UI's paired controls and its pages' scroll affordance

- Ticket: `online-ui-paired-controls-and-page-scroll` — verdict: **the four rejected points pass on this
  run's own readings** and the ticket moves to `review/` for the user's look at the frame. The four points
  are the user's rejection of 2026-10-15, quoted with the reading each one is read from on the ticket
  (*The user's rejection*); the direction that settled the slider's two ("the game already ships a slider
  control — why not reuse it?" → let the game's own interior proportions stand) is implemented as written.
- Batch: `20261015-e` — tickets: this one. The run also took the reading the ticket's direction names as
  its first thing to check, the game's own prefab ASSETS (`cuo-ui-prefab`, new in this cycle), which
  settles what an instance of the prefab can no longer say.
- Commit: `6dad2d41` · Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion
  `0.1.0+6dad2d413fbb97cff96ff567cf3ca84c6f56d0de` (`tools/verify-deploy.ps1`, exit 0) — the commit the
  reading is taken on, with `a1-verify-deploy.txt` recording it.
- Run: 2026-10-15, after the four fixes landed · Host: the physical machine (one client, the Admin page of
  a hosted lobby) · no guest: every row is one client's geometry.
- Artifacts (batch `20261015-e/`): `a5-admin-dump.json` (recipe `cuo-ui-dump`), `a6-prefab.json` (recipe
  `cuo-ui-prefab`), `a7-admin.png` (the Admin page, window-level), `a1-verify-deploy.txt`, `run.log`.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The page's content keeps the bar's room clear | machine | **pass** | `a5-admin-dump.json`: the viewport is `974.00x600.00 span=-492.00..482.00` and the page's `Content` the same width, while the bar is `8.00x596.00 span=482.00..490.00` — the content stops where the track begins. The value box `.../GameSettingInt(Clone)/Input` reads `span=356.00..476.00`, i.e. 6 units clear of the track. The reading the user rejected had it at `486.00` with the track at `482.00..490.00`, four units under it |
| 2 | A paired row's control starts at the page's left margin | machine | **pass** | `a5-admin-dump.json`: the row `Line/GameSettingFloat(Clone)/Slider` reads `820.00x36.00 span=-486.00..334.00`, which is the x every row's label starts at (`Line/GameSettingBool(Clone)/Label span=-486.00..-235.99`). The row's own label is `active=False` — the name is the element on the line above. Before: the slider started at `-227.99`, the page's control column |
| 3 | The slider shows ONE rail with the filled fraction over it | machine + visual | **pass** | `a5-admin-dump.json`: `Slider/Background 820.00x28.00` (grey, alpha 1) and `Slider/Fill Area/Fill 223.33x28.00` (the page's warm tone) — the same 28 units — with the fill inside the prefab's own `Fill Area` and its anchors driven to the value (`0.00,0.00..0.27,1.00`); the knob `.../Handle Slide Area/Handle` reads `36.00x36.00` standing at the value. `a7-admin.png` (read, and its slider band mapped pixel by pixel for this judgement) draws the yellow fraction's two lines and the grey rail's two lines on the SAME pixel rows (`438` and `456`), one rail with the knob on it. Before: a fill 36 units tall stretched over the whole control, drawn over a 28-unit rail, with a knob rendering 45 units tall |
| 4 | A page's own label line keeps the rows' room | machine + visual | **pass** | `a7-admin.png`: the paired row's name (背负重量系数), the section heading and every row's label all start at the same pixel column (`316`), i.e. the page's own leading room; before, the name started at the page's edge, four units left of the labels. The dump agrees on the box (`CUO Online UI Label 974.00x28.00 flexW=1.00` — the line is still the label's box, the room is the text's own margin) |
| 5 | The game's own interior, at the proportions it was authored with | machine | **pass** | `a6-prefab.json` reads the ASSETS: `Special/GameSettingFloat` is `1058.20x80.18` with the slider a `962.45x20.00` band inside it, its `Fill Area 942.45x20.00` the rail's own height, its `Fill` anchored at a point with no height of its own (`sizeDelta 10,00`), and its `Handle Slide Area 942.45x20.00` carrying a `20.00`-wide handle — so the game's own knob is one RAIL-height square with a travel inset by half of it, and nothing of the interior hangs out of its row. This is the ticket's "first thing the next cycle checks", answered by a reading rather than by inference |
| 6 | No regression: the shell, the tab strip, the one-line rows and the bar | machine | **pass** | `a5-admin-dump.json`: `Tabs 984.00x36.00` with six `112.00` tabs inside the window; the toggle rows read their old widths (`Label 250.01`, `Toggle 32.79` at `-227.99..-195.20`); the dropdown and field rows keep the page's column; the shell's bands are unchanged and the bar's handle still reads `8.00x555.28` |

Judged from this run's own readings against the deployed artifact named above; no row is a pass by an
earlier batch's evidence.

## Residuals for the user

None open. The look the user rejected on batch `20261015-d` is the only thing this run could not judge for
them: rows 3 and 4 are judged from the frame by the numbers and the pixels above, and the frame is in the
batch directory for their own eye.

## Limits

- **The knob is the row's height, not the rail's.** The game's own knob is one rail-height square
  (`20.00x20.00` on a `962.45x20.00` band of an `80.18`-unit row, `a6-prefab.json`); this page gives a
  control the whole compact row, so the knob follows that row (`36.00x36.00`, `a5-admin-dump.json`) and
  stands four units proud of the rail at each end. Deliberate and written down rather than hidden: a
  28-unit square would copy the game's ratio at a size this row does not have.
- **The fraction keeps the prefab's own horizontal inset.** The `Fill Area` is inset inside the rail
  (the game's room for its handle), so at full value the fraction stops short of the rail's ends — the
  prefab's own proportion, and the one part of "exactly over it" that is horizontal rather than vertical.
- **The bar's room is reserved whether or not the bar is drawn**: a page that fits carries the same
  10-unit empty gutter on its right. Deliberate — the wrapping that decides whether a page overflows must
  not change with its own answer — and invisible on a page with no bar.
- **A slider's draggable area changed.** It is now the game's own rail (`28` units tall) and knob
  (`36x36`), where it used to include a 36-unit-tall fill strip across the left of the row: the four units
  above and below the rail are no longer a drag target. The game's own parts are what answer the pointer,
  and the frame's slider reads as one control.
- **One client, one page**: the host's Admin page is the whole run. The panels share the row geometry but
  no panel drawer reaches a paired row, so they have no on-screen row here.
- The value's two-way binding under the player's own typing, the page's scrollbar under a dragged handle
  and the row-over-row rhythm are earlier batches' rows; this run re-read them, it did not re-run their
  scenarios.
