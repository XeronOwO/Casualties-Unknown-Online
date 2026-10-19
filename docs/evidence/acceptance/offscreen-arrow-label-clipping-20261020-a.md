# Acceptance record — the off-screen arrow's label on all four edges

- Ticket: `offscreen-arrow-label-clipping` — verdict: **moved to `done/`**. Item 4's four edge frames and the
  corner case pass on the deployed artifact; items 1-3 were the implementation cycle's and are covered by
  their own rules and unit cases.
- Batch: `20261020-a` — tickets `item-data-read-commands`, `offscreen-arrow-label-clipping`.
- Commit: `b28ee39d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c9769177d53b0279daa93f05f75b1c513f53a546`. No rebuild: the delta between the artifact's commit and
  this tree's HEAD is documentation-only, and `tools/verify-deploy.ps1` exited 0 against this tree's build
  output before the clients started.
- Run: 2026-10-19 19:46:00 → 19:55:45 (+08:00), by the clients' own log clock · Host: physical machine ·
  Member: `Steam1` sandbox · Bystander: `Steam2` sandbox.
- Dependencies: `game`, `deploy` (verified, not rebuilt), `steam`, `sandboxie`, `sandbox-alt`, `hotrepl` (three
  endpoints), `input`, `logs`, `capture`, `artifacts`.
- Artifacts: `o1-right.png`, `o1-left.png`, `o1-top.png`, `o1-bottom.png`, `o1-corner3.png` and the
  `s3-host-markers-*` / `s3-host-labels-*` readings in the directory named by `acceptance-artifacts-dir`; Steam
  IDs are written `<host>`/`<member>` here.

The frame's own subjects were fixed by the batch plan before the run. A member was frozen (`body-freeze.cs`, a
declared setup substitution: a kinematic body keeps a placement exact) and placed at a chosen world point; the
host's window was captured with `PrintWindow`, and each frame was judged together with the live numbers of the
label boxes the overlay itself placed.

| # | Row (the ticket's item 4) | Class | Verdict | Evidence |
|---|---|---|---|---|
| O1a | Right edge: a long name plus a three-digit distance stays inside the screen | visual | **pass** | member at `(105.000, 499.616)`, distance `105.000`; the camera projected it to screen `(2288.000, 521.856)`, off the right edge; the live label `Kaguya（小号）  105 米` measured `257.400` wide and its box `left 1610.600 / right 1868.000` — its right edge sits exactly on the guarded margin (`1920 - 52`), and its left edge is inside; the arrow `>` was drawn at `(1868.000, 451.970)` (`s3-host-markers-right.json`, `s3-host-labels-right.json`, frame `o1-right.png`) |
| O1b | Left edge: the same label stays inside | visual | **pass** | member at `(-105.000, 499.616)`, distance `105.000`, projected to `(-1072.000, 521.857)`; the label box `left 52.000 / right 309.400` — pinned exactly on the left guarded margin; the arrow `<` at `(52.000, 455.166)` (`s3-host-markers-left.json`, `s3-host-labels-left.json`, frame `o1-left.png`) |
| O1c | Top edge: the label is not clipped and does not cover its arrow | visual | **pass** | member at `(0.000, 604.616)`, distance `105.001`, projected to `(608.000, 2201.856)`; the arrow `▲` at `(955.529, 52.000)` (its own box spills above the margin line by design) and the label box `top 72.000 / bottom 92.000`, below the arrow and inside the guarded rect (`s3-host-markers-top.json`, `s3-host-labels-top.json`, frame `o1-top.png`) |
| O1d | Bottom edge: the label goes above the arrow rather than past the border | visual | **pass** | member at `(0.000, 394.616)`, distance `105.000`, projected to `(608.000, -1158.144)`; the arrow `▼` at `(954.700, 1028.000)` and the label box `top 988.000 / bottom 1008.000` — above the arrow and inside the guarded rect, exactly the asymmetry the ticket's own Limits named (`s3-host-markers-bottom.json`, `s3-host-labels-bottom.json`, frame `o1-bottom.png`) |
| O1e | A member at a corner is two clamps at once | visual | **pass** | member at `(208.000, 599.616)`, distance `230.790` (read as `231 米`); projected to `(3936.000, 2121.856)`; the arrow `>` landed at `(1868.000, 52.176)` — the right margin and the top margin at the same time, within 0.18 units of the corner, with the label below it (`right 1868.000`, `top 72.176`) (`s3-host-markers-corner3.json`, `s3-host-labels-corner3.json`, frame `o1-corner3.png`) |

## What the run adds beyond the rows

- **The clamp's own numbers are readable live**: the overlay's pooled marker texts are named `Marker Label` /
  `Marker Glyph`, so a probe can report each label's text, its measured width and its box in the geometry
  rules' GUI coordinates. The frames and those numbers agree on every edge (the label's outer edge sits exactly
  on the 52-unit margin line, never past it).
- **The canvas is 1920x1080 design units on a 1280x720 window** (scale 0.667): a pixel read of the frame is not
  a canvas-unit read, and the margin line is 52 canvas units, not 52 pixels. The label's measured width
  (`257.4` units) is far wider than the guarded room at a side edge (`104` units), so a centred placement would
  have run the text past the border — the defect the ticket reports — even with this name.
- **The bottom edge is visibly asymmetric**: the arrow's centre is pinned on the margin line (its own box
  spills), and the label sits above it rather than below.

## Residuals for the user

None. The frames are the host's own window; every verdict above is a layout fact, not a taste.

## Limits

- **The name is the longest persona this session could show** (`Kaguya（小号）`, 9 characters). A Steam persona
  is account state and this run did not change one, so a longer name could not be staged; the label was still
  wider than the side edges' room, which is the condition the ticket's defect needs.
- **The label-wider-than-the-whole-canvas branch (`Pinned`) was not reached** — that would need a label wider
  than 1816 canvas units; its behaviour is the rule's own unit case, and no frame here claims it.
- **Frames are window-level captures of the host's window including its borders** (1298x767 for a 1280x720
  client area); the label boxes quoted above are canvas units (1920x1080) and the comparison with the frames
  uses the 0.667 scale.
- **The members' bodies were frozen for the frames** (kinematic rigidbody, restored afterwards): without it a
  body placed in mid-air falls ~15 units/s, which would move the projected point between the capture and its
  machine reading. The overlay's own geometry, projection and label placement are the product path under test.
  The probes are batch-local artifacts: `body-freeze.cs` (hold the body), `marker-read.cs` (entity positions,
  distances, projections) and `marker-label.cs` (the live label and glyph boxes).
- **The language on the frame is the host's** (Chinese, `105 米`); no row here judges localized text.
