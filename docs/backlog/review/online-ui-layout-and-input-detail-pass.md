# The Online UI's layout and input detail pass

- Status: Review — the sixth pass below fixes the two rows batch `20261018-t` rejected (the open dropdown's
  list and the top-edge title bar) and re-read both live on the deployed build; awaiting the next acceptance
  batch. The failed batch's record stays as the rejection it was:
  `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261018-t.md`.
- History: code-complete since the fifth pass below. Batch `20261015-b` judged the row that kept it rejected:
  in the ENGLISH UI the strip reads `Tabs rect=984.0x36.0` with the six tabs at 112 / 132.1 / 133.8 / 112 /
  119.5 / 191.2 units — 820.6 plus the five 4-unit gaps against the page's 984 — and in Chinese 112 each, both
  read live on the deployed build (record
  `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261015-b.md`). Rows 2 and 3 **pass** in
  batch `20261015-a` (record `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261015-a.md`);
  rows 8–10 stayed `unproven` with the setups batch `20261001-k` named, and row 11's window half — the quick
  panel, the context menu and the overlays — needed a session; batch `20261018-t` has now read that half.
  **That rule landed in the fifth pass below** (`OnlineUiTabLayout`, with the room around a caption counted once).
  **Found 2026-10-14 by the user, after the batch**: this cycle routed the tabs through the width rule so the
  model's `TabWidth` floor would not go unread (the review's own finding), and the floor is not what decides a
  tab's width — the caption is, with the rule's padding counted twice. The live reading of the English page is
  `Tabs rect=1038.7x36.0` against the page's 984 (`.acceptance/20261015-a/f1-admin-en-dump.txt`), and the frames
  that batch captured in English show the last tab ending past the window's right edge. The verdict recorded for
  row 3 in that batch's record ("`Preferences` fits inside its tab") is true and stays; what the run did not read
  is that the STRIP no longer fits the window, which is row 11's half.
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
- Related: `done/online-ui-art-and-controls-overhaul.md` (the migration this pass reviews),
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
- **`TMP_Dropdown`'s own parenting is the template's parent.** `Show` parents the list to
  `m_Template.transform.parent` (`TMP_Dropdown.cs`), which is why the template sits on the layer: the list is
  a child of the layer from the first frame.
- **The popup layer covers the window, not the screen.** Superseded by the sixth pass, which made the layer
  compute the list's position every frame from the field and the canvas: the list opens above the field when
  the canvas has no room below it. What remains is that the layer has no mask, so a list longer than the canvas
  still hangs past the frame's edge.
- **The colour field's new rule is deliberate.** The box is never empty now: with the automatic colour it
  shows the resolved automatic hex, and the "current" line is what says `Auto`. Cleared text therefore means
  "give me the automatic colour", which is also what the Auto control does.
- **The two panels have no popup layer of their own.** The window owns the layer a dropdown's list rides; a
  dropdown added to the quick panel or the context menu would need the same treatment, and neither panel
  carries one today (the review's latent finding, kept as a limit rather than built for a case that does not
  exist).
