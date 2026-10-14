# Acceptance record — the Online UI layout and input detail pass (the Admin page's rows)

- Ticket: `online-ui-layout-and-input-detail-pass` — verdict: **stays in `review/`** (rows 2 and 3 pass; rows 8–10
  remain `unproven` from batch `20261001-k`, and no code is left for them)
- Batch: `20261015-a` — tickets `online-ui-art-and-controls-overhaul`, `online-ui-layout-and-input-detail-pass`
- Commit: `5279880b` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+5279880bf8523f916897b606439b446d26a4781e`
- Run: 2026-10-14 22:5x → 23:2x (the batch's own label is the id its artifacts live under) · Host: physical machine (one client; every row of this batch is a local page rendering)
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `hotrepl`, `capture`, `logs`, `artifacts`
- Artifacts: the `20261015-a` subdirectory of the directory named by `acceptance-artifacts-dir`

## What the row is

Batch `20261014-a` rejected this ticket on rows 2 and 3, both on the Admin page: every rule row's label was one
character wide, stacked down the row's left edge and painted over the rows below, and the rows were taller than
the tab strip. The cycle that answered them is in the ticket's own *Fourth pass (2026-10-14)* section; this run
re-judges the same two rows, in both languages, against the fix.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 2 | Any page: every control is one height and nothing reads as a leftover of the game's own screen | visual | **pass** | `a3-host-admin-zh.png` (Chinese) and `b6-host-admin-en-top.png` (English, page at the top): twelve rule rows, each one line of label at the left and its own control at the right; every row is the tab strip's own 36 units tall, and nothing is drawn under the tabs. The live tree agrees: `a2-admin-zh-dump.txt` reads each row `rect=984.0x36.0` with its label `931.2` and its checkbox `32.8` — the width the game authored for it |
| 3 | A long hint, an English page: wrapped text keeps its own height and never paints over the row below; `Preferences` fits inside its tab; no value is cut mid-word | visual | **pass** | The same two frames: in the English Admin page every label is on one line (`Auto continue`, `Allow remote inventory take`, `Allow member item data write`, `Widen co-op run settings`), nothing overlaps, and the six tabs are as wide as their own captions (`Preferences` included); the two-line hints the page does carry are their own rows and keep their own height |

## Rows this run did not re-judge

- Rows 1, 4, 5, 6 and 7 passed in batch `20261001-k` against the deployed build of that cycle and are unchanged
  by this cycle's code (the shell's stacking, the control height, the dropdown popup, the pointer surfaces, the
  frame's border). They are recorded as `pass` from that batch, not re-judged here.
- Rows 8, 9 and 10 stay `unproven` with the same setup gaps batch `20261001-k` named (a second peer, an
  OS-level drag, a canvas smaller than the frame). No development work is left for them: they need a setup, and
  the ticket stays open on them rather than being closed on a weaker check.

## The family the change reaches, as far as this run read it

The width rule now also gives every button-like element the width its model declares, so the pages this ticket
does not own were captured (`b5-host-home-zh.png`, `b5-host-players-zh.png`, `b5-host-worlds-zh.png`,
`b5-host-network-zh.png`; Home and Players were read): no clipped control, no row overlapping, no page whose
rows read as leftovers.

## Residuals for the user

None beyond the four pages' frames, which any later run may re-read.

## Limits

- One client: every row judged here is a local page rendering of CUO's own window, and no row of these two
  tickets is a cross-client row. The two-panel and world-space surfaces are not driven by this batch, and the
  launcher's idle fade over the medical panel (row 8) needs a session and a peer.
- The frames are one host's window at one resolution; the checkbox's 32.8 units and the slider's 120 read as the
  game's own controls there, and whether they read that way at another UI scale is a later run's judgement.
- The row's own grey frame was removed in the same cycle, after these rows were judged: the frames above
  (`a3`, `b6`) already show it gone, and the user's report that asked for it names the same visual row.
