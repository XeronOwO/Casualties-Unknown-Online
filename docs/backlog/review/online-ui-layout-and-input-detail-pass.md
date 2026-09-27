# The Online UI's layout and input detail pass

- Status: Review (code complete 2026-09-27: S1–S7 landed, gates green, deployed as `0.1.0+fdd72c84`; no code left to develop — the pixels and the clicks are the run's to judge)
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
  `review/cuo-launcher-button-obscures-the-view.md` (the idle fade this pass found still too opaque),
  `review/online-ui-panels-request-alpha-blend-false.md`, `review/remove-the-online-ui-console-page.md`

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

## Non-goals

- Not a richer colour picker: the user decided this pass keeps the preset blocks plus the hex field.
- Not a change to the wire, the session, the save or any gameplay behaviour — this pass is geometry, input
  and presentation.
- Not a UI framework for other mods' windows, and not a re-do of the overhaul's own migration.
