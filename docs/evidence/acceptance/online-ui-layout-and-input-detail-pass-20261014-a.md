# Acceptance record — the Online UI layout and input detail pass (the Admin page's rows)

- Ticket: `online-ui-layout-and-input-detail-pass` — verdict: **back to `todo/`**,
  `- Status: Todo — Rejected (the Admin page's rule rows collapse their labels to one character per line)`
- Batch: `20261014-a` — tickets `item-data-write-commands`, `validation-and-source-checks`,
  `online-ui-art-and-controls-overhaul`, `online-ui-layout-and-input-detail-pass`
- Commit: `16c0d603` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+16c0d6034766394af57fa88106a185f2e4f47fbf`
- Run: 2026-10-14 22:3x · Host: physical machine · Guest: `Steam1` sandbox
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `capture`, `input`, `logs`,
  `artifacts`
- Artifacts: the `20261014-a` subdirectory of the directory named by `acceptance-artifacts-dir`

## What was read

The page the earlier judgements never photographed: the Online UI's **Admin** page, in the same session,
twice — once with the CUO UI language set to Chinese and, minutes later, with it set back to English, so
the two frames are the same page, the same build and the same session.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 2 | Any page: every control is one height and nothing reads as a leftover of the game's own screen | visual | **fail** | `k1-host-admin-zh.png` and `m2-host-admin-en.png`. The page's rule rows are wide empty boxes with their label drawn down the left edge one character per line ("P", "A", "A", "A", "A", "W", "P", "R" in the English frame); only the two rows whose control is the game's own dropdown ("Native binding parity" with its `Warn` box) render their label on one line. The rows are visibly taller than the tab strip's own height, which is the height the pass's decision fixed for every control |
| 3 | A long hint, an English page: wrapped text keeps its own height and never paints over the row below | visual | **fail** | The same two frames: each rule row's label paints over the rows below it, and the stack of characters reads as one vertical smear from "HOST RULES" to the next section rather than as a list of settings. The English frame satisfies this row's own scenario wording ("an English page") and still fails it, so this is not a translation problem — the two frames are the A/B that rules that out |

The user's own report during this run was about the Preferences page's colour picker; the Admin page was
captured while the colour picker's A/B was being taken, and it is the same pass's subject (the geometry the
game's own settings rows are placed in).

## Why the ticket moves

Rows 1–7 of this ticket were judged `pass` in batch `20261001-k`, and rows 2 and 3 are page-agnostic ("Any
page"). A page on which every rule row's label collapses to one character per line is a failed row, and the
acceptance workflow's rule 8 is what this verdict applies: the ticket goes back to `todo/` with the failing
rows named, and the fix is ordinary development work.

## Residuals for the user

None. Both frames are read verdicts, not taste calls.

## Limits

- The frames are the Admin page at one resolution and window size, with the page scrolled where the capture
  found it; the other five pages were not re-photographed in this run (their rows were judged earlier).
- The cause (the rule row's label box being sized to nothing, or the row's own group) was not diagnosed
  here; acceptance names the observation, the fix cycle owns the cause.
