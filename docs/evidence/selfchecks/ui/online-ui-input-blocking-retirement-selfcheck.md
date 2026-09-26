# The Online UI's input blocking after the migration — self-check (2026-09-26)

Ticket `online-ui-art-and-controls-overhaul`, stage **S4 — the retirement pass**. The stage's premise was
that the input blocking built for an IMGUI-only Online UI needs an item-by-item verdict once the surfaces
it was built for are uGUI. The first verdict was that one item was not redundant but harmful: CUO's own
guard covered CUO's own surface. This page records what the pass rests on, what landed, what was audited
around it, and what only a game run can judge.

## 1. Mechanism inventory — what the pass rests on

| # | Mechanism | Evidence (quoted / cited) |
|---|---|---|
| 1 | CUO builds its own canvas and hangs its controls under it | `OnlineUiSurfaceHost.EnsureSurface`: `new GameObject(RootName)`, `root.transform.SetParent(parent, worldPositionStays: false)`, `var canvas = root.AddComponent<Canvas>();` with `overrideSorting` and `sortingOrder = SortingOrder`, then `OnlineUiLauncherView.TryCreate(root.transform, OnLauncherClicked)` and `OnlineUiWindowView.Create(root.transform, _log, _intents.Enqueue)` — the launcher and the window are children of that canvas root |
| 2 | The guard swept every screen-space canvas with no exclusion | `OnlineMenuInputGuard.CreateRaycastBlockers` / `CreateScopedBlockers`: `foreach (var canvas in Object.FindObjectsOfType<Canvas>())` skipping only `!canvas.gameObject.activeInHierarchy` and `renderMode == RenderMode.WorldSpace`, then `rect.SetParent(canvas.transform, false)`, `blocker.transform.SetAsLastSibling()`, `var image = blocker.AddComponent<Image>(); image.raycastTarget = true; image.color = new Color(0f, 0f, 0f, 0f);` — a transparent full-rect graphic that blocks because it is a raycast target, which is the whole premise the guard is built on |
| 3 | The modal opens AFTER the window does | `OnlineUiHost.Update` reads `var windowVisible = _onlineUi.IsWindowVisible;` and calls `inputBlocker.SetOnlineUiModal(windowVisible \|\| consoleOpen \|\| escCloseFrame)` BEFORE `DrainSurfaceIntents()` — and the intent that toggles the window (`OnlineUiIntentKind.LauncherToggled` → `_onlineUi.ToggleWindow(_session.Role)`) is drained after it, so the first frame the window is up is the first frame the blockers are built |
| 4 | The two world input paths | `LocationPingInputHandler.TryHandle` asks `_overlay.IsPointerOverUi(Input.mousePosition)` (screen space, Y up) before it places a ping; `OnlineUiOverlay.HandleContextMenuInput` decides the in-world right-click in GUI space (`Event.current.mousePosition`) |
| 5 | Where the pointer facts come from | `OnlineUiLauncherView.PollHover`: `RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, camera)` polled every frame, queued as `OnlineUiIntentKind.LauncherHoverEntered` / `LauncherHoverLeft` only when it flips (the enter/exit callbacks fire on movement, so a launcher appearing under a stationary pointer would never report); `OnlineUiWindowView.PollPointer` does the same for the window's rectangle |
| 6 | What the modal flag still drives | `PlayerCameraHandleInputPatch`'s prefix returns false while `PatchBridge.SessionSurface is { IsOnlineUiModalOpen: true }` (the game's own input must not act behind a CUO surface), and `PauseHandlerTogglePausePatch` reads the same flag plus `IsNonModalEscapeSurfaceOpen` |
| 7 | Who still needs the scoped blockers | `OnlineUiOverlay.Draw` feeds the quick panel's and the context menu's GUI-space rectangles; both panels are IMGUI (`OnlineUiQuickPanel`, `OnlineUiPlayerContextMenu`), and `OnlineScopedRaycastFilter.IsRaycastLocationValid` is what keeps the ray OUTSIDE those rectangles: it returns true — the blocker takes the ray — only at a point inside one of them, so the game behind the panel cannot be clicked through it while the rest of the screen stays the game's |
| 8 | The console owns the input while it is open | `OnlineUiOverlay.OpenCommandConsole` closes the window and the quick panel; `CommandConsoleOverlay` draws a bottom-anchored panel (`Mathf.Min(Width, Screen.width - 24f)`) and `DrawPlayerContextMenu` is only reached under `if (!_commandOverlay.IsOpen)`; the quick-panel hotkey is gated on `!_commandOverlay.IsOpen` and the window view is switched off when the frame carries no model |
| 9 | The migration's own records, which the pass had to honour | S2a's limit ("the launcher's rect is on no input-blocking census"), S2b's ("the frame is a raycast target, so a click inside the window can no longer reach the world behind it" and "the window's own rect is polled into the fact that keeps an in-world right-click out of the world menu") |
| 10 | The frame accounting of the two paths | `OnlineUiHost.Update` runs `_locationPingInput.TryHandle()` BEFORE `DrainSurfaceIntents()`, and the surface's pointer flips are queued during `PushSurfaceFrame` at the END of the frame — so the middle-click reads a fact polled two frames earlier (~33 ms), while the right-click path (the IMGUI pass, after `Update`) sees the same frame's drain |
| 11 | What only the game can show | whether a click really reaches the launcher and the window's controls again, whether the surface ever rebuilds in play, and whether the console's mutual exclusion reads right — no test in this tree instantiates a `GameObject` |

