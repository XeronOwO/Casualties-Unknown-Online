# The Online UI's window family on the game's own surface — self-check (2026-09-26)

Ticket: `docs/backlog/in-progress/online-ui-art-and-controls-overhaul.md` (High; **stage S2b**, the second
half of the stage the ticket calls S2). Cycle scope: the window S2a made reachable stops being an IMGUI
panel of its own and becomes a display list the game's own controls render — the shell, the tab row and
all six pages. The launcher's mechanism (the surface, the intent channel, the polled pointer) is inherited
unchanged; what this stage adds is the window's model, the adapter's control kit and the plugin's page
builders.

No wire, protocol or save change (protocol stays **43**). The four values S1's probe logs still come from
one game run; nothing here depends on them, because every control and every glyph now comes from the
game's own prefabs.

## 1. Mechanism inventory — what the window family rests on

| # | Mechanism | Evidence (quoted / cited) |
|---|---|---|
| 1 | The game's own settings rows, and what each prefab carries | `SettingsMenu.cs`: `Utils.Create("Special/GameSettingFloat", this.content)` then `g.transform.GetChild(1).GetComponent<Slider>()`, `GetChild(2).GetComponent<TextMeshProUGUI>()` for the value; `GameSettingInt` → child 1 `TMP_InputField`; `GameSettingDropdown` → child 1 `TMP_Dropdown` (`AddOptions` + `SetValueWithoutNotify` + `onValueChanged`); `GameSettingBool` → child 1 `Toggle` (`SetIsOnWithoutNotify`); `GameSettingLanguage` → a `Button` on the root with the caption on child 0. Every row also sets child 0's `TextMeshProUGUI.text` — so child 0 is the row's label and child 1 its control |
| 2 | The rows are placed by hand in the game's own screen | the same `SettingsMenu` positions each row with `g.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -num - …)` and sizes it from its own `sizeDelta` — the prefabs carry no layout group of their own, and a layout group reads nothing off a rect, which is why the adapter seeds that size (see §2) |
| 3 | The destructive prefab rule still holds | `Special/SettingsMenu` builds the whole settings screen and claims the static instance; the window never loads it (`OnlineUiNativeHostPinTests.NoSourceInTheAdapterOrThePluginLoadsTheSettingsMenuPrefab` scans both trees). `Special/Console` is the other forbidden one: `ConsoleScript.cs` instantiates it for the in-game console and the class carries the whole cheat console |
| 4 | The live surface S2a proved | `OnlineUiSurfaceHost`: CUO's canvas under the game's own canvas, active, sorting `30_000`, with a `GraphicRaycaster`, rebuilt when its canvas is gone, EventSystem only when the scene lacks an enabled one; `OnlineUiLauncherView` is the first control on it |
| 5 | The intent channel | `OnlineUiIntent` (kind + control id + payload), `IOnlineUiSurface.TryDequeueIntent`, and the plugin's `OnlineUiHost.DrainSurfaceIntents` — the surface reports facts, the plugin decides what they mean |
| 6 | The polled pointer fact | uGUI's enter/exit callbacks fire on pointer movement, so both the launcher's hover and the WINDOW's own rect are polled with `RectTransformUtility.RectangleContainsScreenPoint` (`OnlineUiLauncherView`, `OnlineUiWindowView`) |
| 7 | The modal guard is unchanged | `OnlineUiHost.Update` still drives `SetOnlineUiModal` from the window's visibility, and the ESC path (`CuoEscCloseSuppression`) still runs in the IMGUI pass — the window's move to uGUI did not change when the guard is on |
| 8 | The game's own UI scale is the canvas' | the surface hangs under the game's canvas, so the window's 780×540 and every width hint are canvas units that scale with `PlayerCamera.uiScale` (the ticket records the launcher's version of the same fact) |
| 9 | The in-world right-click guard needs the window's rect | it used the IMGUI window's rect (`OnlineUiWindow.ContainsPoint`); with the window on uGUI the rect is the surface's fact, so the window view polls it and the plugin keeps a `_pointerOverWindow` flag — one frame of latency, recorded in §7 |

## 2. What landed

- **Runtime, pure (7 new files)** — the display list the window is: `OnlineUiElementKind` (label, button,
  text field, toggle, dropdown, slider), `OnlineUiTextStyle` (default, muted, section, title),
  `OnlineUiElementModel` (one flat record with the per-kind meaning documented, plus a factory per kind so
  a call site never spells the unused fields out), `OnlineUiRowModel` (an empty row is a gap),
  `OnlineUiWindowModel` (title, tabs, rows), `OnlineUiControlIds` (`window.close` — the one id the shell's
  chrome and the plugin both address), and `OnlineUiRowLayout.LineOf` — the greedy wrap rule, spacing
  included, so whether twelve action buttons form two lines or three is decided and tested without a Unity
  runtime. `OnlineUiFrame` gained the window (null = closed); `OnlineUiIntent` gained the control id and
  the payload fields; `OnlineUiIntentKind` gained the five control interactions and the window's pointer
  flip.
- **GameAdapter (4 new files + the surface)** — `OnlineUiWindowView` (the shell: frame, title bar, close
  control, tab row, `ScrollRect` + `VerticalLayoutGroup` content, and the per-frame reconcile that reuses a
  control by kind and id and destroys the ones the model dropped), `OnlineUiControlView` (one element on
  one of the game's own row prefabs — re-applied only where it changed, reporting its CURRENT id, and
  seeding the prefab's own size into the layout so no row can collapse), `OnlineUiWindowRowView` (the line
  containers a wrapped row owns), `OnlineUiWindowDragHandler` (the title bar drag the IMGUI window had),
  and `OnlineUiSurfaceHost` now creates the window next to the launcher, shows it only while the frame
  carries a model, and polls its rect.
- **Plugin (the six pages became builders)** — `OnlineUiPageBuilder` is what a page writes to: rows and
  controls, with every interactive control registering the action its id carries — so the model goes to the
  surface as a value and the intent that comes back is dispatched to the same registration.
  `OnlineUiWindow` no longer draws: it builds the tab row, dispatches the page (the `switch (_state.Page)`
  the console-page pin anchors on) and applies intents. The six drawers became
  `OnlineUi*Drawer.Build(ctx, page)`; the Preferences page's hand-rolled "button that lists buttons" became
  the game's own `TMP_Dropdown` (and its three open/closed booleans died), Admin's parity toolbar became a
  dropdown, Admin's flags and weight became the game's checkbox and slider rows, Home's transport switch,
  both host/join forms and every field became rows, and the member cards' eligibility is answered once
  (`OnlineUiMemberListDrawer.AdminActions` / `InteractionActions`) for both the model and the quick panel's
  remaining IMGUI card.
- **Deleted in the same round** — the IMGUI window and its `GUI.Window`, the theme's window/title/label
  styles, `OnlineUiWindowState.Scroll` and its three dropdown booleans, the overlay's 21-parameter draw
  call (it now takes the frame's context) and the 20 services it used to receive.
- **Tests** — `OnlineUiWindowSurfacePinTests` (14 pins + 16 real-source mutation rows),
  `OnlineUiRowLayoutTests` (9), one new mutation in `OnlineUiSurfacePinTests` (a plugin that pushes no
  window), and the two re-anchored pin sets below.

## 3. Family audit — what else touches this mechanism

| Surface | Verdict |
|---|---|
| The six pages | All six moved; none kept a second renderer. The quick panel is the one IMGUI consumer of the member card left, and it reads the same eligibility methods rather than its own copy |
| The pins that had to move with it | `OnlineUiLauncherFadeTests`: the theme census dropped the window's row (three surfaces left: quick panel, context menu, console overlay), the blended-frame contract untouched. `OnlineUiConsolePageRemovalPinTests`: the tab row and the page dispatch re-anchored from `DrawTabs`/`Draw(ctx)` to `BuildTabs`/`Build(ctx, page)`, contract unchanged (exactly six tabs, `tab.<page>` keys read from source, one case per page). `OnlineUiSurfacePinTests`: the push carries the window again as one whole-expression anchor, with a negative sample. `OnlineUiNativeHostPinTests` untouched (the S1 probe); `AdapterCapabilityPortShapeTests` untouched and green (14 ports / 19 members — no port was added) |
| The remaining IMGUI surfaces | Untouched: the quick panel, the in-world context menu, the console overlay, the nameplates/arrows and the network HUD draw exactly as before |
| The modal guard and the ESC path | Unchanged: `IsWindowVisible` still drives `SetOnlineUiModal`, the ESC handling still lives in the overlay's IMGUI pass, and the window's own × is a control whose click is the shell's fact |
| Input blocking | The window's frame is a raycast target on CUO's canvas, so a click inside it lands on the surface instead of the world; the scoped IMGUI blocks still cover the quick panel and the context menu |
| Other adapter ports | Untouched; the window rides the existing `IOnlineUiSurface` |
| Mod UI windows, console, chat | Untouched |
| Wire, protocol, save, gameplay | None: no `NetMsg`, no save field, no protocol change (protocol stays 43) |

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | The wrap rule packs, wraps, counts the layout's spacing, never loops and treats a natural-width element as nothing | `OnlineUiRowLayoutTests` 9 cases (empty, fits, wraps 5×180 into 2/2/1, natural width, an over-wide element, the spacing that tips two 200-wide buttons onto two lines, the same two without it, a degenerate content width, line numbers never skip) |
| 2 | Every control of the window is the game's own row prefab, and the frame's art and typography are read from it | `OnlineUiWindowSurfacePinTests.TheWindowsControlsAreTheGamesOwnRowPrefabs` (all five prefab paths + the template read) + its mutation row |
| 3 | A control with no width hint still takes the prefab's own size | Same class: `AControlTakesTheGamesRowSizeWhenTheModelGivesNone` + its mutation row (the seeding is what keeps a row from laying out at zero height, since the game's rows are hand-placed and a layout group reads nothing off a rect) |
| 4 | The frame is the only thing that opens and closes the window, and the rect is polled either way | Same class: `TheWindowIsShownOnlyWhileTheFrameCarriesAModel` + two mutation rows |
| 5 | The window hangs on CUO's canvas, next to the launcher | Same class: `TheWindowHangsOnTheSameCanvasAsTheLauncher` + its mutation row |
| 6 | The rows are wrapped by the Runtime's rule, against the window's own content width and its line spacing | Same class: `TheRowsAreWrappedByTheRuntimesOwnRule` + its mutation row |
| 7 | A control is written only where it changed | Same class: `AControlIsWrittenOnlyWhereItChanged` + its mutation row |
| 8 | A focused text field is never overwritten by the model | Same class: `ATextFieldIsNotOverwrittenWhileThePlayerTypes` + its mutation row |
| 9 | Every interactive control reports its id at the moment of the interaction | Same class: `EveryInteractiveControlReportsItsOwnId` + its mutation row |
| 10 | The window's own rect is the polled pointer fact | Same class: `ThePointerFactIsPolledFromTheWindowsRect` + its mutation row |
| 11 | The frame swallows the pointer so a click cannot reach the world behind it | Same class: `TheWindowStopsTheWorldBehindItFromBeingClicked` + its mutation row |
| 12 | The title bar drags the window | Same class: `TheTitleBarDragsTheWindow` + its mutation row |
| 13 | Every intent kind has its own case, enumerated from the enum rather than listed by hand, and a control intent reaches the action table; one whose control is gone is dropped | Same class: `ThePluginDispatchesEveryIntentKind` (a census over `OnlineUiIntentKind`), `AnIntentForAControlTheWindowNoLongerOffersIsDropped` + their mutation rows |
| 14 | The close control is the shell's, its meaning is the plugin's, and both address one id | Same class: `TheCloseControlIsTheShellsAndItsMeaningIsThePlugins` + its mutation row |
| 15 | The console page stays removed in the uGUI world | `OnlineUiConsolePageRemovalPinTests` 21 cases: the page enum, the six-tab tab row (now a model build), the six-case dispatch, the deleted drawer, the command-buffer census, the `tab.`/`console.` key census in both languages, and 12 rejection samples plus one tolerance sample |
| 16 | The launcher's fade and the theme's blended-frame census survive the window's departure | `OnlineUiLauncherFadeTests` 13 cases: the idle matrix unchanged, the census now three surfaces, the theme's blend flag and draw ceiling unchanged |
| 17 | The seams stay consistent with their census | `AdapterCapabilityPortShapeTests` 20 cases green with no port added (14 ports / 19 members) |
| 18 | Gates, build and structure | build 0 warnings / 0 errors; focused `FullyQualifiedName~OnlineUi\|FullyQualifiedName~AdapterCapabilityPortShape` 232/232; the largest touched file `OnlineUiControlView.cs` 556 lines, `OnlineUiOverlay.cs` 530, `OnlineUiWindowView.cs` 505 — all under the 600-line gate; `dotnet format` exit 0 |

## 5. Red, ladder and the numbers

This stage moves a mechanism rather than fixing a defect, so the red it can show is the CONTRACT it moves:
the four pin sets above were re-anchored in the same change, and the sixteen mutation rows of
`OnlineUiWindowSurfacePinTests.Mutations` are what proves the replacements discriminate — each row asserts
its anchor is in the REAL file (so a text drift cannot turn it into a tautology), applies it to the frozen
source and requires that pin's own matcher to fail. The Runtime halves (the wrap rule, the model
factories) are called directly by the unit cases, so they cannot pass without the code.

