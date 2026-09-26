# The Online UI's native surface and its launcher — self-check (2026-09-26)

Ticket: `docs/backlog/review/online-ui-art-and-controls-overhaul.md` (High; **stage S2a**, the first
half of the stage the ticket calls S2). Cycle scope: the game's own UI stops being a picture the mod reads
and becomes a surface the mod stands on. CUO's canvas is parented under the game's canvas, the launcher is
the game's own button-row prefab, the idle fade's alpha is applied to that control, and the player's click
comes back as an intent. The window family itself (shell, tabs, page controls) moves onto the same surface
in S2b — the launcher is the slice that proves the mechanism end to end before six pages ride on it.

No wire, protocol or save change (protocol stays **43**). The four values S1's probe logs still come from
one game run; nothing in this stage depends on them, because the game's own prefab carries the font, the
sprite, the 9-slice and the scale.

## 1. Mechanism inventory — what the launcher's new mechanism rests on

| # | Mechanism | Evidence (quoted / cited) |
|---|---|---|
| 1 | The game's UI is uGUI + TextMeshPro with zero IMGUI | The recon (`%TEMP%/cuo-ui-recon.md`, S1's fact base) and `SettingsMenu.cs`: the settings screen builds every row from `Utils.Create("Special/GameSetting<kind>", content)` |
| 2 | The game's own button-row shape | `SettingsMenu.cs`: `Utils.Create("Special/GameSettingLanguage", this.content)`, then `gameObject2.GetComponent<Button>()` and `gameObject2.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = lang.name` — a `Button` on the prefab root with its caption on child 0, which is exactly the shape the launcher needs |
| 3 | The game's own UI click | `PlayerCamera.cs`: `public static void PlayUISound(string sound, float pitch = 1f)` → `Sound.Play(sound, …)`; `Sound.cs`: `Resources.Load<AudioClip>("Sounds/" + clip)` behind `if (clip != null)` — so a renamed clip is SILENT, not fatal, which is why the call needs no guard |
| 4 | The game's canvases and scale | `PreRunScript.cs` `mainCanvas`, `PlayerCamera.cs` `mainCanvas` and `uiScale` (S1 §1 rows 2-3): a child canvas inherits the game's UI scale and sorting instead of guessing them |
| 5 | uGUI input needs an EventSystem, and the game has one | The game's own menus are uGUI; CUO must not add a second one, so the surface creates its own only when `EventSystem.current` is null — which reports the ENABLED system, so the pinned fact is "the scene lacks an enabled one". The game itself relies on that: `UIUtil.GetEventSystemRaycastResults` dereferences `EventSystem.current` without a guard and runs from the pointer-over-UI path, so a scene where CUO's copy would be the only one is a scene where the game's own pointer handling is already broken |
| 6 | uGUI's enter/exit callbacks fire on pointer MOVEMENT | The reason the hover fact is polled from the rect with `RectTransformUtility.RectangleContainsScreenPoint` and the current pointer: a launcher that appears under a stationary pointer would otherwise never report the hover that keeps it opaque |
| 7 | The launcher's rect and caption are the player's precedent | `review/cuo-launcher-button-obscures-the-view.md` and the pin set that followed it: a right margin of 12, a top margin of 12, 158 by 34, and the `▲`/`▼` marker — S2a keeps both, expressed as a top-right uGUI anchor |
| 8 | The destructive prefab rule still holds | `OnlineUiNativeHostPinTests.NoSourceInTheAdapterOrThePluginLoadsTheSettingsMenuPrefab` scans BOTH trees (comments cut, concatenation included); the live surface loads only `Special/GameSettingLanguage` |
| 9 | The adapter port pattern | `AdapterCapabilityPortShapeTests` (14 ports / 19 members after this stage) and `GameAdapterComposition`: one port per capability, registered once from the one adapter singleton |

## 2. What landed

- **Runtime, pure (5 files, 8-30 lines each)** — `OnlineUiLauncherText` (the caption rule: the translated
  label plus the open/closed marker), `OnlineUiFrame` (what the surface shows for one frame: the caption
  and the opacity the idle rule derived), `OnlineUiIntent` + `OnlineUiIntentKind` (what the surface reports
  back: a launcher click, a hover flip), and the port `IOnlineUiSurface` (`Push` / `TryDequeueIntent`).
