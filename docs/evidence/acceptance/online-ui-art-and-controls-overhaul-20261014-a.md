# Acceptance record — the Online UI art and controls overhaul (the colour picker's blocks)

- Ticket: `online-ui-art-and-controls-overhaul` — verdict: **back to `todo/`**,
  `- Status: Todo — Rejected (the palette blocks do not read as a palette)`
- Batch: `20261014-a` — tickets `item-data-write-commands`, `validation-and-source-checks`,
  `online-ui-art-and-controls-overhaul`, `online-ui-layout-and-input-detail-pass`
- Commit: `16c0d603` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+16c0d6034766394af57fa88106a185f2e4f47fbf`
- Run: 2026-10-14 22:3x · Host: physical machine (the CUO UI language was switched to Chinese for this
  run through the same config-entry path the Preferences dropdown calls) · Guest: `Steam1` sandbox
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `capture`, `input`, `logs`,
  `artifacts`
- Artifacts: the `20261014-a` subdirectory of the directory named by `acceptance-artifacts-dir`

## What the row is

The ticket recorded this residual itself, in its own words: "**How a block reads is the user's run.** The
block is the game's own button row tinted, so the sprite's own colour multiplies the fill — whether eight
of them read as a palette at the player's canvas scale, and whether the tints are recognisable, is a game
observation." The user answered that residual during this run, on the frame below, and the answer is no.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The colour picker's palette blocks read as a palette of colours | visual | **fail** | `c2-host-preferences-zh.png` (the Preferences page, full frame) and `c3-colour-area-zoom.png` (the same frame's colour area at 2×). Nine blocks are drawn in one row; each is the game's own button sprite with the colour laid over its graphic, so the **border** takes the colour and the middle stays transparent — the user's own reading ("only the button border colour, the middle transparent") on the frame, and the reason the row does not read as eight or nine colours |
| 2 | A block's own size is the size the drawer declares | machine | **fail** | Measured from `c2-host-preferences-zh.png` by `measure-swatches.ps1` (the batch's own measuring script): the CUO window's own frame edges span 666 px, which is the 1000 units `OnlineUiWindowLayout` declares, so the frame measures **0.666 px per unit**; the palette row's blocks then measure **12 px = 18.0 units** wide each. The drawer declares `private const float SwatchWidth = 88f` (`src/CasualtiesUnknownOnline.Plugin/OnlineUiPreferencesDrawer.cs`), so a block renders at about a fifth of its declared width and reads as a sliver rather than a block. The live preview block beside "current" is the same shape against its own `PreviewWidth = 64f` |
| 3 | The picker fits the page it is drawn on | visual | **fail** | In `c2-host-preferences-zh.png` the palette row is the last visible row of the page's area and the `Auto` control the drawer adds below it is not visible at this window size; the row reads as cut off rather than as a picker |

## The user's judgement, in their own words (2026-10-14, during this run)

The "current" area shows a semi-transparent rectangle, and the colour buttons below it are very narrow with
only the border coloured and a transparent middle — "it looks strange" — and the user's decision was to
reject the ticket now and fix it in a later cycle rather than in this acceptance run.

## Why the ticket moves rather than this being a new ticket

The residual is the ticket's own, recorded with the delivery; the acceptance workflow's rule 9 ("a
rejected residual returns that ticket to `todo/` with the same rejection marking as a failed row") is what
this verdict applies. The measurement above is new evidence, not a new defect class.

## Residuals for the user

None beyond the judgement already given.

## Limits

- The measurement is taken from one captured frame at the host's resolution and window size; it is a
  reading of that frame, not a sweep of every resolution.
- The exact mechanism that leaves the block at 18 units instead of 88 (the layout system's own measurement
  of a caption-less button, or the row's group) was not diagnosed in this run — acceptance names the
  observation and its size, the fix cycle owns the cause.