- **The drag clamp is pinned now.** `OnlineUiWindowDragTests` holds the top edge (the title bar's own strip),
  the bottom and both sides, and the rule's arithmetic; the felt behaviour is still a run observation.

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

## Fourth pass (2026-10-14): the width a control is given

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
  wide as its caption group measured — is recorded with its acceptance plan on the sibling ticket. (The fifth
  pass below corrects that sentence's word "declares": as this pass left the rule, it added the caption's room
  TWICE, so a button-like element was 40 units wider than the declaration it was supposed to take.)
- **The pins.** `AControlOfARowIsAsWideAsTheGameAuthoredIt` holds the capture and the rule (a mutation row
  restores `PrefabWidth(_rect)`, another removes the authored width from the rule);
  `AnElementThatIsItsOwnControlDeclaresItsWidth` holds the element's own width write;
  `WidthsFollowTheContent` gained `!PrefabWidth(_rect`, which is the clause that let this defect live — the
  earlier pin rejected `LayoutUtility.GetPreferredSize(_rect` as a literal and the code spelled the same
  reading through the helper instead. `OnlineUiWindowSurfacePinTests`' width pin now reads the rule where it
  lives.
- **The row's own prefab art is hidden too, found while the fix ran.** The game draws a grey frame around every
  row of its own full-width settings screen, and the element root kept it: on a page it read as a box around
  every row, one per setting, which the user called out on 2026-10-14 ("the light grey box in the background is
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

## Fifth pass (2026-10-15): the strip's own width rule

The user read batch `20261015-a`'s English frames and asked whether the tab widths add up to more than the
window. They do: the live reading is `Tabs rect=1038.7x36.0` against a page that is 984 wide, with the six
English tabs 138.6 / 172.1 / 173.8 / 143.6 / 159.5 / 231.2 units (130 each in Chinese, which fits) and the last
one ending 55 units past the page's right edge. Two causes put it there, and both are the previous cycle's own
work:

- **The room around a caption was counted TWICE on the element path.** `OnlineUiControlSizing.EffectiveWidth`
  asked for `CaptionWidthOrZero(caption) + (2f * typographySize)` while `CaptionWidth` already returned the
  text plus that same room: an English `Preferences` tab therefore measured 231.2 units against the 191.2 its
  text needs, and every caption-sized button of every page carried the same surplus. The row path read the
  same quantity the other way — `OnlineUiRowGeometry.CaptionWidthOf` calls `CaptionWidth` alone — so the two
  paths disagreed about what a button is wide.
- **Nothing made a strip fit.** The tab row is one `HorizontalLayoutGroup` with `childForceExpandWidth` false,
  so it neither shrinks nor wraps: each tab took exactly what it asked for and the strip grew past its band.

What landed:

- **`OnlineUiControlBox.CaptionedWidth(modelFloor, captionTextWidth, room)`** is the ONE place a caption's room
  is added, over the model's floor, and `room` is an argument because it is not always the same room: a page
  control keeps one font size on each side of its caption, and the least a control can be drawn at keeps the
  room the control's own box keeps. `OnlineUiControlSizing` now separates `CaptionTextWidth` (the text TMP
  measured, no room), `CaptionWidth` (that text plus the room at the caption's own size — the row path's
  reading, unchanged) and `EffectiveWidth` (the room passed once), and adds `MinimumWidth`, the same rule at
  its other end.
- **`OnlineUiTabLayout.Widths(natural, minimum, stripWidth, spacing)`** (Runtime, pure) decides what each tab
  of a strip takes: a strip the tabs fit keeps every one at what it asked for; one they do not fit gives up the
  excess in proportion to the room each tab still has above its own minimum, in one pass, so no tab goes under a
  floor its model declared and the room is what is given up before any of a caption is; a strip whose minimums do
  not fit either keeps them and stays wider than its band, because a narrower caption is the worse of the two
  answers.
- **`OnlineUiWindowView.ApplyTabs` allocates through that rule**, after every tab is applied and before any is
  sized, over a budget of the WINDOW's own inner width (`TabStripWidth`: the frame's rect less its padding, with
  the declared content width standing in before the frame's first layout pass). The band's own rect is
  deliberately not the source, and the independent review of this round is what caught the first cut of the rule
  reading exactly that: the band is a child of the shell's group with `childForceExpandWidth`, so its rect is
  that group's inner width OR its own children's minimum, whichever is larger — and each tab's minimum is the
  width this rule just wrote on it, so a budget read off the band equals precisely what was asked for, the rule
  reports nothing to give up and the strip is as wide as its own content for ever. The live reading is the proof:
  `Tabs rect=1038.7x36.0` where the six English tabs sum to 1038.8, against `Title Bar rect=984.0x28.0` on the
  same shell (batch `20261015-a`). It is the self-measurement `OnlineUiControlSizing` records from batch
  `20261001-k`, in the form that converges instead of running away.
- **The two panels' and the window's view machinery is one type now.** The change's own first cut took
  `OnlineUiWindowView.cs` to 609 lines, past the 600-line ceiling, and the page's row machinery could not be cut
  out inside this cycle: what came out instead is the reconcile the window and the panel each carried a copy of —
  `OnlineUiViewSource`, which builds one element's view, keeps a container's views aligned with the model (a view
  whose kind and id still match is reused, the one whose element changed identity is rebuilt) and owns the
  prefab's two reports (a kind whose row could not be loaded, a control the game left unclickable). The window
  lands at 497 lines, the panel at 441, and `ThePanelsKeepTheWindowsRowMachinery` refuses a private copy growing
  back.
- **The numbers.** With the room counted once the six English tabs ask for 112 / 132.1 / 133.8 / 112 / 119.5 /
  191.2 units — 800.6 plus the five 4-unit gaps, 820.6 against the 984 the page has, so at the shipped window
  size nothing has to shrink and the rule only has work to do once the window is clamped narrower than that;
  with the room counted twice they ask for 1038.8 — the run read `Tabs rect=1038.7x36.0`, the 0.1 being the
  dump's own rounding of six tab widths — and the strip now shares its budget instead of running past it. The
  Chinese tabs go from 130 to the 112 their model declares as a floor.
- **The pins.** `OnlineUiControlWidthPinTests.ATabStripFitsItsOwnBand` holds the whole chain (the allocation
  call, both width reads, the budget read off the window and the rejection of every read off the band, the
  once-counted room, the minimum's own room and its value, and the plugin's `TabWidth` floor, which
  `OnlineUiTabLayoutTests` repeats as a constant because the plugin is not referenced by the suite) with six
  mutation rows — a strip that hands out what was asked for, a strip with no floor under any tab, the room
  counted twice again, a budget read off the band, a floor with no room left for a caption, and the model's
  floor moved. `OnlineUiTabLayoutTests` carries ten cases over the rule, the last of them the six live English
  captions. The width pins' own anchors moved with the rule, and the two pins that pointed at the machinery the
  extraction moved were re-pointed at `OnlineUiViewSource` in the same round.
- **One consequence for the run to look at.** Every caption-sized control is now narrower, because that surplus
  was the room counted twice: exactly 40 units where the caption decides the width, and less where the tab
  model's own 112-unit floor takes over (both `Home` at 138.6 and `Admin` at 143.6 land on 112, so their change
  is 26.6 and 31.6). A caption still keeps one font size on each side of itself, which is the room the rule
  declares, and the frames are the place to see whether that reads as a control.