- **GameAdapter (2 files)** — `OnlineUiSurfaceHost` (the LIVE counterpart of S1's inactive probe host: a
  CUO canvas under the game's canvas, active, sorting 30000, with a `GraphicRaycaster`; the in-run canvas
  preferred over the pre-run one; rebuilt when the canvas it hung on is gone; an EventSystem created only
  when the scene has none; the click queued BEFORE the game's own `miniClick` plays; the total contract —
  a frame that arrives before the game has a canvas is dropped, never thrown) and `OnlineUiLauncherView`
  (the game's own button-row prefab stretched into the launcher's rect with a top-right anchor, a
  `CanvasGroup` for the whole-surface fade, the caption applied only when it changes, the pointer polled
  against the rect, and the plain-button fallback that keeps the window reachable if the game moves the
  prefab).
- **Plugin** — `OnlineUiHost` drains the surface's intents and pushes one frame per update (the rule asked
  with the runtime clock, the caption from the Runtime rule, rebuilt only when its inputs change);
  `OnlineUiOverlay.ToggleWindow` carries the launcher's opening rule (toggle, and Home → Players while a
  session runs); `OnlineUiWindow`/`OnlineUiWindowState`/`OnlineUiTheme` lost the IMGUI launcher, its style
  and its alpha overload. The theme now owns only the surfaces that have not migrated: the modal window's
  frame, the quick panel, the context menu and the console overlay.
- **Wiring** — `IGameAdapter` composes the 14th port, `GameAdapterComposition` registers it from the one
  adapter singleton, `GameAdapter` implements it explicitly and disposes the surface with its own
  `Dispose`.
- **Tests** — `OnlineUiSurfacePinTests` (23 cases: both halves of the rule-to-pixels path, each pin with a
  mutation of the REAL source as its negative sample), `OnlineUiLauncherTextTests` (4 cases), and the
  rewritten `OnlineUiLauncherFadeTests` (13: the idle rule's matrix unchanged, the theme's blended-frame
  contract re-scoped to the four remaining surfaces with its draw census still a ceiling).

## 3. Family audit — what else touches this mechanism

| Surface | Verdict |
|---|---|
| The IMGUI surfaces that remain | Untouched: the window, the quick panel, the context menu, the console overlay, the nameplates/arrows and the network HUD draw exactly as before; the theme's blended-frame census stays green over the four remaining rows |
| A second launcher growing back in IMGUI | Pinned away: `TheLauncherIsNoLongerDrawnInImgui` scans every plugin `.cs` for the launcher style, the alpha overload and the launcher's GUI rect, and the window shell/state must not carry launcher state at all |
| The modal input guard (`INativeInputBlocker`) | Unchanged and unaffected: the launcher never had a scoped block, and the modal guard still reads the window's visibility, which the click now toggles through the same overlay method the IMGUI button called |
| Other adapter ports | The new port follows the existing pattern exactly: declared on the Runtime aggregate, registered from the adapter singleton, resolved optionally by the consumer (`services.GetService<…>()`), asserted once each by the port census |
| The S1 probe | Untouched and still inactive: its host, its policy and its pins are green (16 cases); the live surface is a separate class with its own (opposite) canvas preference, because a live surface must hang on the canvas that is on screen now |
| Mod UI windows, console, chat | Untouched |
| Wire, protocol, save, gameplay | None: no `NetMsg`, no save field, no protocol change (protocol stays 43) |

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | The caption keeps both markers and composes with the window's state | `OnlineUiLauncherTextTests` 4 cases (closed, open, the markers differ, an empty caption still carries the marker) |
| 2 | The idle rule's whole matrix still holds where it did | `OnlineUiLauncherFadeTests` 13 cases: idle window, monotone ramp, hover restores and restarts, repeated evaluations on one update, a wrapped clock, the floor-to-full range, the constants' intent — plus the theme contract's positive case and its three real-source mutations |
| 3 | The plugin asks the Runtime rule with the runtime clock and pushes its answer | `OnlineUiSurfacePinTests.ThePluginDrivesTheSurfaceWithTheRulesAnswer` (+ the hard-coded-opacity and hand-built-caption mutations) |
| 4 | The click and the hover come back as intents and land on the launcher's own rules | The same class: the intent switch's three cases, `ToggleWindowKeepsTheLaunchersRule` (toggle + Home → Players in a session) and its mutation |
| 5 | The live surface is the game's own control at the launcher's own rect | Same class: `TheLauncherIsTheGamesOwnControlAtTheLaunchersRect` (prefab path, anchor/pivot/anchoredPosition/sizeDelta, the four rect constants) + the shrink and plain-button mutations |
| 6 | The fade's alpha reaches the WHOLE launcher, and hover is a polled pointer fact | Same class: `TheFadesOpacityReachesTheWholeLauncher` and `TheHoverFactIsPolledFromTheLaunchersRect`, each with a mutation of the real source |
| 7 | The surface hangs under the game's canvas, takes input, rebuilds when its canvas is gone, and never throws at the frame callback | Same class: `TheSurfaceHangsUnderTheGamesOwnCanvasAndRebuildsWhenItGoesAway` (one parenting call, the canvas components, no `throw` in the file) with two mutations; the canvas preference has its own pin + mutation |
| 8 | A click plays the game's own UI sound, and the fact is queued before it | Same class: `AClickQueuesTheIntentAndPlaysTheGamesOwnClick` (order asserted, the sound name pinned) + the silent-launcher mutation |
| 9 | CUO never duplicates the game's EventSystem | Same class: `TheSurfaceCreatesAnEventSystemOnlyWhenTheSceneHasNone` (+ the guard's mutation) and the matcher's own samples |
| 10 | The pins discriminate on the REAL files | Nine mutation controls on the frozen sources: canvas order swapped → red; raycaster removed → red; the rebuild guard weakened to "the root exists" → red; the click's sound blanked → red; the launcher's width narrowed → red; the pointer poll replaced by the cached value → red; the pushed opacity hard-coded → red; the theme's overlay blend flag flipped → red; the launcher's opening rule widened to any session role → red. All nine reverted byte-identically (per-file md5 before/after recorded in the log; the whole tree checks clean against `%TEMP%/cuo-s2a-hashes-final.txt`; log `%TEMP%/cuo-s2a-mutations-final.txt`) |
| 11 | The seam stays consistent with its census | `AdapterCapabilityPortShapeTests` 20 cases pass with the port added (14 ports / 19 members, the aggregate composes exactly the pinned ports, each registered exactly once) |
| 12 | Gates, build and structure | build 0 warnings / 0 errors; focused `FullyQualifiedName~OnlineUi\|~AdapterCapabilityPortShape` 192/192; the touching classes 23/4/13/16/21/20; `dotnet format` exit 0; the two new adapter files 212 and 196 lines, the five Runtime files 8-30 lines, one top-level type per file; `GameAdapter.cs` 588 lines, still under the 600-line gate |

