# The Online UI's paired controls and its pages' scroll affordance

- Status: Todo — the user's report of 2026-10-14, in three asks, taken while the fixed window was on screen.
- Priority: Medium-High
- Category: Online UI / layout, presentation and input
- Source: The user, 2026-10-14, in their own words: (1) "is there a scrollbar? the Admin page has a part that
  goes past what is shown, and I don't see one on the right"; (2) of the piggyback-weight row: "the slider's
  middle has no thin line, so you cannot see where its ends are, and the value area beside it should be
  editable, with the two bound together so they stay in sync — that would make the UI more perfect"; (3) of the
  paired control in general: "this kind of paired control could be made into a template: the name on the first
  line, the slider and the value on the second; squeezing it onto one line is not pretty, and when the names
  differ in length it looks ragged".
- Related: `todo/online-ui-layout-and-input-detail-pass.md` (the pass this follows: the row's geometry, the
  one compact control height and the row's own prefab art are its work), `done/online-ui-art-and-controls-overhaul.md`
  (the surface these controls are built on), `docs/evidence/acceptance/online-ui-layout-and-input-detail-pass-20261015-a.md`
  (the frames that show the two shapes the asks are about)

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