- **Limits recorded with this pass.** The budget only ever bites when the window's inner width is narrower than
  the tabs' own total — the shipped window is 984 against 820.6 — so the shrink path is exercised by a clamped
  window (a small canvas) rather than by the shipped one, and it is the pure rule's tests, not the run, that
  hold its behaviour. The least a tab is drawn at keeps `ControlInnerPadding` per side for its caption, and the
  game's own row prefab keeps 9.05 per side (the prefab facts of batch `20261015-a`), so a tab sitting exactly
  on its floor has about 1.9 units of slack in the caption's own box; a prefab whose caption box kept more than
  that — the factory's own fallback is one font size per side — would leave the caption tight, which is the
  reason the floor is a named room rather than zero.
- **Structure.** `OnlineUiWindowView.cs` is 497 lines against the 600-line ceiling, after the shared view
  machinery came out of it; the page's own row machinery is the next thing to cut if this file grows again, and
  the paired-controls ticket (which re-cuts the same rows) is the pass that will need it.

## Sixth pass (2026-10-18): the two rejected rows

Batch `20261018-t` read two defects on the deployed build; this pass found each one's cause and re-read the
fix on the deployed build again, through the production code paths (one host client; the run's local
artifacts are the `20261018-w` set named by `acceptance-artifacts-dir`).

### Row 4 — the open list was a negative rect at the window's top edge

The layer itself was right (sorting `30001`, its own raycaster, no masked ancestor), but the template it took
over had been re-cut TWICE by CUO before the game's own dropdown code saw it:

- `OnlineUiBoxGeometry.LayOutInterior` treated the template as one more container of the dropdown's box and
  wrote the row's insets onto it (`offsetMin = (10, 10)`, `offsetMax = (−10, −10)`). The template is
  top-anchored (both vertical anchors at `1`, a zero-height anchor span) with pivot `(0.5, 1)`, so that write
  made it `−20` high; the same pass had already hidden its art and dropped its layout drivers, which the skip
  now leaves alone as well.
