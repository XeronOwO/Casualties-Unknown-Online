# The Online UI's paired controls and its pages' scroll affordance

- Status: Done — batch `20261018-t` re-read all five rows on the current artifact and every one passes
  (record `docs/evidence/acceptance/online-ui-paired-controls-and-page-scroll-20261018-t.md`). The one
  residual is the user's look at the Admin frame (`a11-admin-en.png` / `a6-admin-zh.png` in that batch);
  a rejection returns the ticket to `todo/`.
- History: the four points of *The user's rejection* below were **implemented and passed on batch
  `.acceptance/20261015-e/`'s own readings** (artifact `0.1.0+6dad2d41…`; the reading is the section
  *The reading of the fix* below, and the run's record is
  `docs/evidence/acceptance/online-ui-paired-controls-and-page-scroll-20261015-e.md`). S1 to S4 landed in the suite, and two real-machine passes re-read them. Batch `.acceptance/20261015-c/`
  found the paired row's second line did not stretch (its reading is kept below); the fix that followed — the
  row owns its line — is what batch `.acceptance/20261015-d/` measured and what the user rejected the LOOK of
  (that run's record is `docs/evidence/acceptance/online-ui-paired-controls-and-page-scroll-20261015-d.md`);
  the four points of that rejection, and the reuse direction that settles the slider's two, are below.
- Priority: Medium-High
- Category: Online UI / layout, presentation and input
- Source: The user, 2026-10-14, in their own words: (1) "is there a scrollbar? the Admin page has a part that
  goes past what is shown, and I don't see one on the right"; (2) of the piggyback-weight row: "the slider's
  middle has no thin line, so you cannot see where its ends are, and the value area beside it should be
  editable, with the two bound together so they stay in sync — that would make the UI more perfect"; (3) of the
  paired control in general: "this kind of paired control could be made into a template: the name on the first
  line, the slider and the value on the second; squeezing it onto one line is not pretty, and when the names
  differ in length it looks ragged".
- Related: `done/online-ui-layout-and-input-detail-pass.md` (the pass this follows: the row's geometry, the
  one compact control height and the row's own prefab art are its work), `done/online-ui-art-and-controls-overhaul.md`
  (the surface these controls are built on), `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261015-a.md`
  (the frames that show the two shapes the asks are about)

## The user's rejection (2026-10-15, on the frame batch `.acceptance/20261015-d/` captured)

The user read the frame the run captured (`f6-admin.png`, the Admin page of a hosted lobby; `f5-admin-dump.json`
is the reading beside it) and rejected the look. Four points, in their own words, each with the reading it comes
from:

1. **"The content should keep clear of the scrollbar — that is what normal front-end design does."** Reading:
   the value box ends at `486.00` while the page's bar spans `482.00..490.00`, so 4 units of the box sit under
   the bar. The page's rows have to stop before the bar's room.
2. **"The slider's alignment is wrong: its left end should reach the left boundary."** Reading: the slider spans
   `-227.99..344.00`, i.e. it starts at the page's control COLUMN — where the toggle rows' checkboxes start
   (`Toggle 32.79` at `-227.99..-195.20`) — and not at the page's left margin (`-486.00`, where every row's label
   starts). The paired row's own label keeps that column for the row's control, which is what puts the slider
   there.
3. **"The background box is too big and looks wrong; the yellow rectangle in front and the background's full
   rectangle do not overlap exactly."** Reading: `Slider/Background` — the game's own decoration, and NOT the
   object the slider reads by (the component's own `targetGraphic` carries `whitesquare` while this node carries
   `uiBlockNano`) — is `571.99x28.00` at the prefab's anchors (`0.00,0.25..1.00,0.75`), while the fill
   `Slider/Fill` is `551.99x36.00` at `0.00,0.00..1.00,1.00`: two rectangles of different sizes and insets where
   the player is meant to see one track with the filled fraction over it.
4. **"The name '背负重量系数' still has a margin problem — it is not solved."** Reading: the name is a
   `CUO Online UI Label` whose box is the page's own width (`span=-492.00..492.00`) with nothing inside it, so
   its text starts at the page's edge, while every row's label starts `6.00` units inside it (`-486.00`). It is
   the one line of the page whose text does not keep the page's own room.

The user's summary: *"there are too many detail problems — fix them properly."*

**The user's question that fixes the DIRECTION** (2026-10-15, after the four points): *"the game already ships a
slider control — why not reuse it?"* The answer, and it settles how points 2–3 are to be fixed:

- The slider IS the game's own control and prefab: `OnlineUiControlFactory.PrefabPathOf` maps the kind to
  `Special/GameSettingFloat`, the same row the game's own `SettingsMenu` builds and takes
  `GetChild(1).GetComponent<Slider>()` from (the decompiled shape is recorded in
  `docs/evidence/selfchecks/ui/online-ui-window-family-selfcheck.md`). Its drag behaviour, its sprites
  (`uiBlockNano` for the rail, `whitesquare` for the knob) and its font are the game's, and S1 of this ticket
  removed the rule that hid them, precisely because a slider's children ARE its reading.
- What is NOT reused is that prefab's hand-authored INTERIOR: `OnlineUiSliderGeometry` re-places the rail, the
  fill and the knob for the page's one compact height, and that re-cut is what the three look defects come out
  of — the game's own `Background` is left standing while the part CUO tints and places as the rail is a
  DIFFERENT object (the component's `targetGraphic` carries `whitesquare`, the visible node carries
  `uiBlockNano`), the fill is pulled onto the whole control (`x` inset 10, `y` `0..1`) while the prefab's own
  `Fill Area` sits at `y 0.25..0.75`, and the knob's place is the prefab's old anchors rather than the value.
- **The direction: let the game's own interior proportions stand, and re-cut only the layer that has to be
  re-cut for the compact row — never pull the parts themselves onto the whole control.** The same question must
  be asked of the other kinds before the re-cut is trusted (a field's viewport, a dropdown's frame), because the
  reason the re-cut exists at all is the height the game authored for (~3x this page's), and that reason is
  about the ROW, not about each inner rectangle.
- **Not established yet, and the first thing the next cycle checks**: whether the prefab's own interior survives
  the compact 36-unit height untouched (its parts inside the row, nothing hanging out). If it does not, the
  re-cut stays but must be derived from the prefab's own proportions rather than from CUO's full-span numbers.
  The reading to check it with is the same dump (`rect`, `anchor` and `span` per node; the dump of batch
  `.acceptance/20261015-d/`, `g3-admin-dump.json`, is a worked example of it).

**This was the work order for the cycle that landed it** (the four points are one cycle, not four, and the
direction above decides how the slider's own two are done). What that cycle did, point by point, and what the
next reader should judge it by, is *The reading of the fix* below.

## The reading of the fix (batch `.acceptance/20261015-e/`, artifact `0.1.0+6dad2d41…`, one client, the same page)

The four points of *The user's rejection* above, judged on this batch's own readings —
`a5-admin-dump.json` (recipe `cuo-ui-dump`), `a6-prefab.json` (recipe `cuo-ui-prefab`, added by this cycle)
and `a7-admin.png` (the frame):

| # | The point | Reading | Verdict |
|---|---|---|---|
| 1 | The page's content keeps clear of the bar | `Viewport` and `Content` read `span=-492.00..482.00`, the bar `8.00x596.00 span=482.00..490.00`, and the value box `span=356.00..476.00` — six units clear of where the track begins | **fixed** (was `486.00`, four units under the track) |
| 2 | The slider starts at the page's left margin | `Line/GameSettingFloat(Clone)/Slider` reads `820.00x36.00 span=-486.00..334.00` — the x every row's label starts at (`Label span=-486.00..-235.99`); the row's own empty label is `active=False` | **fixed** (was `-227.99`, the page's control column) |
| 3 | One rail with the filled fraction exactly over it | `Slider/Background 820.00x28.00` (grey) and `Slider/Fill Area/Fill 223.33x28.00` (the page's warm tone) are the SAME 28 units, the fill inside the prefab's own `Fill Area` with its anchors driven to the value, and the knob `Handle 36.00x36.00` standing at that value; the frame draws the fraction's two lines and the rail's two lines on the same pixel rows | **fixed** (was a fill 36 units tall stretched over the whole control, over a 28-unit rail, with a knob rendering 45 units tall) |
| 4 | The name keeps the rows' room | the frame: the paired row's name, the section heading and every row's label start at the same pixel column (`316`) | **fixed** (the name started at the page's own edge, four units left of the labels) |
| 5 | The interior the game authored — the direction's "first thing the next cycle checks" | `Special/GameSettingFloat` is `1058.20x80.18`; the slider is a `962.45x20.00` band inside it; its `Fill Area 942.45x20.00` is the rail's own height; its `Fill` is point-anchored with no height of its own (`sizeDelta 10,00`); its `Handle Slide Area 942.45x20.00` carries a `20.00`-wide handle | **answered**: the interior's proportions stand and nothing of them hangs out of its row, and the game's own knob is one RAIL-height square whose travel is inset by half of it — so what a compact row has to re-cut is the knob's travel, and the knob's own width is this page's choice |

What the fix does NOT change, and the readings that say so: the toggle, dropdown, field and tab rows
(`Label 250.01`, `Toggle 32.79` at the page's column, `Tabs 984.00x36.00` with six `112.00` tabs), the
shell's bands and the bar's own geometry (`8.00x596.00`, its handle `8.00x555.28`).

Three consequences written down rather than hidden:

- **A page's rows are 974 units wide now, not 984** — the row of *The re-taken reading (batch
  `.acceptance/20261015-d/`)` above says "the row's box IS the page's width (984.00)"; that row is
  **superseded** by the user's first point. The page's BAND is still 984 and the content gives up the bar's
  10 units of it: the next reader judges a row's box against `Viewport`'s span, not `Scroll`'s.
- **A slider's draggable area is the game's own rail and knob** (28 and 36 units tall) rather than the
  36-unit-tall fill strip the previous cut stretched across the left of the row.
- **The bar's own room is reserved whether or not the bar is drawn**, so a page that fits carries the same
  empty 10-unit gutter. Deliberate: taking it only while the bar is up would make the wrapping that decides
  whether a page overflows depend on its own answer.

## What the 2026-10-15 pass (batch `.acceptance/20261015-c/`) already reads

Artifact `0.1.0+c496e4c9…` on the real machine, one client, the Admin page of a hosted lobby. The dump
(`dump-admin-4.json`, recipe `tools/acceptance/recipes/cuo-ui-dump.cs`) is the reading:

| What | Reading | Verdict |
|---|---|---|
| The bar on a page that does not fit | `Window/Scroll/CUO Online UI Scrollbar rect=8.0x596.0`, its handle `8.0x555.3`, both active; the content is 644 against a 600 viewport | S4 works |
| The slider's own reading | every control's `background` is `whitesquare α=1`, i.e. no prefab frame is drawn; the fill is the page's warm tone and the handle is the light tone, both placed on the compact box | S1 works |
| The value beside the slider | `GameSettingInt(Clone)/Input` with `text=0.80`, the game's own field, its own box and caret | S2 present |
| The paired row | the name is a line of its own (`Line` 984x28) and the slider and the field are on the NEXT line together | S3 works |
| **The paired row's widths** | the row's own box is 132 wide inside a 984-wide page, and the field is placed at `x=324` INSIDE that 132-wide box, i.e. drawn outside its row — on screen it reads as two floating widgets and is exactly the "no margin" the user reported | **the defect** |
| The bar of the control column | a name is 120 wide and the widgets start at 260 (the test's own default) — but not for the paired row, whose box ignores the column | **the defect** |

The cause is read, not guessed, and the first reading of it was WRONG — refuted by the same day's second
pass, whose fix is what this ticket now waits to have judged:

- **What the first reading blamed**: the forced `LayoutRebuilder.ForceRebuildLayoutImmediate(_content)` in
  `OnlineUiWindowView.Apply`, on the theory that uGUI does not track `LayoutElement` writes. **Refuted**:
  `LayoutElement` marks its own layout dirty whenever a value it holds changes (the engine's own IL —
  `SetPropertyUtility.SetStruct` → `LayoutElement.SetDirty` → `LayoutRebuilder.MarkLayoutForRebuild`, and
  the batch's own dump proves the engine took `LayoutElement` writes without any forced rebuild, because the
  column pass's 250-unit label widths are in the reading). The forced rebuild re-measured exactly the numbers
  it was handed; it was removed from both surfaces.
- **The real cause**: the row declared no width of its own. `OnlineUiControlView.ApplyLayout` wrote
  `flexibleWidth = 0` — the value field shared the row's line, so the row was never "alone" on it — and the
  row's own group then answered the line's group with the width of the control inside it
  (`6 + 120 + 6 = 132`, the number in the table above). `flexW=0` and `flexW=1` sat on different lines of the
  same row because the LABEL was the only flexible thing in the row's own group, and the row's
  `LayoutElement` overrode it with a rigid zero.
- **What the fix is**: the row now OWNS the line the model sent its control to. The Runtime's
  `OnlineUiRowLayout.LineOwners` names the holder of each line, both surfaces lay the elements the model put
  BESIDE the control out INSIDE the owning row, the row's own `LayoutElement` is flexible, its control takes
  what the line has left (so the control stays in the page's control column and stretches), and the value
  field closes at the row's trailing edge — its own 6-unit room inside that box is the page's margin there.

**The reading this table is re-taken with**: the row's box is the page's own width (984.00) and the value box
(`.../GameSettingInt(Clone)/Input`) ends 6 units inside the page's right edge. The dump recipe now reports a
`span` per node — its box in the WINDOW's own units — so that distance is read directly instead of being
recomposed from two nodes' local `rect`/`pos`.

## The re-taken reading (batch `.acceptance/20261015-d/`, artifact `0.1.0+ce9db539…`, one client, the same page)

| What | Reading | Verdict |
|---|---|---|
| **The paired row's width** | `Line/GameSettingFloat(Clone)` `rect=984.00x36.00 span=-492.00..492.00` against the page's own `Content span=-492.00..492.00` — the row's box IS the page's width (was `132.00`) | **fixed** |
| **The value box's margin** | `GameSettingFloat(Clone)/GameSettingInt(Clone)/Input` `span=366.00..486.00`: the value box ends exactly 6 units inside the page's right edge (`492.00`), with the field's own row flush at that edge (`352.00..492.00`) | **fixed** |
| **The control column** | the row's own label keeps the column (`250.01`, `-486.00..-235.99`) and the slider starts at `-227.99` — the column the toggle rows' controls start at | **fixed** |
| The slider's own reading | `Slider 571.99x36.00` (the control column to the value box), `Background 571.99x28.00` grey at alpha 1, the fill in the page's warm tone, the handle a light element; the frame `f6-admin.png` shows the track's thin line with both ends inside the page and the fraction filled to the value | S1 holds, with the handle's own rect a limit the record names |
| The value beside the slider | `GameSettingInt(Clone)/Input` with `text=0.80`, the game's own field, its own box | S2 holds |
| The paired row | the name is an element of its own (`984.00x28.00`) on one `Line` and the row that carries the control (and the value, inside it) is on the next | S3 holds |
| The bar on a page that does not fit | the same as the first reading (`Content 644.00` against `Viewport 600.00`, the bar `8.00x596.00` with its handle `8.00x555.28`) | S4 holds |
| No regression | `Tabs 984.00x36.00` with six `112.00` tabs inside the window; the one-line rule rows read their old widths (`Label 250.01`, `Toggle 32.79`) | holds |

What the run could not settle is in the record's *Limits*: the handle's own rect (`607.99x9.00` on the
prefab's untouched anchors — the engine's own place for a slider's handle is not driven in this page, while
the fill's is), the panels' half of the shape (no drawer reaches it), and a paired row's own minimum width
(`282 + 2 × columnWidth`).

## Readings of the new shape the judging pass has to know (before it re-takes the table)

- **The page's scrollbar track overlaps the value box by 4 units when the page overflows.** The track spans
  the outermost 10 units of the page band (8 wide, inset 2) and is drawn as a sibling ABOVE the viewport, so
  a value box 6 units inside the page's right edge sits under it: a click on those 4 units is the scrollbar's
  jump, not the field. The criterion above asks for exactly 6, so the overlap is a consequence of it.
  **Decided by the user the same evening**: the content is to keep clear of the bar, "as normal front-end
  design does" — and the reading of the fix above is that it now does, at the cost of the page's rows being
  the band less the bar's room.
- **A paired row's own minimum width is `282 + 2 × columnWidth`**: the row's label and the value field's own
  label both keep the page's column. A page whose name column exceeded 351 units would push the value past
  the page's right edge; the Admin page's is ~250, so this is unreachable today and is written down rather
  than guarded.
- **The panels' copy of the shape cannot be judged on screen yet**: no panel drawer uses `NumberRule`, so
  `OnlineUiPanelView`'s half of the fix (it asks the same `ParentOf`/`Owners` rule) is aligned by
  construction and has no acceptance row.

## The gap, with the code fact behind each ask

| # | The ask | Cause read from the code |
|---|---|---|
| 1 | A page taller than the window shows no scroll affordance | `OnlineUiWindowView.Create` builds `Scroll` → `Viewport` → `Content` and sets `scrollRect.vertical = true`, but nothing in CUO builds a scrollbar: the wheel scrolls, the position is invisible, and the ticket of the layout pass even records it — `ContentWidth` is "the viewport's width (the scroll has no scrollbar of its own)" |
| 2a | The slider's track and handle are invisible | `OnlineUiRowGeometry.LayOutInterior` calls `HidePrefabBoxArt` on every child of a control, which for `Special/GameSettingFloat` is the row's own `Background`, `Fill Area` and `Handle` — the game draws the track, the filled part and the handle there, so the control is left as CUO's single box (`ApplyBoxStyle`) with nothing inside it. The hiding is right for a field's box or a dropdown's frame; for a slider it removes the control's own reading |
| 2b | The value beside the slider is a label, not an input | the slider element's third child is a `TextMeshProUGUI` (`OnlineUiControlView`'s `valueText`), written from the model's `Value` string; the page builder's `Slider(...)` takes `valueText` and one `changed` delegate, so there is no id, no field and no second direction |
| 3 | A paired control costs one line | `OnlineUiRowModel.Of(elements)` puts a row's label and its control on one line and the Runtime's `OnlineUiRowLayout` wraps by width alone; nothing expresses "this control belongs UNDER its name", so a slider row reads as `背负重量系数 [slider] 0.80` with the control right-aligned against a label of any length |

## What done looks like

1. **A page that does not fit says so.** The window's page carries a visible scroll affordance that moves with
   the content, and it appears only when the content is taller than the viewport. A canvas too small for the
   window keeps working (the layout pass's own clamp).
2. **The slider reads as a slider.** Its track, its filled fraction and its handle are visible, so both ends of
   the range and the current value can be seen without touching it; the control keeps the page's one box and
   the game's own art where the game's art is what carries the meaning.
3. **The value is editable and bound both ways.** Typing a number moves the slider and dragging the slider
   updates the field, in the same frame, with one setter behind both; a value outside the range or not a number
   is refused the way the hex field refuses a half-typed colour, and nothing is committed until it parses.
4. **The paired control is a template.** Its name is one line, its slider and value the next, and two rows of
   the same kind line up whatever their names are. Whether the other paired controls (a checkbox, a dropdown, a
   text field) take the same two-line shape is the user's call — see the open question below.
5. **No regression.** Every pinned fact of the layout pass stays green, and the pages that fit already do not
   grow a scrollbar.

## Open question for the user

**How wide does the template reach?** The slider and its value are the shape the user described. Applying the
same two-line shape to a checkbox, a dropdown and a text field would make every row of the Admin and
Preferences pages line up identically — at the cost of roughly doubling each row's height, i.e. half the
settings visible per screen on a page that already needed a scrollbar. The stages below build the template for
the slider row and leave the other three kinds on one line until the user answers.

## Stages

1. **S1 — the slider reads as a slider.** Stop hiding a slider's own track, fill and handle
   (`OnlineUiRowGeometry`), tint them from the page's own palette so the filled fraction and both ends are
   visible, and keep CUO's single box around them. Red: a pin that a slider's `Background` / `Fill` / `Handle`
   images keep a non-zero alpha, with a mutation that hides them again.
2. **S2 — the value is an input.** A new element kind (or a paired element) carrying the numeric field with its
   own id and an `edited` action; the field's text and the slider's position written from the same model value,
   the parse policy refused outside the range, and the panel/window machinery unchanged. Red: the parse and the
   two-way write, driven at the model level (the field and the slider are Unity objects and no test in this tree
   builds one).
3. **S3 — the paired-control template.** A Runtime row shape that says "this control is on the line BELOW its
   name" (the wrap rule's own vocabulary), the window and the panels honouring it, and the slider row built
   through it. The stage the user's third ask lands in; the answer to the open question decides whether the
   other three kinds follow in this ticket or in a later one.
4. **S4 — the page's scroll affordance.** A scrollbar on the window's page, built from the game's own art if the
   game ships a scrollable surface to read one from, and a CUO-drawn thin track and handle if it does not (the
   blocker recorded the way the layout pass recorded the colour picker's: the game's screens place their rows by
   hand and their own settings screen does not scroll). Appears only when the content overflows; the window's
   own clamp and the panels' sizes are untouched.

## Evidence the stages start from

- The live geometry of a slider row: `.acceptance/20261015-a/a2-admin-zh-dump.txt` — the slider `rect=120.0x36.0`
  beside its label `780.0`, its `Value` child `56.0` wide, and the control's own children hidden.
- The page that does not fit: `.acceptance/20261015-a/a6-host-prefs-zh.png` (the Preferences page scrolled to
  its picker, with more content below) and the Admin page's own two-section list.
- The row's own prefab art and the one-box rule: the layout ticket's *Fourth pass* section and
  `OnlineUiRowGeometry.HidePrefabBoxArt`'s summary.

## Non-goals

- Not a re-cut of the page designs, the tab row or the window's size.
- Not a change to the wire, the session, the save or any gameplay behaviour: this is geometry, input and
  presentation, exactly as the layout pass was.
- Not a general widget toolkit for other mods' windows.