Ladder on the tree at this stage (the artifacts are the ones the independent review did NOT see, i.e. after
its findings were fixed):

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors (`%TEMP%/cuo-s2b-build9.txt`).
- Focused `FullyQualifiedName~OnlineUi|FullyQualifiedName~AdapterCapabilityPortShape` — 232/232
  (`%TEMP%/cuo-s2b-focus10.txt`).
- Normative gates — 287/288 before the checklist is complete, the single failure being this cycle's
  deliberately reset delivery checklist (`%TEMP%/cuo-s2b-full2.txt`).
- Full suite WITH build — tests project 4245 passed, gate project 287 passed of 288 with the
  delivery-checklist gate failing on the reset (`%TEMP%/cuo-s2b-full2.txt`); the unfiltered run after the
  checklist is complete is recorded at the end of §6.
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0, the exit code recorded in the log itself
  (`%TEMP%/cuo-s2b-format3.txt`).

## 6. Independent adversarial review and dispositions

A fresh-context reviewer read the FROZEN revision — it recomputed HEAD and all per-file md5s of
`git status --porcelain` (41 paths) against `%TEMP%/cuo-s2b-freeze.txt` **before and after** its work and
reported MATCH both times, so the revision it reviewed is provably the one these fixes were applied to. It
re-derived the ladder independently (build, focused runs, the gate run, the full suite WITH build, the line
counts), opened every decompiled citation by explicit path, executed the new mutation theory green, and
diffed every page against HEAD. **No blocker.** Full report: `%TEMP%/cuo-review-online-ui-window-family.md`.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| M1 | major | nothing gave the game's row prefabs a size: a bare `LayoutElement` writes only `preferredWidth`, the game places its rows by hand via `sizeDelta`, and a layout group reads nothing off a rect — so every content row and every zero-hint control could lay out at zero height | landed: the prefab's own `sizeDelta` is seeded into the `LayoutElement` at creation (a prefab that carries its own layout values keeps them; a hand-built label takes the game's font line height and the row's remaining width), with `AControlTakesTheGamesRowSizeWhenTheModelGivesNone` and its mutation row; §7 now states the residual (the prefabs' authored sizes and the resulting look are still the run) |
| m2 | minor | the placeholder for a missing prefab rendered nothing for every non-Label kind, contradicting its own docstring | landed: the placeholder takes the root caption for every kind (a button's stays clickable), the docstring says what it does, and the window logs the miss once per kind through the view's `UsesGamePrefab` — the way S2a's launcher reports its fallback |
| m3 | minor | the wrap rule ignored the spacing its consumer applies, so a row summing to the content width would overflow by (n−1)×4 in the layout | landed: `LineOf(widths, contentWidth, spacing)` counts the gap, the adapter passes its own `LineSpacing`, two cases pin the arithmetic, and the pin's anchor and mutation moved with the signature |
| m4 | minor | the "every control kind is dispatched" pin was five hand-written literals with no reflection, so a sixth kind could be dropped silently | landed: `DispatchesEveryIntentKind` enumerates `OnlineUiIntentKind` from the enum and requires a case label per member, so a new kind fails the pin instead of disappearing at runtime (all ten present kinds pass) |
| m5 | minor | four self-check numbers did not reproduce (console-page cases 19→21, adapter new files 3→4, the overlay's draw call 20→21 parameters) and the `dotnet format` claim cited a log with no exit code | landed: all four corrected here and in the ticket, and the format claim now cites `%TEMP%/cuo-s2b-format3.txt`, whose last line is `exit=0` |
| m6 | minor | the surface pin's push anchor had been weakened from one whole expression to three substrings that need not be one statement | landed: the push is one source line again and the pin matches it whole, with the "pushes no window" mutation still failing it |
| m7 | minor | `OnlineUiElementModel.Enabled` and the whole interactable path were unreachable in production — dead capability rather than a regression | landed: removed from the model, its factories, the builder and the adapter; the write-only-changes pin and its mutation were re-anchored to the width guard |
| nit | nit | Admin's guest rule line and the Preferences profile status had lost HEAD's two-tone rich text | landed: both compose the state word in its own colour inside the body/muted line again |
| nit | nit | §5's forward reference to §6 dangled | landed: §6 is this section, and the numbers above are the post-fix ladder |
| nit | nit | the element list shifted when a view could not be built | landed: creation cannot fail — the placeholder is built inside the view — so the list and the model stay aligned and the branch is gone |
| nit | nit | the removed IMGUI slider dead-band was suspected of changing behaviour | NOT a defect: both paths call the setter only on a real change (the game's own config write is unchanged); recorded so a later cycle does not re-open it |

The reviewer also confirmed what it could not falsify (§7 lists what needs a game run): the child-index
contract against `SettingsMenu.cs` is exact, the Runtime stays Unity-free, all ten intent kinds are handled
and pinned, the frame is the only show/hide path, page parity is clean (no localization key became an
orphan, every `row.Can*` condition and Home range check survives verbatim, the player-colour index shift is
correct), the mutation theory really discriminates, nothing is half-migrated, and the architecture boundary
holds (the plugin binds Runtime + GameAdapter only; the adapter is still the only `Assembly-CSharp`
consumer; the port census is 14/19).

The final unfiltered runs after the checklist is complete: the normative gate project 288/288, exit 0
(`%TEMP%/cuo-s2b-gates-final.txt`), and the full suite WITH build — tests project 4245, gate project 288 —
exit 0 (`%TEMP%/cuo-s2b-full-final.txt`).

## 7. Limits — what this cycle does not prove

- **No Unity runtime in the verification path.** No test in this tree can instantiate a `GameObject`, so
  the window is proven by the compile-time contract, the source pins and the mutation rows — not by a run.
  Whether the game's five row prefabs behave when instantiated ACTIVE (their `Awake`/`OnEnable`, any
  `ContentSizeFitter` or `LayoutElement` that fights the window's layout groups), whether the layout
  produces the shape the IMGUI window had, whether hover/click/wheel reach the controls through the game's
  EventSystem, and whether the result LOOKS like this game: all of that is the user's game run.
- **The control sizes are seeded, not measured.** Each view takes the prefab's own `sizeDelta` into its
  `LayoutElement` (the game places those rows by hand, and a layout group reads nothing off a rect), which
  is what keeps a row from collapsing — but whether the authored size is the size that reads well in this
  window, whether a prefab carries its own layout values, and whether the wrap and the look come out right
  are run observations. This is the first thing to look at in the run.
- **The look is not measured.** The frame reuses the game's own row sprite, `Image.type` and
  pixels-per-unit multiplier, and every label takes the game's font and size — but the tints are CUO's and
  whether that reads as one screen is a judgement only a run can make. The S1 reading that could align the
  chrome colours with the game's is still pending (S1 deployed nothing).
- **The text field's content type is changed on the game's own prefab.** The only reachable input field is
  the integer row's (`Special/GameSettingInt`), so a field that carries free text sets
  `contentType = Standard` and a character limit on its instance; the visual is the game's, the semantics
  are ours. A run shows whether the field behaves (caret, selection, mobile keyboard).
- **The window now depends on the native surface.** A composition without an adapter has no canvas, hence
  no window — the same trade S2a made for the launcher, and the reason the S4 retirement pass owns the
  remaining IMGUI surfaces. Nothing in this cycle adds a second renderer.
- **The pointer-over-window fact is one frame old.** The rect is polled and reported as a flip, so an
  in-world right-click in the frame the window appears or disappears is judged against the previous frame's
  fact. The window's own modal guard is unaffected (it reads visibility directly).
- **The costs are unmeasured**: one model rebuild per frame while the window is open (the same discipline
  the IMGUI pass had), one reconcile pass over the visible controls, and a control write only where
  something changed. No frame-time measurement was taken.