## 5. Red, ladder and the numbers

This stage rewrites a mechanism rather than fixing a defect, so the red it can show is the CONTRACT it
moves: the launcher's IMGUI draw pin (`PinsTheIdleFade` and its three negative-sample bodies) is deleted
here because the surface it pinned no longer exists, and the nine mutation controls of §4 row 10 are what
proves the replacements discriminate. The pure halves call the production functions directly, so they
cannot pass without the code.

Ladder on the fixed tree:

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- Focused `FullyQualifiedName~OnlineUi|~AdapterCapabilityPortShape` — 192/192
  (`%TEMP%/cuo-s2a-focus-final.txt`); the touching classes 23/4/13/16/21/20
  (`%TEMP%/cuo-s2a-classes-final.txt`).
- Mutation controls — nine reds, all restored byte-identically (`%TEMP%/cuo-s2a-mutations-final.txt`).
- Normative gates — 287/288, the single failure being this cycle's deliberately reset delivery checklist
  (`%TEMP%/cuo-s2a-gates-final.txt`); the unfiltered run after the checklist is complete is recorded below.
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0.
- Full suite WITH build — tests project 4205 passed, gate project 287 passed with the delivery-checklist
  gate filtered, exit 0 (`%TEMP%/cuo-s2a-full-final.txt`); the same suite unfiltered, after the checklist
  is complete, is recorded at the end of §6.

## 6. Independent adversarial review and dispositions