- The template's parent became the `1000 × 700` layer, where its anchors resolve against the window: the
  batch read `sizeDelta=-20.0x-20.0 rect=980.0x-20.0` at the window's top edge.

The template's rect is what `TMP_Dropdown.Show` starts the list from (`TMP_Dropdown.cs`, the `m_Template`
block of `Show`): the clone inherits the template's anchors, pivot, position and height, and the game's own
code only ever SHRINKS that height (`float num = rectTransform.rect.height - rectTransform2.rect.height;` —
`if (num > 0f)`), so a negative authored height stayed negative. The fix is one ownership rule per writer:

- **The box pass leaves the template alone** (`OnlineUiBoxGeometry.LayOutInterior`, both loops): a dropdown's
  template is the list's factory, not part of the box the player sees. Its authored rect — the height a list
  may take (`193.4` in the game's own reference) and the edge it opens at — is where `TMP_Dropdown` starts.
- **The popup layer owns the template's rect and the open list's** (`OnlineUiDropdownPopup`): it captures the
  authored height when it adopts the template, and every frame the window is applied it puts the template on
  the field's bottom edge and the open list on the side of the field the canvas has room for — below normally,
  above when below would leave the canvas and above would not (`OnlineUiDropdownPlacement.SideFor`, the same
  question the game's own `FlipLayoutOnAxis` answers from world corners). The field is read from the live
  `RectTransform` (world corners into the layer's space), so a dragged window, a scrolled page or a rebuilt
  row carries the list with it. The layer tracks one template per dropdown, prunes it when the dropdown's row
  is rebuilt (the layer, not the row, is its parent) and logs that at `Debug`; and it closes a second field's
  list, because the game's own outside-click blocker sits one step below CUO's surface and would not swallow
  the click that opens it.

Live reading after the fix (Preferences, the language dropdown, a `150 × 36` field):

- the template and the list are `150.0 × 193.4`, anchored at the field's bottom (`anchor 0.172, 0.431`,
  pivot `0.5, 1`); the list's layer-local box is `x −403.0..−253.0, y −241.4..−48.0` against the field's
  `y −48.0..−12.0` — under the field and exactly as wide;
- the list opens below the field on the shipped canvas — the batch's own field has room for it (the worst
  case on that canvas leaves 4.6 units to spare), and the reading above is that case;
- the viewport keeps its `Mask` and the list its `ScrollRect` (content `150.0 × 760.1`), so the game's own
  list still clips and scrolls;
- the layer sorts at `30001`; the list is created with the template's `30000` and the next `Apply` normalises
  it to `30001` (a read taken in the same eval as `Show()` sees `30000`; the frame captured after that frame
  sees `30001`), while its alpha is still fading in;
- choosing `English` through the item's own `Toggle` — the listener TMP wires to `OnSelectItem` — applied at
  once: `value 1 → 0`, and the next frame's UI was English; selecting `简体中文` went back (`w-after-select.png`,
  `w-final-zh.png`);
- the log-level dropdown (7 options) opens the same way under its own field (field `y 86.0..122.0`, list
  `y −107.4..86.0`), content `150.0 × 1010.1` with all seven items and the same mask and scroll.

### Row 9 — the top clamp kept a strip of the FRAME, which at the top is the window's bottom

The clamp was symmetric — `canvas/2 + window/2 − 48` on every side — so at the up clamp the 48 units that
stayed inside were the window's bottom, and the title bar, the only surface a drag starts on, sat 411 screen
pixels (616.5 canvas units) above the canvas. The rule is now a pure Runtime type (`OnlineUiWindowDrag`): the
bottom and both sides keep `Reach = 48` units of the frame, while the top keeps the TITLE BAR
(`min(Reach, TitleHeight)` of it), so the window may rise only until the bar would leave.
`OnlineUiWindowDragHandler` reads the rule and keeps the IMGUI behaviour — the window simply follows — when
there is no canvas to clamp against.

Live reading on the batch's canvas (1920 × 1080 canvas units): right `1412`, left `−1412`, up `190`, down
`−842`. At the up clamp the window's world box is `307,253..973,720` on the 1280 × 720 screen and the title
bar `312,696..968,715` — wholly on the canvas; at the down clamp the title bar `312,8..968,27` is the strip
that stays.

