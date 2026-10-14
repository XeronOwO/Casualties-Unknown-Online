# The Online UI's layout and input detail pass

- Status: Review — rows 2 and 3 are fixed (2026-10-15, see *What landed — the width a control is given* below);
  waiting for the acceptance batch that re-judges them.
  **Rejected 2026-10-14 in batch `20261014-a`** (`docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261014-a.md`): rows 2 and 3 of the table below fail on the Admin page in BOTH languages — each rule row's label is one character wide, stacks down the row's left edge and paints over the rows below it, and the rows are taller than the tab strip's own height. The page was never photographed by the earlier judgements, which stand for the pages they read.
- Re-judged before that (batch 20261001-k: the control internals ARE laid out now;
  rows 3, 5, 6 and the window third of row 11 pass against the deployed build)
- Acceptance (20261001-k): rows 1, 2, 3, 4, 5, 6 and 7 pass; row 11 passes for the window half (the quick
  panel, the context menu and the overlays need a session) → the session half stays unproven; rows 8–10
  unproven (setup gaps: a second peer, an OS-level drag, a canvas smaller than the frame) — record
  `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261001-k.md`
- Acceptance (20261001-j, the rejection this pass answers): rows 1, 2, 4 and 7 pass; rows 3, 5, 6 and 11
  fail (unlaid-out control internals); rows 8–10 unproven — record
  `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261001.md`
- Priority: High
- Category: Online UI / layout, presentation and input
- Source: User acceptance pass (2026-09-27), on the overhaul ticket's own delivery. The findings in the
  user's words, one per row: the body of a page needs room from the top tab row; the button sizes need work;
  the dropdown "clips through" (`下拉框穿模`); an open dropdown's options appear behind the UI, are covered
  and cannot be clicked; the whole interface could be a little larger — the frame, not the text, because a
  page holds a lot of information and is too small for it; in the English UI `Preferences` is longer than its
  tab button; the colour page is misaligned in several places and the palette the user expected is not there;
  most of the text boxes in the interface cannot be clicked at all, which the user suspected was a UI
  layering problem and asked to be investigated first; the hex field does not show the hex of the colour the
  player has just picked, which it should, live; and the game's own windows have a white border, which the
  Online UI's window does not.
- Related: `review/online-ui-art-and-controls-overhaul.md` (the migration this pass reviews),
  `done/cuo-launcher-button-obscures-the-view.md` (the idle fade this pass found still too opaque),
  `done/online-ui-panels-request-alpha-blend-false.md`, `done/remove-the-online-ui-console-page.md`

## The gap

The overhaul put the game's own controls on the game's own canvas and that part is accepted — the style now
reads as this game. What it did not do is re-cut the geometry those controls are placed in. Every row of a
page is still one of the game's settings rows instantiated into a window it was never authored for: the
prefab places its own children by hand for the game's own full-width settings screen, the window's shell
still places its bands with the first cut's own sum, and the control sizes are the fixed hints a Chinese
window needed. The user's run found what that produces — overlapping text, boxes that cannot be clicked,
options behind the window, and a frame with no border.

The findings, each with the code fact behind it:

| # | Finding (user, 2026-09-27) | Cause read from the code |
|---|---|---|
| 1 | The body of a page touches the tab row | `OnlineUiWindowView` placed the page's top edge at `TitleHeight + TabHeight + 2 × LineSpacing` = 66, while the tab strip's own bottom edge is `Padding + TitleHeight + LineSpacing + TabHeight` = 70: the page started four units INSIDE the strip, and the frame's own padding was missing from the sum |
| 2 | Button sizes | Every control took the game's row prefab's authored `sizeDelta.y` (the game's settings rows are tall), while the tabs were forced to 30: one page showed two very different control heights |
| 3 | A dropdown clips through its neighbours | The row's own children keep the prefab's hand-placed rects, and a multi-line label was laid out one line tall, so its wrapped text painted over the row below |
| 4 | An open dropdown's options are behind the window and cannot be clicked | `TMP_Dropdown` builds its list from a template INSIDE the row, so the list is a child of CUO's page: clipped by the page's `RectMask2D`, laid out by the page's groups and drawn under every row after it |
| 5 | The interface could be larger (the frame, not the text) | The window's rect is a fixed 780×540 and nothing clamps or scales it |
| 6 | `Preferences` runs past its tab button | The tab width is a fixed 112-unit hint; the caption is the game's font, so an English label needs more room than a Chinese one |
| 7 | The colour page is misaligned and its palette cannot be found | The colour rows mix a flexible label, a fixed-width field and a row of blocks in one line; the "current colour" preview lives beside its name, and the blocks are unlabelled |
| 8 | Most text boxes cannot be clicked (priority: investigate) | uGUI hit-tests GRAPHICS: a field whose box carries no raycast-target graphic, and whose text is not one either, shows its value and answers nothing — and the field's own viewport may still hold the prefab's authored rect rather than the box CUO sized |
| 9 | The hex field does not follow a picked colour | The field's model value was `PlayerColorInput ?? StoredHex`, and `StoredHex` is empty for the automatic colour: a palette pick left the box showing whatever the label-selected colour's stored text was |
| 10 | The game's own windows have a white border, this one does not | The frame's image was tinted CUO-dark, which paints the sprite's own border away — the sprite is what carries the game's border |
| 11 | The spacing is not only wrong at the top edge: some section headings have no room above or below them (user, same pass) | A page's vertical rhythm was one 4-unit gap plus a `page.Space()` each drawer had to remember: a heading owned no room of its own, so it read as glued to whatever preceded it |

## What done looks like

| # | Scenario | Expected |
|---|---|---|
| 1 | Any page, scrolled to the top | The first row sits below the tab strip with a visible gap; nothing is drawn under the tabs |
| 2 | Any page | Every control is one height, close to the tab strip's; nothing reads as a leftover of the game's own screen |
| 3 | A long hint, an English page | Wrapped text keeps its own height and never paints over the row below; `Preferences` fits inside its tab; no value is cut mid-word |
| 4 | Open the log-level or language dropdown | The list draws above the whole window, is not clipped by the page, and every option can be clicked; choosing one applies at once |
| 5 | Click any text box (hex, template name, lobby id, address, port, player name) | It takes the caret and typing edits the value; the hex box shows the colour picked from the palette in the same frame |
| 6 | Pick a palette colour, then Auto | The hex field shows the canonical hex of the colour carried now in both cases, and the "current" line says which one it is |
| 7 | Look at the window's edge | The game's own border is visible around the frame |
| 8 | Leave the launcher alone in the top-right corner over the medical panel | It fades to a ghost and the panel's readout stays readable; hovering brings it back, and it stays clickable in both states |
| 9 | Drag the title bar towards an edge | The window follows, and a strip of it always stays on the canvas so it can be grabbed again |
| 10 | A canvas smaller than the frame (low resolution, large game UI scale) | The frame is clamped to the canvas instead of hanging off it |
| 11 | No regression | The launcher, all six pages, the quick panel, the context menu and the world-space overlays behave as before; the gates and the pins stay green |

## Decision (2026-09-27, technical calls recorded rather than asked)

- **The frame grows, the text does not.** The user's answer was explicit: "the whole interface frame can be a
  little larger, but the text and the controls inside it need not grow — the problem is that there is a lot
  of information and a small page". The window is therefore 1000×700 (`OnlineUiWindowLayout.Width/Height`,
  the first cut's 780×540 plus room), the control height stays at the tab strip's own 30, and the fonts are
  still the game's own.
- **The ENGINE sizes, CUO declares only floors.** Language decides how wide a control must be, and the
  plugin's constants were tuned for Chinese; the answer is not a better CUO computation but the layout
  system's own measurement. A control now carries a layout group over its own content (a button over its
  caption, a dropdown over its caption and arrow, a field over its text) and reports what uGUI asks it for;
  the view declares the model's width as `LayoutElement.minWidth` and reads the engine's answer back
  (`LayoutUtility.GetPreferredSize`) for the wrap. The model's width is therefore a FLOOR and nothing more.
- **The ENGINE stacks the shell.** The title bar, the tab strip and the page are children of one
  `VerticalLayoutGroup`: each declares its own height (the page is the flexible band) and the layout's own
  spacing is the gap between the bands. No band's position is computed anywhere — the arithmetic that put the
  page inside the tab strip is deleted, and a band added later cannot be forgotten by it.