## 2. What landed

- **The retirement itself.** `OnlineUiSurfaceMarker` (GameAdapter, `OnlineUi/`) marks the root
  `OnlineUiSurfaceHost` builds, and `OnlineMenuInputGuard` asks `OnlineUiSurfaceMarker.IsInside(...)` in
  all three of its sweeps — the full-rect blocker, the scoped blocker and the `AdaptiveButton` capture —
  so a blocker meant for the game's UI can never land on CUO's own canvas. The modal log line now names
  both counts (`N game button(s) disabled, M screen-space canvas(es) covered; CUO's own surface is never
  blocked by its own guard`) so the retired branch is observable rather than silent.
- **The pointer census.** `OnlineUiPointerCensus` (Runtime, pure) holds the facts — pointer over the
  launcher, pointer over the window, a modal CUO surface open, the IMGUI panels' rectangles — and answers
  the two questions the two world input paths ask: `BlocksWorldPing` (the modal surface owns the SCREEN,
  so no ping becomes a world ping anywhere) and `BlocksWorldMenu` (the modal surface does NOT own the
  world menu, so a right-click outside the window's own rectangle still targets a player — the IMGUI
  window's behaviour, kept). Exactly two facts are new — the launcher's rectangle on both paths and the
  window's own rectangle on the ping path, neither of which existed before this cycle; every other case
  of the two predicates is the pre-change case set.
- **The launcher enters the census.** The fact was already crossing the seam for the idle fade, so the
  same hover flip now also feeds `OnlineUiOverlay.SetPointerOverLauncher` — one polled fact, two readers —
  and the plugin keeps no second copy of the window's fact (`_pointerOverWindow` is gone with it).
- **The facts are retracted when the view that reports them dies.** The views report a FLIP only and a
  rebuilt one starts un-hovered, so `OnlineUiSurfaceHost.DestroySurface` (the rebuild path when the
  canvas it hung on is gone, and the dispose path) queues a hover-left for the launcher and for the
  window before it drops them: a census left holding "the pointer is over CUO's UI" is global, and would
  block every world ping and every in-world right-click until the pointer happened to leave the launcher
  again. The review found this hole (M1); the pin holds the retraction now.
- **One rectangle source.** `OnlineUiOverlay.CollectOverlayRects` is what both the adapter's scoped
  blockers and the census read, so a click cannot be blocked on one path and leak on the other.
- **The console's mutual exclusion.** `ToggleWindow` refuses a launcher click while the command console
  overlay is open and logs it at INFORMATION level — the plugin's own default log configuration is
  Information, so a debug line would not have made the refusal observable (the review's M4). With the
  blocker no longer covering CUO's canvas that click would otherwise open the window behind the console;
  what used to be an accident of the blocker is now a rule with a line of its own, and the pin holds the
  order (refusal before the toggle).
- **Deleted in the same round.** `OnlineUiQuickPanel.Contains`, which had no caller left once the census
  reads the panel's rectangle through the one rect list.
- **The pins moved with it.** `OnlineUiInputBlockingPinTests` is new: 7 pins × 11 real-source mutation rows
  through one `Matcher` registry, covering the ownership rule in all three sweeps, the marker on the
  surface root, the launcher fact reaching the census, both world paths asking it, the single rect source,
  the retraction of a fact whose view died, and the console's refusal (its text, its level and its order).
  `OnlineUiSurfacePinTests`' two launcher-hover clauses were re-anchored onto the same contract with the
  census's reader added (the pin's own subject — the plugin drives the surface with the Runtime rule — did
  not change).

## 3. Family audit — what else touches this mechanism

| Surface | Verdict |
|---|---|
| The full-screen modal blockers | Kept, narrowed to the GAME's canvases. The game's UI behind a CUO surface must still not take the click |
| The `AdaptiveButton` capture | Kept, narrowed by the same ownership rule. Whether a CUO control could ever BE an `AdaptiveButton` is not decidable from this tree — no `OnlineUi` source names the class, the game's prefabs live outside the repository, and `reversing/` carries only the class itself — so this is the ownership rule applied to the third sweep rather than a fix for an observed object (recorded in §7) |
| The scoped blockers (`SetOnlineUiScopedBlocks`, `OnlineUiBlockRect`, `OnlineScopedRaycastFilter`) | Kept: the quick panel and the context menu are still IMGUI and invisible to uGUI. They are also still the reason `OnlineUiBlockRectTests` stays |
| `SetOnlineUiEscapeSurfaceVisible` | Kept: the quick panel's non-modal ESC still suppresses only the native pause toggle |
| The middle-click ping | Unchanged shape, wider facts: the modal surfaces and both IMGUI panels as before, plus the launcher and the window's own rectangle |
| The world right-click | Unchanged shape, wider facts: the two IMGUI panels and the window's rectangle as before, plus the launcher; a right-click outside the window still opens the world menu while the window is up |
| ESC closing the window / console / quick panel | Untouched: `CuoEscCloseSuppression` and the IMGUI pass's `esc.Use()` are exactly as they were |
| The launcher while the console is open | New explicit rule (refused + logged); before the pass the click was swallowed by the blocker, so no reachable behaviour regressed |
| The port census | Unchanged: no member was added to `INativeInputBlocker` or any other port (14 ports / 19 members), so `AdapterCapabilityPortShapeTests` and `OnlineMenuInputGuardContractTests` hold as they were |
| Wire, protocol, save, gameplay, localisation | None: no message, no save field, no catalogue key |
| The remaining IMGUI faces | Decided, not silently kept: the console overlay stays IMGUI (recorded in the ticket and decision 230), the quick panel and the context menu are S5, the world-space overlays are S6 |

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | CUO's own guard never puts a blocker on CUO's own canvas | `OnlineUiInputBlockingPinTests.TheGuardNeverBlocksCuosOwnSurface` (the ownership clause in `IsBlockable`, both blocker sweeps asking it, the `AdaptiveButton` sweep asking the same rule) + 3 mutation rows |
| 2 | The live surface marks the root it builds, before anything is built under it | Same class: `TheSurfaceMarksItsOwnCanvas` (the marker line, and its position ahead of the launcher's and the window's creation) + its mutation row |
| 3 | The launcher's pointer fact reaches the census in both directions | Same class: `TheLauncherEntersThePointerCensus` + its mutation row |
| 4 | Both world input paths ask the census and the plugin keeps no second window fact | Same class: `BothWorldInputPathsAskTheCensus` (the GUI conversion, the two census calls, and `_pointerOverWindow` gone) + 2 mutation rows |
| 5 | The adapter's scoped blockers and the census read ONE rectangle list | Same class: `OneRectSourceFeedsTheBlockerAndTheCensus` + its mutation row |
| 6 | The console keeps a launcher click from opening a second surface | Same class: `TheConsoleKeepsTheLauncherFromOpeningASecondSurface` (the refusal, its log line, and the toggle that follows it) + its mutation row |
| 7 | The rule itself: a modal surface blocks every ping but not the world menu | `OnlineUiPointerCensusTests.AModalSurfaceBlocksEveryWorldPingButNotTheWorldMenu` |
| 8 | The rule itself: the launcher and the window block both world inputs | Same class: `TheLauncherBlocksBothWorldInputs`, `TheWindowBlocksBothWorldInputs` |
| 9 | The rule itself: the IMGUI panels block only their own rectangles, and every rectangle counts | Same class: `TheOverlayPanelsBlockOnlyTheirOwnRectangles`, `EveryOverlayRectangleIsConsidered`, `TheRectangleEdgesBelongToTheSurface`, `AnEmptyRectangleListBlocksNothing`, `ThePointIsGuiSpaceWithTheOriginAtTheTopLeft` |
| 10 | The rule does not go sticky: leaving a surface unblocks it | Same class: `LeavingASurfaceUnblocksTheWorldInputs` |
| 11 | An idle screen blocks nothing at all | Same class: `AnIdleScreenBlocksNothing` |
| 12 | The retired branch is observable | `OnlineMenuInputGuard.BeginModal` logs the disabled-button count, the covered-canvas count and the ownership rule at information level; `SetScopedBlocks` logs the rectangle and canvas counts at debug level; the launcher's refusal while the console owns the input is logged at INFORMATION level, which the plugin's own default (`Logging.MinimumLevel=Information`) shows |
| 13 | The migration's other pins still hold | `OnlineUiSurfacePinTests` (24 facts, its 13 in-file mutation samples intact; the two launcher-hover clauses re-anchored), `OnlineUiWindowSurfacePinTests` (14 pins + its own 16 mutation rows), `OnlineUiLauncherFadeTests` (the IMGUI theme census, untouched), `OnlineUiBlockRectTests`, `OnlineUiConsolePageRemovalPinTests`, `AdapterCapabilityPortShapeTests` and `OnlineMenuInputGuardContractTests` — all green in the focused run |
| 14 | The dead code the change orphaned is deleted, not parked | `OnlineUiQuickPanel.Contains` removed (no caller in `src/` or `tests/`); `OnlineUiPlayerContextMenu.Contains` kept, because the left-click close still asks it |
| 15 | A pointer fact is retracted when the view that reported it dies | `OnlineUiInputBlockingPinTests.ThePointerFactsDieWithTheSurface` (both hover-left intents in `DestroySurface`) + its mutation row |
| 16 | The console's refusal runs BEFORE the toggle it guards | Same class: `TheConsoleKeepsTheLauncherFromOpeningASecondSurface` now pins the two positions as well as the three texts + a second mutation row that swaps them |

## 5. Red, the ladder and the numbers

- **The red.** The pin set was written and frozen BEFORE `src/` was touched, and run against HEAD's
  sources: **15 failed / 0 passed / 15** (`%TEMP%/cuo-s4-red3.txt`), with all six pins of that moment
  failing on their own assertions (zero exceptions in the run) and every mutation row failing because its
  anchor belongs to the delivered source and is absent at HEAD. The independent review could not re-run it
  (that needs a second checkout at HEAD) and verified it ANALYTICALLY instead: every shape those six pins
  required is absent at HEAD (`OnlineUiSurfaceMarker`, `IsBlockable`, `_pointerCensus`,
  `CollectOverlayRects`, `BlocksWorldMenu`, and the console clause in `ToggleWindow`), and all nine
  mutation anchors are new-code text, so each row fails on its own `source.Contains(anchor)` assertion.
  The red run was possible as described: the pin class depends only on `System`, `Xunit` and
  `System.IO`.
- **The ladder** (re-taken after the review's findings landed). build 0 warnings / 0 errors
  (`%TEMP%/cuo-s4-build2.txt`); the focused filter, spelled out the way vstest accepts it,
  `FullyQualifiedName~OnlineUi|FullyQualifiedName~AdapterCapabilityPortShape|FullyQualifiedName~PlayerColor|FullyQualifiedName~ConfigurationProfile`
  313/313 (`cuo-s4-focus5.txt`); `dotnet format` exit 0 (`cuo-s4-format2.txt`); the full suite WITH build
  4314 + 288, exit 0 (`cuo-s4-full2.txt`); the normative gates 288/288 (`cuo-s4-gates2.txt`).
- **The new cases.** `OnlineUiInputBlockingPinTests` 18 (7 pins + 11 mutation rows) and
  `OnlineUiPointerCensusTests` 10 facts — 28 new cases, which is the difference between the previous
  cycle's recorded 4286 and this run's 4314. (4286 is a prior-cycle figure; it is reproduced here by
  arithmetic, not by a run of this tree.)
- **The size review.** Largest touched sources: `OnlineUiOverlay.cs` 552, `OnlineUiHost.cs` 453,
  `OnlineMenuInputGuard.cs` 286, `OnlineUiSurfaceHost.cs` 243 — all under the 600-line aggregate gate, and
  the new files are small (`OnlineUiPointerCensus.cs` 76, `OnlineUiSurfaceMarker.cs` 22,
  `OnlineUiInputBlockingPinTests.cs` 348, `OnlineUiPointerCensusTests.cs` 159). `git diff --stat` for the
  change set: 9 files changed, 234 insertions, 68 deletions, plus 5 new files.

## 6. Independent adversarial review and dispositions

The review ran in a fresh context against the FROZEN working tree before the commit, with read-only tools.
It reproduced every number it could (build, the full suite's 4311 + 288 before the fixes below, the gates,
the 25 new cases, all eight line counts), verified the red analytically, confirmed the tree was untouched
(`git status --porcelain` and `git diff --stat HEAD` identical before and after) and could not run
`dotnet format` by rule. Its verdict: no blocker, no major; the eleven claims held up as substantively
correct. Findings, and what happened to each:

| # | Severity | Finding | Disposition |
|---|---|---|---|
| M1 | minor | A stale pointer fact outlived its view: `DestroySurface` dropped the launcher and the window with no hover-left, the views report only a FLIP and a rebuilt one starts un-hovered, so a surface rebuilt while the pointer sat on the launcher left the census holding `true` for good — and the fact is global, so every world ping and every in-world right-click stayed blocked. The ticket's limit covered only click-time staleness | landed (behaviour fix, not a recorded limit): `DestroySurface` now queues `LauncherHoverLeft` and `WindowHoverLeft` before it drops the views, pin `ThePointerFactsDieWithTheSurface` + its mutation row, §2/§3/§7 and the ticket's S4 limits say it |
| M2 | minor | §4 row 13 attributed "16 mutation rows" to `OnlineUiSurfacePinTests` — the figure belongs to `OnlineUiWindowSurfacePinTests`; `OnlineUiSurfacePinTests` has 24 facts and 13 in-file mutation samples | landed: the row now names each class with its own census |
| M3 | minor | §5's focused filter was written in a shorthand vstest rejects: it ran **0 tests, exit 0, no summary** — the dangerous kind of green. The expanded spelling gives the real figure | landed: §5 spells the filter out the way it was actually run, 313/313 |
| M4 | minor | The console refusal logged at Debug, which the plugin's own default (`Logging.MinimumLevel=Information`) never shows — a swallowed player click is the low-frequency/exceptional case the observability rule sends to a visible level | landed: `Plugin.Logger.LogInfo`, the pin requires the information-level call, and its doc says why |
| N1 | nit | "No CUO control is an `AdaptiveButton` today" is not decidable from this tree (the prefabs live outside the repository) | landed: §3 states what is and is not observable, and §7 carries it as a limit |
| N2 | nit | The S2a limit bullet about the launcher's missing census still read as current | landed: the bullet now carries "closed by S4" |
| N3 | nit | `CollectOverlayRects`' doc claimed an undrawn panel is not included; the context menu keeps its last rectangle while the console is open | landed: the comment states what the method really does — the list is the panels' CURRENT state, and the context menu's stale rectangle is not a reachable input path (the menu is not drawn and its click path is gated) |
| N4 | nit | §1 row 7 inverted `IsRaycastLocationValid`'s sense | landed: it now says the filter returns true INSIDE a rectangle (the blocker takes the ray) and false outside |
| N5 | nit | The console pin pinned three substrings but not their order — a refusal moved after the toggle would survive | landed: the matcher compares the two positions and a second mutation row swaps them |
| N6 | nit | "preserves the pre-change behaviour for every pre-existing case" over-claimed: the launcher and the window-on-the-ping-path facts are deliberate changes | landed: §2 says exactly which two facts are new and that everything else is the pre-change case set |
| N7 | nit | The ownership rule is opt-in per root: a future ACTIVE CUO canvas without the marker would be read as the game's | landed as a RECORDED limit (§7); today the only other CUO canvas — the S1 probe host — is inactive from its first statement, so `FindObjectsOfType<Canvas>` never sees it |
| R1 | (answer, not a finding) | The launcher's hover flip does NOT arrive before the middle-click read: `Update` drains the ping before the flips, and the flips are queued at the end of the frame, so the ping reads a fact polled two frames earlier (~33 ms) — the entering direction was undisclosed while the ticket claimed "one frame old" | landed: §1 row 10 states the frame accounting, and the ticket's S4 limit now names both directions |

## 7. Limits — what this cycle does not prove

- **That the click lands is the user's run.** No test in this tree instantiates a `GameObject`, so the pins
  are real-source text pins with mutation rows; whether the game's own controls answer again, at the game's
  UI scale, is a game observation. The same goes for whether the surface's rebuild path (M1's trigger) is
  ever taken in play, and whether the console's mutual exclusion reads right.
- **The pointer facts are up to two frames old on the ping path.** The middle-click is handled before the
  frame's intents are drained and the surface polls at the end of the frame, so a middle-click within about
  two frames of arriving on the launcher still pings (the leaving direction is one frame); the right-click
  path, which runs in the IMGUI pass after `Update`, sees the same frame's facts.
- **The ownership rule is opt-in per root.** The surface marks its own root; an ACTIVE CUO canvas built
  elsewhere without the marker would be treated as the game's and blocked. Nothing is missed today — the
  only other CUO canvas, the S1 probe's, is `root.SetActive(false)` from its first statement.
- **Whether a CUO control could be an `AdaptiveButton` is not decidable from this tree.** The third sweep's
  ownership clause is the rule applied uniformly, not a fix for an object anyone has observed.
- **The guard pin's sweep census counts the sweeps that exist.** A THIRD blocker sweep added later that
  never asks the ownership predicate would not raise the count (declared in the pin's own coverage note).
- **The context menu keeps its last rectangle while the console is open** (`Bounds` is only written while
  the panel is drawn), so the census can block a right-click there; the menu is not drawn in that state and
  its click path is gated on the console being closed, so it is not a reachable input path.
- **The ticket's goal is not reached yet.** The quick panel and the player context menu are still IMGUI
  panels of the flat theme and the world-space overlays still draw with the IMGUI skin's font: the
  player-facing art ask stays open until S5 and S6 land, which is why the ticket stays in progress.
- **The S1 chrome reading is still pending**, and nothing in this pass used it — the retirement depends on
  no runtime reading at all.

