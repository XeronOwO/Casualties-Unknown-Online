# Acceptance record — The Online UI's paired controls and its pages' scroll affordance

- Ticket: `online-ui-paired-controls-and-page-scroll` — verdict: **all five rows pass; the ticket moves to
  `done/`**, with the look the user rejected on batch `20261015-d` left as a residual for the user's eye.
- Batch: `20261018-t` — one build, one session, one Admin page; scope
  `docs/evidence/acceptance/20261018-t-scope.md`
- Commit: `5566b095` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5566b0954c12084df9a35fdf01faee47835d8686` (`verify-deploy.ps1` exit 0; `prelude-verify-deploy.txt`)
- Run: 2026-10-18 10:59 → 11:31 (+08:00) · Host: the physical machine · the window is a local surface
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `input`, `logs`, `artifacts`
- Artifacts: the `20261018-t` subdirectory of the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A page that does not fit says so; the bar moves with the content and appears only when the content overflows | machine + visual | **pass** | `a5b-admin-probe.txt`: the page's `Content` is `804.00` tall against a `600.00` viewport, the bar `CUO Online UI Scrollbar` is `8.00x596.00` at `482.00..490.00` with a `8.00x444.78` handle; `a11-admin-en.png` and `a14-prefs-en.png` show the bar at the right edge. The Preferences page, which fits, carries the same 10-unit gutter and no handle (deliberate, recorded below) |
| 2 | The slider reads as a slider: the track, the filled fraction and the handle are visible | machine + visual | **pass** | `a5b-admin-probe.txt`: `Slider/Background` is `820.00x28.00` at `-486.00..334.00`, the fill `223.33x28.00` inside the prefab's own `Fill Area` at `-486.00..-262.67`, the handle `36.00x36.00` at `-276.93..-240.93`; `a11-admin-en.png` shows the grey rail with the warm fill over it |
| 3 | The value is editable and bound both ways; an out-of-range or non-numeric value is refused and nothing commits until it parses | machine | **pass** (the half-typed presentation is a limit) | `a40`/`a41`: typing `0.55` into `admin.rule_piggyback_weight.field` moves the slider to `value=0.55` (fill `156.7`); `a42`/`a43`: setting the slider to `0.65` through its own `value` reads the field `INPUT[0.65]`; `a205`/`a206`: typing `abc` leaves the field `0.65` and the slider `0.65` — refused, nothing committed |
| 4 | The paired control is a template: the name is one line, the slider and value the next; rows line up whatever their names | machine + visual | **pass** | `a5b-admin-probe.txt`: the paired row is `974.00x36.00` at `-492.00..482.00`, its own label `active=0`, the slider `-486.00..334.00` starting at the page's left margin, and the value `Input` `356.00..476.00`, six units inside the content's right edge `482.00`; every one-line row's label starts at `-486.00`; `a11-admin-en.png` shows the two-line shape |
| 5 | No regression: the shell, the tab strip, the one-line rows and the bar | machine | **pass** | `a5b`: tabs `984.00x36.00` with six `112.00` Chinese tabs, the one-line rows `Label 318.97`/`Toggle 32.79` at the page's column, the bar `8.00x596.00` with its handle `8.00x444.78`. The column widened from `250.01` in batch `20261015-e` because the Admin rule caption is now `Allow member cheat verbs (switch)` and the column is the widest name — a content change, not a layout regression |

## Residuals for the user

- **The look.** The same Admin page the user rejected on batch `20261015-d` is in
  `a11-admin-en.png` (English) and `a6-admin-zh.png` (Chinese), captured on this artifact. The rows above are
  judged by numbers and pixels; whether the slider, the column and the bar now read as the user wants is
  their call. A rejection returns the ticket to `todo/`.

## Limits

- **One client, one page.** The host's Admin page is the whole run; the panels share the row geometry but no
  panel drawer reaches a paired row.
- **The invalid-input presentation.** The run sent one `set-text` edit (`abc`) and read the model's refusal
  (the field returns to the committed `0.65`, nothing commits). The per-keystroke half-typed sequence
  (`0`, `.`, `5`) a person produces was not replayed, so whether the field lets a decimal point through
  while typing is not proven here; the refusal itself is.
- **The value's two-way binding was driven through the field's own intent and the slider's own `value`
  setter**, not through a pointer drag on the handle. The fill/handle geometry is read from the live objects
  and the frame.