A fresh-context reviewer read the FROZEN revision — it recomputed HEAD, both tree md5s and all 23 per-file
md5s from `%TEMP%/cuo-s2a-freeze.txt` before and after its work and reported MATCH, so the revision it
reviewed is provably the one these fixes were applied to. It re-derived the numbers (build, focused runs,
the gate run, and the full suite WITH build at 4204 before the last test landed), replayed every pin's
matcher in a scratch harness, and checked every decompiled citation by explicit file path. **No blocker,
no major**; its full report is `%TEMP%/cuo-review-online-ui-native-surface.md`.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| e-1 | minor | §4 row 10's byte-identity sentence cited `%TEMP%/cuo-s2a-hashes-before.txt`, which predates the `dotnet format` pass — following it today prints ten mismatches and reads as tampering, although the restore was genuine | landed: the nine controls were re-run against a fresh baseline on the frozen tree, and the row now cites `%TEMP%/cuo-s2a-mutations-final.txt` and `%TEMP%/cuo-s2a-hashes-final.txt` |
| e-2 / e-3 | minor | the class-count and focused artifacts were pre-format (12 and 190) while §5 called them the fixed tree | landed: both re-run on the final tree (`cuo-s2a-classes-final.txt` 23/4/13/16/21/20, `cuo-s2a-focus-final.txt` 192/192) and cited by name |
| e-4 | minor | §2 and §4 claimed the adapter files were "203 and 196 lines"; the frozen tree is 212 and 196 | landed: measured (`%TEMP%/cuo-s2a-lines-final.txt`) and corrected, `GameAdapter.cs` included at 588 lines |
| e-6 | minor | the rebuild half of the surface pin had no negative of its own (only the raycaster removal was exercised under that test's name) | landed: `ThePinRejectsASurfaceThatNeverRebuilds` (the class's 23rd case) plus a ninth real-source mutation control |
| m-1 | minor (mechanism) | the "no second launcher" pin is three fingerprints over the plugin tree only — a launcher regrown at another rect, or drawn through another call, would slip past | landed as a RECORDED coverage limit (the matcher's doc comment, the ticket's S2a limits, §7), and the ticket's own sentence was narrowed to "the IMGUI launcher's own fingerprints" |
| m-3 | minor | the EventSystem guard tests `EventSystem.current != null`, i.e. "enabled", not "the scene has one" — the test name and docstring overstated it | landed: the test, its helper and §1 row 5 now say "lacks an ENABLED one" and name the game path that depends on it (`UIUtil.GetEventSystemRaycastResults`) |
| m-4 | minor (mechanism) | every deactivation of the game's canvas destroys CUO's canvas and re-instantiates the prefab on the next push; it recovers, and the churn is unmeasured | landed as a recorded limit (the ticket's S2a limits, §7): a game run shows whether it happens in practice |
| m-5 | minor (mechanism) | the launcher's rect is on no input-blocking census, so a middle-click over it still pings — pre-existing launcher behaviour, not a regression | landed as a recorded limit in the ticket: the surface's input-blocking story belongs to the S4 retirement pass |
| nit | nit | §2 said "5 Runtime files, 12-40 lines each"; the real span is 8-30 | landed |

The reviewer also replayed six further negative samples of its own and confirmed the eight recorded
mutation controls discriminate; it flagged nothing it could falsify in the code.

The final unfiltered runs after the checklist was complete: the normative gate project 288/288, exit 0
(`%TEMP%/cuo-s2a-gates-complete.txt`), and the full suite WITH build — tests project 4205, gate project 288
— exit 0 (`%TEMP%/cuo-s2a-full-gate.txt`).

## 7. Limits — what this cycle does not prove

- **No Unity runtime in the verification path.** No test in this tree can instantiate a `GameObject`, so
  the surface is proven by the compile-time contract, the source pins and the nine mutation controls —
  not by a run. Whether the game's row prefab behaves when instantiated ACTIVE (its own components'
  `Awake`/`OnEnable`, any `LayoutElement`/`ContentSizeFitter` that fights the launcher's rect), whether the
  game's canvas is where the surface expects it in each game state, whether the pointer poll and the
  EventSystem make the button clickable, whether the game's `miniClick` plays, and whether the result
  LOOKS like the game: all of that is the user's game run.
- **The launcher's on-screen size now scales with the game's UI** (`uiScale`), because the surface hangs
  under the game's canvas by design — the same rule that makes it read as the game. A run shows the exact
  size; the 12/12/158/34 constants are canvas units now.
- **The fallback path is unexercised.** If the game moves `Special/GameSettingLanguage`, the surface falls
  back to a plain uGUI button and logs a warning naming the prefab; nothing in this tree can produce that
  situation for a test.
- **The reading S1 logs is still pending** (no deployment happened in S1). S2a does not depend on it: the
  look comes from instantiating the game's own prefab, not from the four values.
- **Costs are not measured in-game**: the push is one small struct per update and one rect/pointer test,
  with the caption and the alpha written only when they change — no allocation on the steady path, but no
  frame-time measurement either.
- **The "no second launcher" pin is a fingerprint scan, not a proof of absence**: three fingerprints (the
  theme's launcher style, the alpha overload, the launcher's GUI-space rect) over the plugin tree, so a
  launcher regrown at another rect — or drawn through a different call that computes its rect — would evade
  it. The ticket records the same limit.
- **The rebuild churn is unmeasured**: every deactivation of the game's canvas destroys CUO's canvas and
  re-instantiates the game's prefab on the next push. It recovers correctly; whether the game deactivates
  that canvas in practice, and what the churn costs, is a game-run observation.
- **The launcher's rect is on no input-blocking census**: a middle-click over the launcher still pings
  (pre-existing launcher behaviour — `IsPointerOverUi` and the scoped blocks cover the IMGUI surfaces), and
  the migrated surface's input-blocking story belongs to the S4 retirement pass.