### Pins and tests

- `OnlineUiWindowDragTests` holds the rule: seven cases, run red before the fix (the extracted first-cut
  arithmetic failed three of them), green after it — the top edge's exact value (`190` on the batch's canvas,
  `16` on the clamped one) and the title bar's relation to `Reach` are pinned, not just the inequality.
- `OnlineUiDropdownPlacementTests` (new) holds the side rule and the anchor arithmetic: below when it fits,
  above when below does not and above does, below when neither side does, the exact fits, and a field dragged
  outside the layer.
- `OnlineUiPopupLayerPinTests` (new) is the popup family split out of `OnlineUiLayoutDetailPinTests` when its
  own mutation rows took that class to 43 cases against the 40-case cap: eight mutation rows — a window with no
  layer, the template left inside the page, a centred list, a zero-height list, a second list left open, the
  prune left silent, the box pass stretching the template again, and the caption's room counting the
  template's width.
- `OnlineUiLayoutDetailPinTests` keeps the rest (11 facts + 23 mutation rows = 34 cases);
  `OnlineUiWindowSurfacePinTests` re-anchored its drag pin on the rule the handler now reads.
- The focused runs are green: `OnlineUiWindowDragTests` 7, `OnlineUiDropdownPlacementTests` 7,
  `OnlineUiPopupLayerPinTests` 9, `OnlineUiWindowSurfacePinTests` 31 and `OnlineUiLayoutDetailPinTests` 34.

### Self-check: mechanism × change × evidence