- **One compact control height.** Buttons, dropdowns, fields, toggles, sliders and colour blocks share
  `OnlineUiWindowLayout.ControlHeight` (the tab strip's 30); only labels are their own height, because only
  labels know how many lines they need.
- **CUO owns the geometry INSIDE one of the game's rows.** The prefab's hand placement belongs to the game's
  own screen, so the row's label and control are laid out by a horizontal group CUO puts on the row, and a
  dropdown's caption and a field's viewport and text are stretched onto the box CUO sizes. The game's art —
  sprite, 9-slice, font, the control components themselves — is untouched; only rectangles are CUO's.
- **A label is as tall as its wrapped text.** The label keeps a one-line floor in `LayoutElement.minHeight`
  and leaves `preferredHeight` to TMP, which is the layout element that knows the wrapped height.
- **An open dropdown's list is an overlay.** One popup layer on the window (a canvas with its own raycaster,
  sorted one step above CUO's surface); every dropdown hands its template to it, so a list is instantiated
  outside the page's mask and groups and inherits the layer's sorting; the layer re-asserts the order once
  per frame the window is applied.
- **A control the game left unclickable is given a pointer surface and REPORTED.** The field's or the
  dropdown's own object must carry a graphic that accepts the raycast; when CUO has to add or enable one, the
  window logs it once per kind, because a box nobody can click is a defect that must not pass silently.
- **The colour field mirrors the colour, live.** The field's text is the canonical hex of the colour the
  player carries now (`PlayerColorInput` only holds text that is not a colour yet), so a palette pick, Auto
  or a typed value all show up in the same frame; the palette and the hex field stay as they are — the user
  decided this pass needs no richer picker.
- **A page's vertical rhythm is one scale, and a heading owns its own room.** The page was purely "computed"
  only in the sense that every gap was either the layout group's 4 units or a `page.Space()` a drawer had to
  remember; the user's report was that some headings sit flush against the row above them. The rhythm is now
  `OnlineUiWindowLayout`'s own numbers (`RowGap`, `SectionGap`, `SectionBodyGap`, `BlockGap`), the builder's
  `Section` writes the room above and below a heading itself, and the ten call sites that compensated with a
  manual `page.Space()` before a heading are gone. Underneath, the layout is still uGUI's own system — a
  `VerticalLayoutGroup` for the page, a `HorizontalLayoutGroup` per line, `LayoutElement` preferences, TMP's
  own measurement for text height, a `ContentSizeFitter` for the content and a `ScrollRect` with a
  `RectMask2D` viewport — with three things that system cannot do left to CUO: per-child margins (hence a
  space ROW rather than a margin), wrapping (uGUI groups never wrap, hence `OnlineUiRowLayout`), and the sizes
  the game's own row prefabs were never authored with.
- **The frame keeps the game's border.** The frame's own image keeps the game's sprite untinted and the dark
  surface becomes a fill laid inside it; the panels get the same treatment through one helper, because they
  are the same window family.
- **The launcher's idle floor drops to a ghost.** `OnlineUiLauncherFade.IdleAlpha` 0.35 → 0.12 and the idle
  window 4 s → 2.5 s: the user's report is that the top-right corner holds the medical panel's readout, so
  the launcher must give it back. Hover still restores full opacity and the rectangle and the click are
  unchanged (the launcher ticket's own contract).
- **Self-found in the same pass, recorded rather than asked:** the title bar's drag now keeps a strip of the
  window on the canvas (a larger frame is easier to lose), and the window's rect is clamped to the canvas it
  hangs on (a canvas smaller than the frame must still hold the whole page). The tab row and the Home page's
  transport switch show their current choice as a wash on the game's own sprite, not only as a coloured
  caption.

## Stages

1. **S1 — the shell's layout and the frame.** `OnlineUiWindowLayout` (Runtime, pure) holds the window's size,
   the padding, the band heights, the one control height and the page's rhythm; the window view puts the
   title bar, the tab strip and the page into ONE `VerticalLayoutGroup` and declares each band's height, so
   the engine stacks them and the first cut's arithmetic (the defect that put the page inside the tab strip)
   no longer exists to be wrong. The frame is clamped to the canvas. Red: `OnlineUiWindowLayoutTests` — the
   first cut's own inset is written out as the negative sample the rule rejects.
2. **S2 — the engine's geometry.** Every control carries a layout group over its own content, so uGUI
   measures the width and CUO declares only the model's floor; the window's wrap is asked with the widths the
   engine reports; every control takes the shared height; a label's height is TMP's own wrapped height over a
   one-line floor. `OnlineUiRowGeometry` takes over the inside of a row: a horizontal group on the row, the
   label flexible, the control measured by the engine above its floor, a slider's value after it.
3. **S3 — the dropdown popup and the pointer surfaces.** `OnlineUiDropdownPopup` (the window's popup layer,
   sorted above CUO's canvas) adopts every dropdown's template, and normalises the open list once per frame;
   `OnlineUiRowGeometry.EnsurePointerSurface` guarantees a clickable box for a dropdown and a field, and the
   window reports a fix once per kind.
4. **S4 — the frame's border.** `OnlineUiControlFactory.MakeFrame` keeps the sprite untinted on the frame and
   lays the fill inside it; the window and both panels use it.
5. **S5 — the colour field and the launcher's floor.** The hex field shows the carried colour's canonical
   text, live, and drops its own text when a choice is applied; `OnlineUiLauncherFade`'s idle floor drops to
   0.12 with a 2.5 s window.
6. **S6 — the page's rhythm (from the same pass).** `OnlineUiWindowLayout` gains the spacing scale
   (`RowGap`/`SectionGap`/`SectionBodyGap`/`BlockGap`), `OnlineUiRowModel` gains a space row that carries its
   own room, the window and the panels honour the room a row asked for, and `OnlineUiPageBuilder.Section`
   writes the room above and below a heading itself — the ten manual gaps that compensated are deleted.

## Pins and tests

- `OnlineUiWindowLayoutTests` (Runtime, pure): the page clears the tab strip, the first cut's arithmetic
  would not, the bands stack from the top edge, the frame holds the page, the frame is larger than the rect
  it replaces, every control shares the tab strip's height, the page's rhythm separates a heading from what
  it opens, and a space row carries only room.
- `OnlineUiLayoutDetailPinTests` (new): nine facts + sixteen real-source mutation rows for the shell's
  placement, the one control height, the measured widths, the wrapped-label height, the frame's border, the
  row's inside geometry, the popup layer, the pointer surfaces and the heading's own room.
- `OnlineUiWindowSurfacePinTests`: `WritesOnlyChanges` re-anchored to the new layout write (the width is now
  the measured one); the remaining pins are untouched and green.
- `OnlineUiColorPickerPinTests`: `TheFieldMirrorsTheColourThePlayerCarries` added with its mutation row.

## Independent review dispositions (2026-09-27)

The adversarial review (`%TEMP%/cuo-review-ui-detail.md`, session artifact) ran against the frozen tree and
found four things, all fixed in the same round, plus findings it judged sound:

- **Fixed: an element that shared its line took the line's surplus.** The review read `FillsTheRow` as one
  write overwriting another; the two writes were in fact on two different objects (the element's own root and
  the control inside it), but its underlying case was real — a field or a dropdown sharing a line with a label
  was flexible too, so the two split the surplus and the field's floor stopped meaning anything. The rule is
  explicit now: a label always takes what its line has left, and one of the game's own rows takes the line only
  when it is ALONE on it (`aloneOnItsLine`).
- **Fixed: a checkbox's box kept the prefab's authored spot** while the control was forced to the compact
  height; `OnlineUiRowGeometry.PrepareInternals` centres a toggle's own box in its control. A slider needs no
  equivalent, and that is recorded in the code, because uGUI's `Slider` positions its own fill and handle from
  the slider's rect — the reason only the toggle needed the help.
- **Fixed: the Worlds page's heading was built as a raw row**, so it was the one heading without the new room.
  `OnlineUiPageBuilder.Section` gained the trailing controls a heading's line carries.
- **Fixed: the frame was fitted to the canvas once**, so a canvas that changed size afterwards (a resolution
  change, the game's own UI scale) left the frame hanging off it; the fit is re-measured when the canvas rect
  changes.
- **Checked, sound: the colour row's wrap prediction** (a label counts as nothing towards the wrap because it
  takes the line's leftover — that is the rule, and the row lands where the engine puts it), **and the drag
  clamp** (the review re-derived it after first suspecting it and confirmed it keeps the intended strip of the
  window on the canvas at every canvas size).
- **Tightened pins at the review's suggestion:** the colour-field pin now matches the field's own call shape
  instead of a bare token; the border pin covers the panel as well as the window; and the pin that replaced
  the game's-row size contract carries behavioural mutation rows (a control that fills a shared line, a content
  group that stretches its caption) instead of a comment-only one.

## Limits recorded with this pass

- **The pixels are the user's run.** No test in this tree instantiates a `GameObject`: whether the game's row
  prefabs behave under CUO's group, whether the popup lands where the player expects, whether the border
  reads like the game's own and whether the frame is now the right size are game observations.
- **The click fix is structural and instrumented, not reproduced.** The defect was reported from a run this
  tree cannot make. What is proven here is the shape: the box CUO sizes, the raycast surface on it, and a
  warning when the game's prefab left none. If a click still fails, the first thing to read is that warning.
- **uGUI has no wrapping layout, so one decision stays CUO's.** A `HorizontalLayoutGroup` lays a line out
  and never wraps, and `GridLayoutGroup` is cell-based; the grouping of a row's elements into lines is
  therefore decided by the Runtime's own rule (`OnlineUiRowLayout`) and executed with one real layout group
  per line, which is where the engine takes over again (flexible widths, alignment, sizes). That is the
  boundary of "the engine does the layout" in this tree: everything else is declared, measured or placed by
  uGUI.
- **`TMP_Dropdown`'s own parenting is not visible from this tree.** The template is re-parented and given its
  own canvas, sorting order and raycaster, and the open list is normalised every frame, so the outcome does
  not depend on which of the two parents the library picks; which one it picks is still unverified here.
- **The popup layer covers the window, not the screen.** A list longer than the window's own rect is not
  clipped by the layer (it has no mask), but its position is computed by the game's own code from the
  dropdown's rect, so a dropdown within a few units of the window's bottom edge may still open over the
  frame's lower border — a cosmetic difference to watch in the run.
- **The colour field's new rule is deliberate.** The box is never empty now: with the automatic colour it
  shows the resolved automatic hex, and the "current" line is what says `Auto`. Cleared text therefore means
  "give me the automatic colour", which is also what the Auto control does.
- **The two panels have no popup layer of their own.** The window owns the layer a dropdown's list rides; a
  dropdown added to the quick panel or the context menu would need the same treatment, and neither panel
  carries one today (the review's latent finding, kept as a limit rather than built for a case that does not
  exist).
- **The drag clamp is not pinned.** It is a small recovery guard (a strip of the window always stays on the
  canvas); no test holds its constant, and the felt behaviour is a run observation.

## Second pass (2026-09-27): the tab strip's height and the title

The user ran the deployed window the same day and found two things this pass had not settled:

- **The tab strip kept the WINDOW's height, not TabHeight's.** Every band declares its own height
  (`DeclareBandHeight`), but the height a layout group *reports* is composed, not declared: the tab row's
  own `HorizontalLayoutGroup` reported its children's forced-flexible sum, so the shell's vertical group saw
  a flexible band and fed it the window's leftover height — the strip rendered 333 units (ten times
  `TabHeight`) on both clients, with the tab buttons stretched to it. `DeclareBandHeight` now zeroes the
  band's flexible height too, so only the page's own band (set beside it) takes the leftover room;
  `OnlineUiLayoutDetailPinTests.TheTabStripKeepsItsOwnHeight` pins it with its mutation.
- **The window title read the game's Chinese name reversed** (`伤亡未知`); the official name is `未知伤亡`.
  The catalogue's `window.title` carries it now and `LocalizationServiceTests` pins the string.
- **The compact height was one size too tight for the game's Chinese font.** At `TabHeight = 30` the tab
  captions rendered 27.9 units tall (the live probe's own reading), leaving about a unit of room at each
  edge; the user's second look called it out ("the Chinese text touches the bottom"). `TabHeight` and
  `ControlHeight` are 36 now — the same single compact height for the strip and every page control, with
  room for the font's own line box.

Verified on the deployed build: a probe of the live rects reads `Tabs = 984x36`, every tab `112x36` (the
compact height the second look raised from 30), and the English `Preferences` `169.2x36` by its own longer
caption; both clients' frames were captured and read, and a create/join smoke passed. Evidence: the local
run artifacts (`ui-before-*`, `ui-experiment-*`, `ui2-fixed-*`, `ui3-*`) in the directory `AGENTS.local.md`
names, captured per client window.

## Third pass (2026-10-01): the width is declared, and a control shows one box

Batch `20261001-j` rejected this ticket: a text field's value area read 0×0 and its own rect read
hundreds of thousands of units wide, so a picked or typed value was never painted. This pass found the
mechanism and reworked the two decisions that carried it (commit `968edee8`).

- **The mechanism: the measurement fed itself.** uGUI measures a layout group's child by walking that
  child's own content, and content that is STRETCHED inside the box reports the width the group just
  wrote. Asking for a field's preferred size therefore reads the group's own output. The batch `k` probe
  caught it running away: the same dropdown measured **690,623** units wide inside a 984-unit page and
  **849,678** a couple of minutes later — doubling every frame or two, while the value it held was correct
  the whole time. The earlier "the engine sizes, CUO declares only floors" decision is what made that
  possible: it asked the engine to measure exactly the thing that cannot be measured.
- **The width is DECLARED now.** A button is as wide as its own caption's text (TMP measures the text
  rather than the rect, so this is stable) plus the room the game's row keeps around it; a toggle, slider
  or field answers from the size its prefab was authored with; a dropdown and a field keep the layout's
  own minimum under the model's floor, because a control the player types into cannot be zero wide.
  `OnlineUiControlBox` (Runtime, pure) holds the rule and its tests.
- **A control's insides are placed one rect at a time.** A container fills the box less the row's room, a
  caption keeps its own text width at the left edge so it never runs under the row's art, and the row's
  art (a dropdown's arrow) keeps its size at the right edge. Nothing in that pass asks a stretched child
  how wide it would like to be, and a layout group or fitter left on one of those children is removed.
- **The row's own label and control are still laid out by the row's group**, because that group is the
  one place where measuring is safe: its children are the row's label and a control whose width CUO has
  already written down. The label stays flexible and the control keeps the declared width.
- **One control shows ONE box** (the user's calls of the same day: "the grey box looks strange, can it go
  — and unify the style"). The game's own rows bring their own boxes — a dropdown's white frame, and an
  integer row's grey fill UNDER it spanning the whole row, which is what the user saw as a grey border
  with a margin nobody chose. Those are hidden now, and the control paints a dark fill with a hairline
  inside its edge, so a field reads as the same family as a button on every page.
- **The hairline is four one-pixel lines, not the game's sprite.** The first attempt drew the edge with
  `uiBlockNano` left untinted — the trick the window's own frame uses — and failed visibly: that sprite's
  BODY is opaque, so the box came out white with no text (`style1-preferences.png`) and the frame rate
  fell to 12. The style needs a line, and a rect is what the pass already owns.

Verified on the deployed build (`968edee8`, `verify-deploy.ps1` exit 0): every control 150–200 units wide
(the runaway is gone), a field's text area `137.1×16` with its value painted, six tabs sized by their own
captions (`Preferences` 169.2), `controls=4 worstControlWidth=200 zeroInteriors=0`, the six pages
reachable, and the frame rate unchanged at 160–166. Evidence: `.acceptance/batch-k/` (`baseline-internals.json`
for the runaway, `final-internals.json`, `final-values.json`, `final-census.txt`, `final-buttons.json`, and
the page frames), and the record `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261001-k.md`.

## Fourth pass (2026-10-15): the width a control is given

Batch `20261014-a` rejected rows 2 and 3 because every rule row on the Admin page drew its label one character
per line. The cycle that fixed it read the live geometry first (batch `20261015-a`, probe in
`.acceptance/20261015-a/`), and the reading is the defect:

- **The rows had grown without bound.** Each rule row measured `7282.9 × 36` units, the last one `30537.1`
  units, inside a page that is 984 wide; the page's own content reported a preferred width of 30787. The
  label inside a row measured `0.0 × 36` while the toggle beside it held `7327.1` — the sum is what the row's
  group reports, and the label, whose minimum is zero, got nothing.
- **The cause is one reading of the wrong object.** `OnlineUiControlView.EffectiveWidth`'s fallback branch — the
  one a TOGGLE and a SLIDER take — asked `PrefabWidth(_rect)`, and `_rect` is the element ROOT. A row's preferred
  width is its own layout group summing its children, so it CONTAINS the control's declared width: every frame
  wrote `W = W + RowPadding×2 + RowGap + label width` into the control and the row grew by that much again. The
  widths on one frame were 7282.9 / 13587.1 / 19237.1 / 24887.1 / 30537.1 for labels of 44.2 / 100 / 150 / 200 /
  250 units, and the step between the widths over the step between the labels is 112.98 — the recurrence above,
  one step per frame. (The probe records no frame counter, so how many frames it ran for is derived from those
  widths, not measured.)
- **The width now comes from the control, read once.** `OnlineUiControlSizing.AuthoredChildWidth` reads the
  width the game authored for the control off the CONTROL's own rect, at the one moment it is intact — the view
  is built before the row's group writes it — and only when the game anchored that child at a point: the float
  row's slider is stretched across the game's own row and therefore declares no width of its own, so the model's
  floor answers for it. The game's own numbers, read from the prefabs in the same run: `Special/GameSettingBool`
  is a label plus a `32.8 × 32.5` checkbox at the right edge, `GameSettingDropdown`'s box is 160 wide,
  `GameSettingInt`'s field 223.1, `GameSettingFloat`'s value text 73.5 beside a stretched slider.
- **Every width rule is one rule now.** `OnlineUiControlSizing.EffectiveWidth` takes the element, the caption,
  the game's type size and the control's authored width and answers for all seven kinds — including the
  button-like kinds' `CaptionWidthOrZero` — and the view passes what only it knows. The read that had no caller
  left (`EffectiveWidth(element, contentWidth)`) is gone with it, and the row's own geometry entry point moved to
  `OnlineUiRowGeometry.LayOutRow`, where the row-inside geometry already lived.
- **The shared typography is a value of its own** (`OnlineUiControlTypography`): four surfaces build their text
  from the one reading the factory takes off the game's row prefab.
- **What the rule gives each kind, and what moved with it.** A toggle takes the size the game authored for its
  checkbox (`32.8`) and nothing else; a slider keeps the layout's own minimum under the model's floor, because a
  control the player drags cannot be zero wide (the rule gained that floor in this cycle — the only slider
  declares `NumberRuleWidth = 120`, so nothing visible moves); a field and a dropdown are as they were; a button
  or a colour block takes its caption plus the game's own room, or the model's floor when that is wider, and the
  window's tab row now routes its tabs through the same rule instead of leaving `OnlineUiPageBuilder.TabWidth`
  unread (recorded on the sibling ticket). An element whose game prefab could not be loaded has no control child
  either, so it takes the declared width as well — the placeholder path is written down in
  `OnlineUiRowGeometry.LayOutRow`'s doc rather than left implicit. The consequence on the surfaces no test can
  render — every button-like element of every page and panel is now as wide as the rule declares instead of as
  wide as its caption group measured — is recorded with its acceptance plan on the sibling ticket.
- **The pins.** `AControlOfARowIsAsWideAsTheGameAuthoredIt` holds the capture and the rule (a mutation row
  restores `PrefabWidth(_rect)`, another removes the authored width from the rule);
  `AnElementThatIsItsOwnControlDeclaresItsWidth` holds the element's own width write;
  `WidthsFollowTheContent` gained `!PrefabWidth(_rect`, which is the clause that let this defect live — the
  earlier pin rejected `LayoutUtility.GetPreferredSize(_rect` as a literal and the code spelled the same
  reading through the helper instead. `OnlineUiWindowSurfacePinTests`' width pin now reads the rule where it
  lives.
- **The row's own prefab art is hidden too, found while the fix ran.** The game draws a grey frame around every
  row of its own full-width settings screen, and the element root kept it: on a page it read as a box around
  every row, one per setting, which the user called out on 2026-10-15 ("the light grey box in the background is
  still there") after the width fix was on screen. A row of a page is a CONTAINER CUO arranges, not a control, so
  `OnlineUiRowGeometry.LayOutControlRow` hides the row's own drawing — the same treatment a control's children
  already had — and the page's fill shows through; the rect and the raycast stay (a click inside the window still
  lands on CUO's surface), while the control's own box and every element that IS a control keep the game's art.
  The capture after it is `e1-prefs-noframe.png` / `e3-admin-noframe.png` in the batch's own directory, against
  `d4-prefs-after.png` / `d2-admin-after.png` before it.
- **Limits.** Whether `32.8` units of checkbox reads as the game's own checkbox at the player's canvas scale is
  the acceptance run's judgement, and so is whether the rule rows now read as one column; the reading above is
  one host's window at one resolution, and this pass is the width the layout gives a control, not a re-cut of
  the Admin page's design.

## Non-goals

- Not a richer colour picker: the user decided this pass keeps the preset blocks plus the hex field.
- Not a change to the wire, the session, the save or any gameplay behaviour — this pass is geometry, input
  and presentation.
- Not a UI framework for other mods' windows, and not a re-do of the overhaul's own migration.
