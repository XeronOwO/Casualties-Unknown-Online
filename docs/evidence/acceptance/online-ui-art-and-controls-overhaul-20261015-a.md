# Acceptance record — the Online UI art and controls overhaul (the colour picker's blocks)

- Ticket: `online-ui-art-and-controls-overhaul` — verdict: **moved to `done/`**
- Batch: `20261015-a` — tickets `online-ui-art-and-controls-overhaul`, `online-ui-layout-and-input-detail-pass`
- Commit: `5279880b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5279880bf8523f916897b606439b446d26a4781e`
- Run: 2026-10-14 22:5x → 23:2x (the batch's own label is the id its artifacts live under) · Host: physical machine (one client; every row of this batch is a local page rendering)
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the `20261015-a` subdirectory of the directory named by `acceptance-artifacts-dir`

## What the row is

Batch `20261014-a` rejected this ticket on three rows — the blocks did not read as a palette, a block
measured 18.0 units against the 88 its drawer declares, and the picker read as cut off at the page's bottom
edge. The cycle that answered them is in the ticket's own *What landed — the colour block's fill and width*
section, and this run re-judges the same three rows against the fix.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The colour picker's palette blocks read as a palette of colours | visual | **pass** | `a6-host-prefs-zh.png` (the Preferences page scrolled to the picker) and `a7-colour-zoom.png` (the same frame's picker at 2×). Eight blocks in one row, each a solid colour across its middle inside the game's own row sprite; the live preview block beside `当前：` carries the same green the markers do |
| 2 | A block's own size is the size the drawer declares | machine | **pass** | `d3-prefs-after.txt` (the live object tree): every palette block `GameSettingLanguage(Clone) rect=88.0x36.0` with `LE[w=88.0/88.0/0.0]`, the preview `rect=64.0x36.0` with `LE[w=64.0/64.0/0.0]` — the drawer's own `SwatchWidth = 88f` and `PreviewWidth = 64f`. Each block carries one `Swatch Fill rect=84.0x32.0` child whose image is the palette colour and whose raycast is refused; the row's own sprite is untinted |
| 3 | The picker fits the page it is drawn on | visual | **pass** | `a6-host-prefs-zh.png`: with the page scrolled to it, the whole picker is inside one viewport — the `当前：` preview, the hex field, the eight blocks in one line (8 × 88 + 7 × 4 = 732 of the page's 984 units) and the `自动` control below them; nothing is clipped and the row no longer sits at the fold |

## What moved with the fix, read in the same run

- The blocks' width is now written on the element itself (`OnlineUiRowGeometry.LayOutRow`), so the wrap the
  window asks and the width the line gives agree — the same rule gives the tab row its declared floor.
- The row's own grey frame is gone from every page (the user's report of 2026-10-14, answered in the sibling
  ticket's cycle): `d4-prefs-after.png` still shows it, `e1-prefs-noframe.png` and `a6-host-prefs-zh.png` do not.
- The four pages this ticket does not own were captured as the change's family check (`b5-host-home-zh.png`,
  `b5-host-players-zh.png`, `b5-host-worlds-zh.png`, `b5-host-network-zh.png`; Home and Players were read):
  their rows and buttons render at the widths the rule now declares, with no clipped control.

## Residuals for the user

None. The three rows are read verdicts, not taste calls.

## Limits

- Whether eight blocks read as a palette is judged from one host's frames at one resolution; a second
  resolution is not part of this batch.
- The sprite's own left border is one texel, so the two units the fill leaves clear show a thin dark seam
  along a block's left edge (`a7-colour-zoom.png`). It reads as the game's own edge at this scale; whether it
  reads that way at another UI scale is a later run's judgement.
- The window's launcher and the two panels were not driven in this run: the width change reaches them through
  the same rule, and their rendering is recorded as a limit on the sibling ticket rather than judged here.
