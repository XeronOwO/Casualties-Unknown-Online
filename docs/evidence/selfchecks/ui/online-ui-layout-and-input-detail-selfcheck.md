# Self-check — the Online UI's layout and input detail pass

Ticket: `docs/backlog/review/online-ui-layout-and-input-detail-pass.md` (user acceptance pass, 2026-09-27).
Scope: the Online UI window's shell geometry, the geometry inside the game's own rows, how wide a control is,
an open dropdown's popup layer and the pointer surfaces — plus the frame's border, the launcher's idle floor
and the colour field's live correspondence. No wire, save, session or gameplay change.

## 1. What the user reported, and what each finding became

| # | Finding (2026-09-27) | Change | Evidence |
|---|---|---|---|
| 1 | The body of a page touches the tab row | The three bands are children of ONE `VerticalLayoutGroup` with declared heights and the layout's own spacing: the ENGINE stacks them and the first cut's arithmetic is deleted | `OnlineUiWindowLayoutTests` (6 facts, incl. the first cut's 66 as the rejected sample), `OnlineUiLayoutDetailPinTests.ThePageStartsBelowTheTabStrip` + 2 mutation rows |
| 2 | Button sizes | Every non-label control takes `OnlineUiWindowLayout.ControlHeight` (30, the tab strip's own height) instead of the game's row prefab's authored height, and its width is the engine's measurement above the model's floor | `OnlineUiLayoutDetailPinTests.EveryControlOfAPageIsTheSameCompactHeight` + 1 mutation row; `OnlineUiWindowLayoutTests.EveryControlSharesTheTabStripsHeight` |
| 3 | A dropdown/clip-through, wrapped text over its neighbours | `OnlineUiControlSizing` measures the content, so the model's width is a floor; a label leaves its height to TMP with a one-line floor in `minHeight`; `OnlineUiRowGeometry` lays the row's own label and control out | `OnlineUiLayoutDetailPinTests.AControlTakesWhateverItsOwnContentNeeds` (2 mutations), `.ALabelIsAsTallAsItsWrappedText`, `.TheRowsInsideIsCuosGeometry` (2 mutations) |
| 4 | An open dropdown's options are behind the window and cannot be clicked | `OnlineUiDropdownPopup` — a canvas on the window sorted one step above CUO's surface, with its own raycaster; every dropdown's template is adopted into it and an open list is normalised every frame | `OnlineUiLayoutDetailPinTests.AnOpenDropdownListRidesTheWindowsPopupLayer` + 2 mutation rows |
| 5 | The interface could be larger (the frame, not the text) | `OnlineUiWindowLayout.Width/Height` 780×540 → 1000×700, clamped to the canvas by `FitToCanvas`; fonts and control heights unchanged | `OnlineUiWindowLayoutTests.TheFrameIsLargerThanTheOneItReplaces`; the clamp is code-read, unpinned (see §4) |
| 6 | `Preferences` runs past its tab | The tab width is a floor and the engine measures the caption through the button's own content group (`OnlineUiControlSizing.EffectiveWidth` reads `LayoutUtility.GetPreferredSize`) | `OnlineUiLayoutDetailPinTests.AControlTakesWhateverItsOwnContentNeeds`; `OnlineUiPageBuilder.TabWidth`'s doc re-stated as a floor |
| 7 | The colour page is misaligned | The colour rows inherit the row geometry and the measured widths; the palette and the hex field stay as they are (the user's decision) | same pins as 3; `OnlineUiColorPickerPinTests` unchanged and green |
| 8 | Most text boxes cannot be clicked (the user's priority) | uGUI hit-tests graphics: a field's or a dropdown's own object is given a graphic that accepts the raycast (`OnlineUiRowGeometry.EnsurePointerSurface`), a field's viewport and text are stretched onto the box CUO sizes, and the window logs a warning once per kind when it had to fix one | `OnlineUiLayoutDetailPinTests.AControlWithNoPointerSurfaceIsGivenOneAndReported` + 2 mutation rows; the warning is the runtime trace to read in the user's log |
| 9 | The hex field does not follow a picked colour | The field's value is the carried colour's canonical hex, live; `PlayerColorInput` holds only text that is not a colour yet | `OnlineUiColorPickerPinTests.TheFieldMirrorsTheColourThePlayerCarries` + 1 mutation row |
| 10 | The game's windows have a white border, this one does not | `OnlineUiControlFactory.MakeFrame` keeps the sprite untinted on the frame's own image and lays the dark fill inside it; used by the window and both panels | `OnlineUiLayoutDetailPinTests.TheFrameKeepsTheGamesOwnBorder` + 2 mutation rows |

Self-found in the same pass (the user invited them): the launcher's idle floor (`OnlineUiLauncherFade`
0.35 → 0.12, idle window 4 s → 2.5 s, because the top-right corner holds the medical panel's readout); the
title-bar drag keeps a strip of the window on the canvas; the current tab and transport choice take a wash
on the game's own sprite, not only a coloured caption.

## 2. Mechanism inventory (what was touched, and where its evidence is)

| Mechanism | Change | Evidence |
|---|---|---|
| `OnlineUiWindowLayout` (new, Runtime, pure) | The shell's band heights and rhythm, the padding, the frame's size, the one control height | `OnlineUiWindowLayoutTests` 8 facts |
| `OnlineUiWindowView` | Bands DECLARED into one `VerticalLayoutGroup` (the engine stacks them); the frame's border and fill; the canvas clamp; the engine's widths feed the wrap; per-row view sync; popup layer wiring | `OnlineUiLayoutDetailPinTests` (3 pins) + the existing `OnlineUiWindowSurfacePinTests` (re-anchored, green) |
| `OnlineUiControlSizing` (new) | The width policy: the model's width is a floor, the content decides | `OnlineUiLayoutDetailPinTests.AControlTakesWhateverItsOwnContentNeeds` |
| `OnlineUiRowGeometry` (new) | The row's inside: the label, the control, the slider's value, the control's own insides, the pointer surface | `OnlineUiLayoutDetailPinTests.TheRowsInsideIsCuosGeometry`, `.AControlWithNoPointerSurfaceIsGivenOneAndReported` |
| `OnlineUiDropdownPopup` (new) | The popup layer and the template adoption/normalisation | `OnlineUiLayoutDetailPinTests.AnOpenDropdownListRidesTheWindowsPopupLayer` |
| `OnlineUiControlView` | The compact height, the measured width, the wrapped-label height, the selected wash | `OnlineUiLayoutDetailPinTests` (3 pins) + `OnlineUiWindowSurfacePinTests.WritesOnlyChanges` (re-anchored) |
| `OnlineUiPanelView` | The same frame treatment and row geometry for the quick panel and the context menu | `OnlineUiPanelSurfacePinTests` untouched and green; the two panels' own pins still pass |
| `OnlineUiPreferencesDrawer` | The hex field mirrors the carried colour | `OnlineUiColorPickerPinTests.TheFieldMirrorsTheColourThePlayerCarries` |
| `OnlineUiLauncherFade` | The idle floor and window | `OnlineUiLauncherFadeTests` (unchanged assertions over the constants; green) |
| `OnlineUiElementModel` / `OnlineUiPageBuilder` | The width's documented meaning: a floor, not a hint | the two docs; no API change |

## 3. Red, then green

- **Red observed for the shell rule**: with `OnlineUiWindowLayout.PageTop` temporarily set to the first cut's
  value (66), `OnlineUiWindowLayoutTests` failed exactly as the user described —
  `the page's top edge (66) must clear the tab strip's bottom edge (76) by the page's gap (10)` — and 4 of its
  6 facts stayed green. `PageTop` was restored to `TabBottom + TabGap` immediately after
  (the run's output: `失败: 2，通过: 4`).
- **Red observed for the pins**: every pin of `OnlineUiLayoutDetailPinTests` carries real-source mutation rows
  (14) and `OnlineUiColorPickerPinTests` gained one; the mutation harness asserts the anchor is present in the
  real file, that the replacement changes it, and that the pin's own matcher then returns false. A mutation
  row that stops tripping its pin is a red test in the suite itself.

## 3b. The independent review's dispositions

The adversarial review (frozen tree, `%TEMP%/cuo-review-ui-detail.md`) confirmed what it could run (build, the
focused `~OnlineUi` set green, the fast net48 suite green, all 61 pin mutation anchors real) and found four
defects, fixed in the same round: an element sharing its line took the line's surplus (now `aloneOnItsLine`);
a checkbox's box kept the prefab's authored spot (now centred in the compact control); the Worlds page's
heading was a raw row and missed the section room (now `Section` with the heading's own trailing control); and
the frame's fit to the canvas ran once (now re-measured when the canvas rect changes). It also corrected one of
its own findings after re-deriving the drag clamp, and three pins were tightened at its suggestion.

## 4. Limits (what this cycle cannot prove)

- **The pixels and the clicks are the user's run.** No test in this tree instantiates a `GameObject`; whether
  the game's row prefabs behave under CUO's group, whether the popup lands over the window, whether the
  border and the enlarged frame read well and whether the fields now take a click are game observations.
- **The click fix is structural and instrumented.** The defect was reported from a run this tree cannot make:
  what is proven here is the shape (the box CUO sizes, the raycast surface on it, the warning when the game's
  prefab left none). A failed click should be read against that warning in the log.
- **`TMP_Dropdown`'s own parenting is not visible from this tree.** The template is re-parented and given its
  own canvas, sorting order and raycaster, and an open list is normalised every frame, so the outcome does not
  depend on which parent the library picks — which one it picks is still unverified here.
- **uGUI has no wrapping layout, so one decision stays CUO's.** A `HorizontalLayoutGroup` lays one line out and
  never wraps, and `GridLayoutGroup` is cell-based: the grouping of a row's elements into lines is decided by
  the Runtime's own rule (`OnlineUiRowLayout`) and executed with one real layout group per line, where the
  engine takes over again (flexible widths, alignment, sizes). That is the boundary of "the engine lays it
  out" here — everything else is declared, measured or placed by uGUI.
- **The two panels have no popup layer of their own** (the review's latent finding): a dropdown added to the
  quick panel or the context menu would need the window's treatment, and neither carries one today.
- **The canvas clamp and the drag clamp are unpinned guards.** They are simple and reversible
  (`FitToCanvas`, `OnlineUiWindowDragHandler.Reach`); the felt behaviour at a small resolution is a run fact.
- **The control height overrides the game's own authored row height.** The tab strip already ran at 30 with
  the same prefab and reads well in the user's own screenshots, which is why 30 was chosen; a prefab whose
  9-slice cannot take that height would show as a squashed frame in the run.