| Mechanism | Change | Evidence |
|---|---|---|
| The dropdown's template rect (the list's factory) | The box pass skips it in both loops; the layer captures the prefab's authored height when it adopts it | `OnlineUiPopupLayerPinTests` (two mutation rows on the skip; the capture clause); live: template and list `150.0 × 193.4` |
| The list's placement | The layer writes anchor, pivot and size every frame from the field's live box; the side is `OnlineUiDropdownPlacement.SideFor` | `OnlineUiDropdownPlacementTests` (7 cases), the popup pin's pivot and size rows; live: list under the field (`w-popup2.json`) |
| The list's sorting | The layer sorts at `30001`; the open list is normalised to it on every `Apply` | The popup pin's sorting and normalise clauses; live: `listCanvas=30001` after the next `Apply` |
| The template's lifetime | One template per dropdown; pruned with a `Debug` line when its row is rebuilt | The popup pin's prune-log clause and its mutation row |
| A second field's list | The layer closes every open list but the newest | The popup pin's `Hide()` clause and its mutation row |
| The window's drag clamp | A pure Runtime rule: `Reach` of the frame on the bottom and sides, the title bar's own strip at the top | `OnlineUiWindowDragTests` (7 cases, red before the fix — 3 of 5 old ones failed); live: up `190`, down `−842` |
| The drag's application | The handler reads the rule and keeps the IMGUI follow where there is no canvas | `OnlineUiWindowSurfacePinTests.TheTitleBarDragsTheWindow`, re-anchored |
| The build and the suite | — | `dotnet build` exit 0; `dotnet format` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` 5861 behavioural + 741 normative, all green (`full2.log`) |

### Limits (this pass)

- **A field with room on neither side keeps its list below.** The list flips above only when the canvas has
  room above it; a field boxed in by the canvas (a canvas shorter than twice the list plus the field) keeps
  the list below, with its first option at the field and the rest past the canvas's edge. The layer has no
  mask, so a list longer than the canvas is not clipped by it either.
- **No OS pointer and no OS drag.** The option choice was driven through the item's own `Toggle` (TMP's
  production listener) and the drag through the production `OnlineUiWindowDragHandler.OnDrag` with a
  synthetic delta — the same limits batch `20261018-t` recorded.
- **One host, one window.** The two rows are window-only; no member view is involved, and no session was
  needed for them.

## Review dispositions (2026-10-18)

The independent adversarial review (fresh context, frozen tree) found two blockers, one major, twelve minors
and three nits; all were fixed or answered in the same commit as this section, and each is recorded here with
the code it cites.

- **B1 — the test-class size gate.** `TestClassSizeGateTests` read `OnlineUiLayoutDetailPinTests` at 43 cases
  (HEAD was exactly 40; the three new popup mutation rows took it over). Fixed by the split the gate asks for:
  the popup fact and its rows moved to `OnlineUiPopupLayerPinTests`, and the class now holds 34 cases.
- **B2 — six dangling backlog references.** The move to `review/` left `docs/backlog/todo/...` in
  `docs/acceptance/lessons.md`, `docs/backlog/review/offscreen-arrow-label-clipping.md`,
  `docs/evidence/acceptance/20261014-a-scope.md`, `20261015-a-scope.md`, `20261015-b-scope.md` and
  `20261018-t-scope.md`; all six now point at `docs/backlog/review/online-ui-layout-and-input-detail-pass.md`,
  and `BacklogReferenceGateTests` is green.
- **M1 — the list only opened downward, cancelling the game's own flip.** The layer now answers the same
  question the game does (`RectTransformUtility.FlipLayoutOnAxis` on a world corner that leaves the canvas,
  `TMP_Dropdown.cs`): `OnlineUiDropdownPlacement.SideFor(listHeight, roomBelow, roomAbove)` opens the list
  above the field when below has no room and above does. The "always opens downward" limit is retired; the
  remaining limit is the field with room on neither side.
- **m1 — `411 units` was screen pixels.** The ticket and the `OnlineUiWindowDrag` doc now say 411 screen pixels
  (616.5 canvas units).
- **m2 — `listCanvas=30001` on the first frame was contradicted by the same-eval reads.** Corrected: the list
  is created at `30000` and normalised on the next `Apply`.
- **m3 — "zero-height pivot" misnamed the mechanism.** Corrected to "both vertical anchors at 1 (a zero-height
  anchor span), pivot `(0.5, 1)`".
- **m4 — the earlier popup limit's cause was stale.** Its sentence said the game's own code computes the
  position; the sixth pass computes it, and the old limit now says so (superseded) and keeps only the true
  part (no mask on the layer).
- **m5 — "on every page switch" over-claimed.** The six inactive templates are per rebuilt dropdown, not per
  page switch.
- **m6 — the causality was inverted.** The layer is the template's parent, so the row's destruction no longer
  takes it; `NormaliseAdopted`'s prune does, and it now logs at `Debug`.
- **m7 — the first box-pass loop's guard was unpinned.** `part != template` now has its own mutation row in
  `OnlineUiPopupLayerPinTests`.
- **m8 — the drag tests did not pin the rule.** `TheTopClampIsTheWindowsTopEdge` asserts the exact `190` and
  `16`, and `TheTitleBarIsWithinTheReach` states the relation the `min` branch assumes.
- **m9 — the popup's arithmetic was pinned only by spelling.** It is now the pure Runtime rule
  `OnlineUiDropdownPlacement` with its own tests.
- **m10 — the prune branch was unobservable.** `NormaliseAdopted` logs
  `"Online UI window: the popup layer dropped the template of a dropdown whose row was rebuilt."` at `Debug`,
  and a mutation row holds the line.
- **m11 — the live reading cited no build identity.** It is the tree's build output deployed as
  `0.1.0+6065f67a05497f62f80fa937787ff93a82534309-dirty` (`verify-deploy.ps1` exit 0 in the run's
  `verify1.log`); the committed artifact is verified after the commit and the batch names it.
- **m12 — two open lists could be misplaced.** The layer now closes every open list but the newest
  (`adopted.Dropdown.Hide()`), which is what the game's own blocker would have done one step higher; a
  mutation row holds it.
- **Nits.** The zero-height mutation's wording now says the batch's negative rect is the degenerate form; the
  duplicated `canvas.overrideSorting` clause is pre-existing and left alone; the cause section now names the
  template's art and layout drivers, which the same skip covers.

## Non-goals

- Not a richer colour picker: the user decided this pass keeps the preset blocks plus the hex field.
- Not a change to the wire, the session, the save or any gameplay behaviour — this pass is geometry, input
  and presentation.
- Not a UI framework for other mods' windows, and not a re-do of the overhaul's own migration.
