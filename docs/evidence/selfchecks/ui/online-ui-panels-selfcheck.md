# The Online UI's last two IMGUI panels — self-check (2026-09-26)

Ticket `online-ui-art-and-controls-overhaul`, stage **S5 — the last player-facing IMGUI panels**. The
stage's premise was that the quick panel and the in-world player context menu were the last consumers of
the scoped-rectangle blocking built for an IMGUI-only Online UI, so moving them onto the game's own
controls retires that machinery in the same round rather than leaving it behind. This page records what the
stage rests on, what landed, what was audited around it, what the independent review found, and what only a
game run can judge.

## 1. Mechanism inventory — what the stage rests on

| # | Mechanism | Evidence (quoted / cited) |
|---|---|---|
| 1 | CUO's surface is a live canvas under the game's, and it is where a panel can be built | `OnlineUiSurfaceHost.EnsureSurface`: `root.transform.SetParent(parent, worldPositionStays: false)`, `root.AddComponent<GraphicRaycaster>()`, then `_quickPanel = OnlineUiPanelView.Create(QuickPanelName, root.transform, …)` and `_contextMenu = OnlineUiPanelView.Create(ContextMenuName, root.transform, …)` — the same root the launcher and the window are built under |
| 2 | A nested canvas is not resized by Unity, so the surface's rect had to be stated | `EnsureSurface` sets `var canvasRect = (RectTransform)root.transform;` with `anchorMin = Vector2.zero`, `anchorMax = Vector2.one`, `offsetMin/offsetMax = Vector2.zero` — without it, `OnlineUiLauncherView`'s top-right anchor, the window's centred rect and the panel's dock are placed against an arbitrary rect (the premise S2a's rects already assumed, unpinned until now) |
| 3 | The game's own art is read in one place | `OnlineUiControlFactory.ReadRowTemplate` (moved out of `OnlineUiWindowView` so the panel view can call it too): instantiates `Special/GameSettingLanguage`, reads the label's `font`/`fontSize` and the row's `sprite`/`Image.type`/`pixelsPerUnitMultiplier`, and destroys the template in the same call |
| 4 | The rows reuse the page machinery, not a second renderer | `OnlineUiPanelView.ApplyRow`: `_rows.Add(new OnlineUiWindowRowView(LineSpacing));`, `var lines = OnlineUiRowLayout.LineOf(widths, ContentWidth, LineSpacing);`, `OnlineUiControlView.Create(element, parent, _typography, _report);` — the same three the window's pages use |
| 5 | Both panels are models, built in the plugin | `OnlineUiQuickPanel.Build` returns `OnlineUiPanelModel.Docked(ctx.T("quick.title"), OnlineUiControlIds.QuickPanelClose, Width, page.Rows)`; `OnlineUiPlayerContextMenu.Build` returns `OnlineUiPanelModel.AtPoint(BuildContextTitle(ctx, row), _position.x, Screen.height - _position.y, Width, page.Rows)` — the click's GUI-space point converted to screen space before it crosses the seam |
| 6 | One action table for three surfaces | `OnlineUiOverlay.BuildSurfaces` clears `_actions` once and calls `_window.Build(ctx, _actions)`, `_quickPanel.Build(ctx, _actions)`, `_contextMenu.Build(ctx, _actions)`; `Apply` is the one lookup (`if (!_actions.TryGetValue(intent.ControlId, out var action))`); `OnlineUiPageBuilder`'s `Key(id) => id.Length == 0 ? "" : _idPrefix + id` namespaces each panel's ids (`quick.`, `menu.`) while keeping an empty id meaning "no control" |
| 7 | The member card has ONE builder | `OnlineUiMemberListDrawer.Build(ctx, page, rows)` serves the Players page and the quick panel's single target row; the IMGUI twin (`BuildImgui` + `DrawImguiButton`) and the `OnlineUiTheme.Label()` style it used are deleted — the eligibility rules (`AdminActions`, `InteractionActions`) are answered once |
| 8 | Every pointer fact is a surface poll | `OnlineUiLauncherView.PollHover`, `OnlineUiWindowView.PollPointer`, `OnlineUiPanelView.PollPointer` — all three: `RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera)` compared against the last answer, one intent per FLIP; `OnlineUiSurfaceHost.Push` polls the window and both panels every frame, and `DestroySurface` queues a hover-left for all four facts |
| 9 | The census answers without geometry | `OnlineUiPointerCensus.BlocksWorldPing() => ModalSurfaceOpen \|\| BlocksWorldMenu();` and `BlocksWorldMenu() => OverLauncher \|\| OverWindow \|\| OverQuickPanel \|\| OverContextMenu;` — the two callers: `LocationPingInputHandler.TryHandle` → `_overlay.IsPointerOverUi()`, `OnlineUiOverlay.Draw` → `_contextMenu.HandleInput(ctx, _pointerCensus.OverContextMenu, BlocksWorldMenu)` |
| 10 | What retires, and who its consumers were | `INativeInputBlocker.SetOnlineUiScopedBlocks` (called only from `OnlineUiOverlay.Draw`), `OnlineUiBlockRect` (only the port signature, the guard and the filter), `OnlineScopedRaycastFilter` (only the guard's scoped creator), `OnlineMenuInputGuard.SetScopedBlocks`/`CreateScopedBlockers`/`DestroyScopedBlockers`/`ScopedBlocksEqual`, `OnlineUiOverlay.CollectOverlayRects`/`FromRect`, and the panels' own rectangles (`OnlineUiPlayerContextMenu.Contains`/`Bounds`/`_lastRect`, `OnlineUiQuickPanel.Bounds`/`_rect`) — every one is deleted, and `OnlineUiInputBlockingPinTests.TheRetiredRectangleApiIsGoneFromTheSource` scans `src/` for the three names |
| 11 | The gesture paths the stage must not change | the middle-click ping (`LocationPingInputHandler`), the in-world right-click (`OnlineUiPlayerContextMenu.HandleInput`, `RemoteTargetPicker.Find`), the quick panel's ESC (`OnlineUiQuickPanel.HandleInput`) with `SetOnlineUiEscapeSurfaceVisible`'s pause suppression, the target pickers (`QuickPanelTargetPicker.Resolve`), the local `member.get_down` row, and the console's mutual exclusion (`ToggleWindow`) |
| 12 | What only the game can show | whether the game's row prefabs behave inside a content-sized panel, whether a click/typing/wheel reaches the panels at the game's UI scale, whether the docked corner and the menu's own corner are where the player expects, and whether CUO's canvas really covers the game's |

## 2. What landed

- **One panel view, two panels.** `OnlineUiPanelView` (GameAdapter) is the window's shell without the
  window: the game's prefab gives it the frame and the typography, the row machinery gives it the rows, and
  the two hover kinds it reports come from its caller — because each panel is its own fact in the census.
  Its height is the content's (`ContentSizeFitter`) and its width is the model's.
- **The panels are models.** The quick panel keeps its dock, its width (340), its target selector and its
  local `member.get_down` row; the context menu keeps its title (name + dead marker), its candidate
  selector and its eleven eligibility-driven actions, and closes on the pick the way the IMGUI button did.
  The IMGUI member card, the hand-rolled button-height measuring and the `GUIStyle` menu button are gone.
- **One table, namespaced ids.** `OnlineUiOverlay.BuildSurfaces` clears one action table per frame and the
  window and both panels register into it; `OnlineUiPageBuilder` gained the id prefix that keeps
  "the quick panel's Carry on this member" and "the Players page's Carry on this member" two intents.
  `OnlineUiWindow.Apply` moved up to the overlay with the table, so an id the frame no longer offers is
  dropped in one place (and logged at Information, where the plugin's default level shows it).
- **The census lost its geometry.** All four facts are polls; both questions are parameterless; the GUI-space
  rectangle value, the raycast filter and the port member retired with them. The one place a rectangle used
  to be read in the plugin — the context menu's click-away close — now reads the menu's own census fact.
- **The placement rule is pure, and it reasons in ONE unit.** `OnlineUiPanelPlacement.ForPointer` clamps the
  menu's corner (pointer offset, both edges, and a too-tall panel hanging from the top so its first rows
  stay visible) and is covered by seven facts without a Unity runtime; the adapter lays the panel out first
  (`LayoutRebuilder.ForceRebuildLayoutImmediate`) so the rule clamps against the real height, and converts
  the pointer into the canvas once so the pointer, the panel's size and the bounds are all in canvas units.
  **The first cut did not**: it passed a screen-pixel pointer beside canvas-unit sizes, which holds only at
  a canvas scale of 1 — the independent review's one MAJOR finding (F1), fixed here with a mutation row
  that fails if the two units are mixed again.
- **The surface covers the game's canvas.** Stated and pinned here rather than left to play, because every
  anchored control the three migrated surfaces place depends on it.
- **Deleted in the same round.** The scoped-block machinery end to end, `CollectOverlayRects`, the panels'
  own rectangles, the IMGUI member card, and the theme's panel frame and its now-unused styles.

## 3. Whole-family audit — what else the same pattern touched

| # | Sibling | Verdict |
|---|---|---|
| 1 | `OnlineUiTheme` | The panel frame (`Panel`, `PanelLight`, `Border`, `DrawBackground`, `DrawFrame`) and the styles the two panels used (`CloseButton`, `Tab`, `Label`, `Section`) lost their last consumers with this change and are deleted. `Button`, `MutedLabel`, `Status`, `DrawOverlayBackground` and the palette stay: the console overlay and the world-space overlays still use them |
| 2 | `OnlineUiLauncherFadeTests` | Its census listed three themed surfaces; one is left, so the pin now asserts the console overlay's four call sites and a theme ceiling of exactly ONE blended `GUI.DrawTexture` — the two "panel frame" mutation controls became one "a second rectangle appears" control |
| 3 | `OnlineUiWindowSurfacePinTests` | Two anchors moved with the action table (`OnlineUiWindow.cs` → `OnlineUiOverlay.cs` for the dropped-intent rule, `_actions[` → `actions[` for the close registration) and one with the template reader (`OnlineUiControlFactory.ReadRowTemplate`); the review found a third row whose mutated file landed in the wrong matcher slot (F3), so that matcher now routes the broken source by the type it declares |
| 4 | `OnlineUiSurfacePinTests` | The frame-push anchor became the three-model push, and the mutation that hides the window became "the surfaces record carries nothing" |
| 5 | `OnlineUiInputBlockingPinTests` | The scoped-block pin retired with the mechanism; in its place: both panels' census facts, both panels polled, the four-fact retraction, and a tree scan that keeps the retired rectangle API retired |
| 6 | `AdapterCapabilityPortShapeTests` / `OnlineMenuInputGuardContractTests` | The port's member list and the composition census (19 → 18) moved with the retirement, and the guard contract gained the negative — a scoped setter regrown on the guard fails |
| 7 | `OnlineUiColorPickerPinTests`, `OnlineUiConsolePageRemovalPinTests` | Untouched and green: the member card's colour tag, the colour edit's lifetime, the window's tab row and its six-case page dispatch are all unaffected by the move |
| 8 | The wire, the session and the saves | Untouched: no message, no protocol number (43) and no stored shape changed — this stage is presentation and input only |
| 9 | `OnlineUiBlockRectTests` | Deleted with its type; the rectangle semantics it pinned (which edges belong to the surface) are gone with the rectangles, because nothing tests a rectangle any more |
| 10 | The panels' kept geometry and identity | The review's F9: nothing pinned the panels' rects or their close id, so a changed constant kept every pin green. Three pins + five mutation rows now hold the quick panel's 340/16 dock, the menu's 240, the quick close id being its own (not the window's) and the frame swallowing the pointer |

## 4. Verification — the ladder

| Step | Result | Artifact |
|---|---|---|
| Red before the change | **39 failed / 44 passed / 83 total** with the new pins run against the pre-change source (`git stash` of `src/`, the two tests that reference the new Runtime types set aside). Caveat, recorded: four rows whose file did not exist pre-change (`OnlineUiPanelView.cs`) failed with a file-not-found rather than a matcher assertion, so the red file alone does not show those four failing for the right reason — their mutation controls do, on the frozen tree | `%TEMP%/cuo-s5-red.txt` |
| Build | 0 warnings / 0 errors | `%TEMP%/cuo-s5-build5.txt` |
| Focused run (`OnlineUi`, `AdapterCapabilityPortShape`, `OnlineMenuInputGuardContract`) | 341 / 341, exit 0 | `%TEMP%/cuo-s5-focus5.txt` |
| Normative gates | 288 / 288, exit 0 (287/288 before the checklist was filled: the checklist gate itself) | `%TEMP%/cuo-s5-gates4.txt` |
| Format | `dotnet format` exit 0, recorded in the artifact; the touched files are CRLF with 0 bare LF (checked at byte level, after `write` created two of them LF-only — the review's F11) | `%TEMP%/cuo-s5-format2.txt` |
| Full suite WITH build | **4348 + 288 passed, 0 failed**, exit 0 | `%TEMP%/cuo-s5-full2.txt` |
| Mutation controls | 20 real-source mutation rows in `OnlineUiPanelSurfacePinTests` + 16 in `OnlineUiInputBlockingPinTests`, each re-run on the frozen tree | the pin classes' own run |

### 4.1 Full suite

`dotnet test CasualtiesUnknownOnline.slnx` (WITH build, the final ladder step): **4348 passed / 0 failed**
in `CasualtiesUnknownOnline.Tests` and **288 passed / 0 failed** in the normative gates, exit 0 —
`%TEMP%/cuo-s5-full2.txt`.

## 5. Limits — what this evidence cannot say

- **No test in this tree instantiates a `GameObject`.** Nothing here proves the panels render, that the
  game's row prefabs behave inside a content-sized panel, that a click reaches them, or that the two
  corners are where the player expects. Those are the user's run.
- **The placement rule's unit is now the canvas's, and the canvas's scale is unread.** `PlayerCamera.uiScale`
  is the game's own name for the surface's scale and S1's probe still has to read it; the rule is correct in
  canvas units whatever that scale is, but the *apparent* size of the margin and the pointer offset (and
  therefore how close to the edge the menu sits) is a run observation.
- **The canvas-rect change is a premise fix.** It is pinned as source shape; that the game's canvas rect and
  scale are the ones the player sees is a run observation, and the change lands under the already-deployed
  launcher and window as well.
- **The forced layout rebuild is a cost, not a measurement.** `PlaceAtPoint` rebuilds the menu's layout every
  frame while the menu is up; the frame cost is unmeasured (a game run shows whether it is noticeable).
- **The pointer facts are up to two frames old on the ping path** — the same frame accounting S4 recorded
  (`OnlineUiHost.Update` handles the middle-click before the frame's intents are drained and the surface
  polls at the end of the frame) — and the menu's own click-away close reads its fact one frame old.
- **The quick panel's target row wraps by the Runtime's rule now**, not by the IMGUI panel's "four on one
  line, then one per line": with five or more candidates the shape differs (more regular, not identical).
- **The S1 chrome reading is still pending**, so the panels' tints are the window's tints.
- **The ticket is not done**: the world-space overlays still draw with the IMGUI skin's font (S6).

## 6. The independent review, and how each finding was disposed of

The review ran in a fresh context against the frozen tree (report `%TEMP%/cuo-review-s5.md`, 471 lines:
1 MAJOR / 6 MINOR / 5 NIT, all CONFIRMED). It attacked the retirement's completeness, the action table's
lifetime and id space, the panel view line by line, the placement maths, the census and its frame
accounting, the canvas rect, every mutation row's slot, the gesture invariants, the docs against the code,
and the recorded numbers. Verdict: no blocker.

| # | Finding | Disposition |
|---|---|---|
| F1 | MAJOR — the clamp mixed screen pixels with canvas units (correct only at canvas scale 1) | **Fixed**: the pointer is converted into the canvas once and the rule is asked entirely in canvas units; the pin's matcher and two mutation rows hold it |
| F2 | MINOR — the documented pin/mutation counts were inflated (29 actual, 31 claimed) | **Fixed**: counted from the files after this round's additions (10 pins + 20 rows, 9 pins + 16 rows) and corrected in the ticket, this page and the checklist |
| F3 | MINOR — a `OnlineUiWindowSurfacePinTests` row mutated in the wrong matcher slot (pre-existing, re-anchored by S5) | **Fixed**: the matcher routes the broken source by the type it declares, so each row lands in the slot it controls |
| F4 | MINOR — the dropped stale intent was logged at Debug against an Information default | **Fixed**: `LogInformation`, with the reason recorded in the member's doc |
| F5 | MINOR — `OnlineUiMemberAction`'s summary still claimed two renderers | **Fixed**: one renderer, said plainly |
| F6 | MINOR — a cited `%TEMP%` artifact did not exist | **Fixed**: this page now cites the artifacts the ladder actually produced |
| F7 | NIT — decision 230's superseded clause was not marked in place | **Fixed**: marked "SUPERSEDED IN PART by 231" |
| F8 | NIT — the MANIFEST still called the S4 self-check current although S5 supersedes its panel rows | **Fixed**: the row names what S5 supersedes |
| F9 | NIT — the panels' kept rects and their close id were unpinned | **Fixed**: a new pin + five mutation rows (see §3 row 10) |
| F10 | NIT — `ColorSwatchElement`'s "empty id = preview" promise broke under a prefix | **Fixed**: `Key` keeps an empty id empty, and says why |
| F11 | NIT — two touched documents were LF-only, and this page's own CRLF claim was false | **Fixed**: both normalised to CRLF and re-checked at byte level; the claim now names the files |
| F12 | NIT — the ticket's deletion census omitted the panels' own rectangle members | **Fixed**: the ticket lists them |
