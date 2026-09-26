# The Online UI's world-space overlays — self-check (2026-09-26)

Ticket `online-ui-art-and-controls-overhaul`, stage **S6 — the world-space overlays**. The stage's premise
was that the four things CUO draws over the world — the network readout, the remote players' nameplates, the
off-screen arrows that replace them at the screen's edge, and the transient location pings — were the last
player-facing IMGUI face, and that what was left to win there was the LOOK: they take no input, so no control
and no pointer contract is involved. This page records what the stage rests on, what landed, what was audited
around it, what the independent review found, and what only a game run can judge.

## 1. Mechanism inventory — what the stage rests on

| # | Mechanism | Evidence (quoted / cited) |
|---|---|---|
| 1 | The seam already exists: one frame per update carries every CUO surface | `IOnlineUiSurface.Push(OnlineUiFrame frame)`; `OnlineUiFrame` gained the sixth member `OnlineUiWorldOverlay World`, and `GameAdapter` forwards the whole frame (`void IOnlineUiSurface.Push(OnlineUiFrame frame) => _onlineUiSurface.Push(frame);`) — no port was added |
| 2 | The surface is where a label can live | `OnlineUiSurfaceHost.EnsureSurface` builds CUO's canvas under the game's, stretches its rect over the game's (`canvasRect.anchorMin = Vector2.zero`, `canvasRect.anchorMax = Vector2.one`), and `Push` applies the frame to each view; `OnlineUiWorldOverlayView.Create(root.transform)` is built there with the launcher, the window and the two panels |
| 3 | A nested canvas is not resized by Unity, so the layer states its own rect | `OnlineUiWorldOverlayView.Create`: `new GameObject(LayerName, typeof(RectTransform))`, `rect.anchorMin = Vector2.zero`, `rect.anchorMax = Vector2.one`, `rect.offsetMin = Vector2.zero`, `rect.offsetMax = Vector2.zero` — the layer's `rect` IS the screen in canvas units |
| 4 | The game's own art is read in one place | `OnlineUiControlFactory.ReadRowTemplate(root.transform, out var typography, out _, out _, out _)` — the same reader the window and the panels use; the view's `CreateLabel` sets `text.fontSize = typography.SizeOf(OnlineUiTextStyle.Muted)` and `text.font = typography.Font` |
| 5 | The projection happens ONCE, into the canvas | `OnlineUiWorldOverlayView.TryProject`: `var screen = camera.WorldToScreenPoint(new Vector3(marker.X, marker.Y, 0f));` then `RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, new Vector2(screen.x, screen.y), cameraOfCanvas, out var local)`, then the Y flip into the convention the Runtime rules are written in: `point = new Vector2(local.x - bounds.xMin, bounds.yMax - local.y);` |
| 6 | The geometry is the Runtime's, asked in the canvas's UNITS | `var placement = OffScreenArrowGeometry.Place(point.x, point.y, bounds.width, bounds.height, ScreenEdgeMargin);`, `NameplateLayout.AboveHead(placement.X, placement.Y)` for the on-screen box, `OffScreenArrowText.Glyph(placement.Direction)` for the edge mark — with `ScreenEdgeMargin = 52f` in canvas units, and `Screen.width`/`Screen.height` absent from the file |
| 7 | The plugin builds the model, and every input of it is a runtime fact | `OnlineUiOverlay.BuildWorldOverlay(ctx)` returns `new OnlineUiWorldOverlay(BuildNetworkHud(ctx), _worldMarkers)`; the nameplates come from `ctx.Entities.RemotePlayers` (skipping `remote.IsLocal` and `!ctx.Session.IsRemoteInWorld(remote.SteamId)`), prefer `ctx.AnchorQuery.TryGetRemoteHeadPosition(remote.SteamId, out var headX, out var headY)` over `remote.Position`, and carry `ctx.DisplayName(...)`, `ctx.F("hud.distance", Mathf.RoundToInt(distance))` and `ToRgba(ctx.PlayerColor(...))`; the pings come from `ctx.LocationPings.ActivePings`, drop the expired ones (`if (remaining <= 0)`), and fold the fade in: `color.A * Mathf.Clamp01((float)remaining / PingFadeMs)` |
| 8 | The two states the old code did not draw in survive the move | `OnlineUiHost.PushSurfaceFrame`: `var world = _gateState is { IsWaitingForReady: true } \|\| _onlineUi.IsCommandConsoleOpen ? OnlineUiWorldOverlay.None : _onlineUi.BuildWorldOverlay(ctx);` — the start gate and the open command console |
| 9 | The overlay takes no input | the layer has no graphic (`typeof(RectTransform)` alone), every label is `text.raycastTarget = false;`, and the view mentions neither `OnlineUiIntent` nor `Graphic`. The pointer census and the two world input paths are untouched: `OnlineUiPointerCensus.BlocksWorldPing()`/`BlocksWorldMenu()`, `LocationPingInputHandler.TryHandle` and `OnlineUiPlayerContextMenu.HandleInput` are exactly as S4/S5 left them |
| 10 | The draw order is deliberate | `root.transform.SetAsFirstSibling();` — the layer is the canvas's first child, so the markers draw behind the launcher, the window and the two panels. IMGUI rendered after every canvas, so the same markers used to draw over them |
| 11 | What retires, and who its consumers were | `LocationPingOverlay` (the file, whose only caller was `OnlineUiOverlay.Draw`), `OnlineUiOverlay.DrawNetworkHud` / `DrawNameplatesAndArrows` / `DrawNameplate` / `DrawOffScreenArrow` / `ToColor`, its `ScreenEdgeMargin` / `NameplateFontSize` / `OffScreenArrowFontSize` / `OffScreenNameFontSize` constants, and `OnlineUiTheme.Status(Color)` (whose only caller was `DrawNetworkHud`). `OnlineUiTheme.MutedLabel()` STAYS: the command console's four call sites still use it |
| 12 | What only the game can show | whether the names are legible at the player's UI scale, whether the edge arrows and the top-left readout sit where the player expects, whether the game's font asset carries `▲ ▼ ◄ ▶ ! ●`, whether the canvas rect really is the screen, and whether the result reads as this game |

## 2. What landed

- **One view, one layer.** `OnlineUiWorldOverlayView` (GameAdapter, `OnlineUi/`) owns the layer and a pooled
  pair of labels per marker: the mark drawn at the point (an arrow, or a ping's own glyph) and the text
  beside it. A marker that changes kind between frames is re-written, not rebuilt; a marker the model drops
  is hidden rather than destroyed; a frame with no camera, or a point the canvas cannot answer for, hides
  what it cannot place instead of drawing it at the origin.
- **The models are the Runtime's.** `OnlineUiWorldMarker` (kind, world position, the two label forms, the
  glyph and the colour), `OnlineUiWorldMarkerKind`, `OnlineUiNetworkHud` (both readout lines with their
  colours) and `OnlineUiWorldOverlay` (readout + the marker list, with `None` as the empty state). The two
  marker factories are where the IMGUI overlay's label composition moved to (`"Ana  42 m"` off screen,
  `"Bo ●"` off screen), so the spacing is a tested rule rather than a concatenation in a view.
- **One copy of the arrow rule.** `OffScreenArrowText.Glyph(direction)` (Runtime, pure) replaced the
  identical four-way switch that lived in `OnlineUiOverlay.DrawOffScreenArrow` and again in
  `LocationPingOverlay.DrawOffScreen`.
- **The units are stated, not assumed.** The projection is converted into the canvas once and the whole
  placement is asked there; the plugin no longer touches `Screen.width`/`Screen.height`, and the two Runtime
  rules' doc comments now say the numbers are in the caller's unit.
- **The labels' fit and their marks are stated policy, not TMP defaults.** One line, auto-sizing off, and
  overflow rather than a clip or an ellipsis (`enableWordWrapping`/`enableAutoSizing`/`overflowMode` are
  written in the view), and every mark is checked against the game's own font asset
  (`TMP_FontAsset.HasCharacter(..., searchFallbacks: true)`) with an ASCII stand-in (`^ v < >`, `*`) and one
  warning per mark when the asset cannot draw it.
- **The one silent branch is observable.** A marker the canvas cannot map is skipped — not the end of the
  pass, so one bad world point no longer hides the markers after it — and reported once at Warning; a frame
  with no main camera (the ordinary pre-run state) is reported once at Debug.
- **The suppression rules survive.** The frame pushes `OnlineUiWorldOverlay.None` while the start gate holds
  the player and while the command console is open — the two states the IMGUI pass returned before drawing
  the overlays in. The console half was missing from the first cut and the pin's second mutation row is what
  holds it now.
- **Deleted in the same round.** The IMGUI overlay, its font-size constants, and the theme's `Status`
  style. The theme's own draw census does not move: `OnlineUiLauncherFadeTests` still pins a ceiling of
  exactly one blended `GUI.DrawTexture`, and S6 added none.

## 3. Whole-family audit — what else the same pattern touched

| # | Sibling | Verdict |
|---|---|---|
| 1 | `OnlineUiLauncherFadeTests` | Untouched and green. The theme's census is unchanged because the overlays never drew through `DrawOverlayBackground`; of the two styles they used, `MutedLabel()` still serves the console and `Status(Color)` lost its last caller and was deleted |
| 2 | `OnlineUiSurfacePinTests` | Its frame-push anchor moved with the frame (`… surfaces.ContextMenu, world));`) and now includes the suppression expression; its three existing mutations (hard-coded opacity, hand-built caption, no window) still fail the pin as before |
| 3 | `OnlineUiPanelSurfacePinTests`, `OnlineUiWindowSurfacePinTests`, `OnlineUiColorPickerPinTests`, `OnlineUiConsolePageRemovalPinTests`, `OnlineUiInputBlockingPinTests`, `OnlineUiPointerCensusTests` | Untouched and green: the window, the panels, the colour picker, the console page removal, the input-blocking contract and the census are all unaffected by this stage |
| 4 | `NameplateLayout` / `OffScreenArrowGeometry` | Kept as the pure rules they were (no behaviour change, no test change); only their doc comments changed, to say that the numbers are in the caller's unit and that the surface asks in the canvas's |
| 5 | `AdapterCapabilityPortShapeTests` / the port census | Untouched: no port was added and no member changed (14 ports / 18 members) — the frame carries the new model |
| 6 | The wire, the session and the saves | Untouched: no message, no protocol number (43) and no stored shape changed. This stage is presentation only, and it is the reason the ticket's `## Non-goals` still holds |
| 7 | The three IMGUI surfaces left | `CommandConsoleOverlay` (+ its input renderer) — decided in S4, a developer surface with a text input; `ModUiDrawing`/`ModUiRenderer` — mod-owned windows, not CUO's UI; `StartGateOverlay` — the session-readiness gate, outside this ticket's stages. Each now has a reason and none of them is a world-space marker |
| 8 | The evidence pages that describe the removed renderer | `ui/location-ping-selfcheck.md`, `ui/name-tag-ui-polish-selfcheck.md` and `players/online-ui-player-awareness-selfcheck.md` keep their feature claims (the ping control, the marker rules, the head anchor and the world-presence filter all still hold) but their IMGUI rendering rows are superseded — the MANIFEST rows say so now |
| 9 | The gesture paths the stage must not change | The middle-click ping (`LocationPingInputHandler`), the in-world right-click (`OnlineUiPlayerContextMenu.HandleInput`), the quick panel's ESC, `SetOnlineUiModal` / `SetOnlineUiEscapeSurfaceVisible` and the pointer census: all untouched, and pinned as untouched by the S4/S5 pin classes |
| 10 | The markers' own geometry | The IMGUI rects are kept as the same numbers in canvas units: the nameplate's box above the head (180×24, 8 above), the arrow's 32-unit box, the ping's 40-unit mark and its name 22 below it, the edge margin 52, and the readout's own 20-unit rows at `(8, 8)` from the canvas's top-left corner — so this is a change of renderer and of unit, not of layout intent |

## 4. Verification — the ladder

| Step | Result | Artifact |
|---|---|---|
| Red before the change | **36 failed / 27 passed / 63 total**, on the final test set: the two Runtime-referencing classes set aside, `git stash push --include-untracked -- src`, then the two pin classes run against HEAD's source — 35 of the failures in `OnlineUiWorldOverlayPinTests` and 1 in `OnlineUiSurfacePinTests` (the re-anchored frame push). Breakdown, counted from the log: 21 fail with a file-not-found stack (the rows that read `OnlineUiWorldOverlayView.cs`, which HEAD does not have) and 15 with the pin's own assertion. The first cut of this page stated 25+1 from a different scoping; the review could not derive it from the sentence, so the recipe is now stated exactly and the number is the one the recipe produces | `%TEMP%/cuo-s6-red2.txt` |
| Build | 0 warnings / 0 errors | `%TEMP%/cuo-s6-build1.txt` |
| Focused run (`OnlineUi`) | 369 / 369, exit 0 | `%TEMP%/cuo-s6-focus6.txt` |
| Normative gates | 288 / 288, exit 0 (two earlier red runs are recorded rather than hidden: 287/288 while the fully-qualified-name gate held `System.StringComparison.Ordinal` in a new test file, and 286/288 while this stage's own documentation was still being written — the review's BL1) | `%TEMP%/cuo-s6-gates3.txt` |
| Format | `dotnet format` exit 0 | `%TEMP%/cuo-s6-format2.txt` |
| Line endings | every touched file re-checked at byte level after `dotnet format` and the last edits: the new and modified files are CRLF with 0 bare LF and 0 `\r\r`. Two touched markdown files (`review/middle-click-location-marker.md`, `ui/location-ping-selfcheck.md`) were LF-only BEFORE this change and stayed as they were — 179 of the tree's `docs/**.md` files are LF-only, so that is the repository's own mixture and not this stage's | shell check recorded in this session |
| Mutation controls | 26 real-source mutation rows in `OnlineUiWorldOverlayPinTests`, each re-run on the frozen tree (every anchor present, every matcher false on its own broken source) | the pin class's own run |

### 4.1 Full suite

`dotnet test CasualtiesUnknownOnline.slnx` (WITH build, the final ladder step, after every review finding was
disposed of): **4401 passed / 0 failed** in `CasualtiesUnknownOnline.Tests` and **288 passed / 0 failed** in
the normative gates, exit 0 — `%TEMP%/cuo-s6-full2.txt`. The main suite grew by exactly this stage's 53 new
cases (13 facts + 26 mutation-theory rows in `OnlineUiWorldOverlayPinTests`, 3 facts + a four-case theory in
`OffScreenArrowTextTests`, 7 facts in `OnlineUiWorldMarkerTests`) over S5's 4348.

## 5. Limits — what this evidence cannot say

- **A unit test cannot instantiate a `GameObject`.** Nothing here proves the labels render, that the game's
  font asset has the glyphs, that the game's `TextMeshProUGUI` behaves on a canvas CUO owns, or that a
  nameplate sits over a head. Those are the user's run.
- **The unit change is a real change of appearance.** The IMGUI overlay worked in screen pixels with fixed
  pixel font sizes (a nameplate at 15, an edge label at 13); the labels work in canvas units and take the
  game's own type ladder (the body size, and two below it for an edge label or the readout — 14 and 12 on the
  reader's fallback path), with no auto-sizing. The rule is correct either way, but whether the apparent size
  is right at the player's `PlayerCamera.uiScale` is a run observation — and S1's reading of that scale is
  still pending.
- **The readout's colours are still CUO's.** `Muted` and `Positive` come from `OnlineUiTheme`; the game's own
  chrome colours are what S1's probe would bring, and they are not used here.
- **One deliberate order change.** The markers used to draw over every uGUI surface (IMGUI renders last) and
  now draw behind CUO's own panels. A nameplate behind an open window is the intent; a player who wanted the
  opposite would see it as a regression, which is why it is recorded here and in the ticket rather than left
  implicit.
- **A point behind the camera is still projected mirrored** and pinned to an edge with a direction the player
  may not expect — the removed overlay's own behaviour. The new path adds one step the old one did not have:
  the point must be mappable into the canvas, and a point the canvas refuses hides that ONE marker for the
  frame (with one Warning) instead of pinning it. Whether a mirrored point is refused depends on the canvas's
  render mode and on Unity's own answer, which this tree cannot produce.
- **The marker pool's reuse protocol is held only by text pins.** `Hide`/`HideGlyph`/`Glyph`/`Label` and the
  two placement calls are read as source shape; nothing here can build a `TextMeshProUGUI`, so a stale glyph
  or a stale size on a reused item would be a game-run observation.
- **The surface rebuilds cost one more view.** A scene change destroys CUO's canvas and the next frame builds
  the launcher, the window, the panels AND the world overlay again, which includes one
  `ReadRowTemplate` probe per view. The churn is unmeasured (S2a recorded the same for the surface itself).
- **The overlay needs the adapter.** Without a surface there is no canvas, so there are no nameplates, no
  arrows, no readout and no pings — the trade S2a recorded for the launcher, now extended to the last
  player-facing face.
- **The start gate and the command console are still IMGUI**, so the Online UI still has two surfaces whose
  typography is the skin's. Neither is a world-space marker and neither is in this ticket's stages.

## 6. The independent review, and how each finding was disposed of

The review ran in a fresh context against the frozen tree (report `%TEMP%/cuo-review-s6.md`, 662 lines:
26 findings — 1 blocker / 4 major / 11 minor / 10 nit). It re-derived every claim from the working tree,
re-ran the ladder, replicated the pin class's matchers in Python to test all 18 mutation rows mechanically,
walked the pooled marker state machine and the surface's six apply/destroy paths, and checked the deletion's
reference integrity in both directions. Verdict: **no code defect found in the S6 production path** — the two
things it judged able to break the feature visibly were the label fit (M2) and the glyph coverage (M4), and
both are addressed below. What it did catch in the code was one mutation row that could not compile (M1) and
one behaviour the first cut introduced silently (N2: a bad nameplate could blind every ping for a frame).

| # | Finding | Disposition |
|---|---|---|
| BL1 | BLOCKER — the gate project was red (288 total, 286 passed): the new self-check had no MANIFEST row, and the ticket's `Status:` disagreed with the folder it still lived in | **Fixed**: the MANIFEST row landed and the ticket moved to `review/` with its `Status:`, its index row and every path that named it — the final gate run is green (§4.1) |
| M1 | MAJOR — mutation row 8's replacement was not valid C#, so the only row anchoring the single canvas conversion could never compile | **Fixed**: the replacement keeps the `if` (`var local = …; var answered = true; if (!answered)`) and removes the call |
| M2 | MAJOR — the migrated labels are smaller than the ones they replace (15 → the game's body size, 13 → two below it) and no auto-sizing/overflow policy was set | **Fixed and recorded**: the fit policy is written in the view (one line, auto-sizing off, overflow rather than a clip) and pinned with a mutation row; the size change is a named limit in the ticket and in §5 |
| M3 | MAJOR — two claims were under-pinned: "cannot swallow a click" was absence-of-text only, and "the two world input paths are untouched" was asserted nowhere | **Fixed**: the no-input matcher now also rejects `Selectable`, `Button`, `Toggle`, `EventTrigger`, `IPointer`, `Input.mousePosition` and `OnlineUiPointerCensus`, with two new rows (a `Button` on the layer, a pointer poll in the pass); `TheWorldInputPathsStillAskTheCensus` pins both call sites with its own row |
| M4 | MAJOR — the marks moved to the game's font asset with no fallback configured | **Fixed**: every mark is checked against the asset (`HasCharacter`, fallbacks searched) and drawn as its ASCII stand-in with one warning per mark; the five codepoints are pinned in `OffScreenArrowText` with a mutation row. Which glyphs the asset carries stays a run fact (§5) |
| B2 | MINOR — the RED recipe did not reproduce as written, and its count was not derivable from the sentence | **Fixed**: the recipe is stated exactly and re-run on the final test set — 36 failed / 27 passed / 63 total, 21 by file-not-found and 15 by pin assertion (§4) |
| N2 | MINOR — `ApplyMarkers`' `break` hid every marker after the first failed projection, coupling the pings to a bad nameplate in a way HEAD could not | **Fixed**: the pass skips that marker, fills the pool by its own cursor and hides the tail |
| N5 | MINOR — the new limits claimed the behind-camera behaviour was "unchanged" | **Fixed**: the limit names the added step and what it can do instead (§5, the ticket) |
| N8 | MINOR — two "current" self-check files still referenced the deleted `LocationPingOverlay` | **Fixed**: both rows say what superseded them, and the review ticket that named the class was annotated too |
| N9 | MINOR — `ApplyHud` treats an empty `StatusText` as "nothing to show" while the model documented `null` | **Fixed**: the model's doc states null OR empty, and why an empty translated line must not take a slot |
| N10 | MINOR — the projection-failure and no-camera branches were silent | **Fixed**: one Warning per surface for a refused projection, one Debug for a missing camera |
| N11 | NIT — `Camera.main` is asked every frame with no cache | **No change, reason in the code**: the surface outlives the scene its camera belongs to, so a cached camera would have to be re-validated anyway |
| N12 | NIT — the two glyph sizes are `float` where HEAD had `int`, and the boxes were unpinned | **No change to the values** (22/28 in a 32/40 box, byte-for-byte HEAD's); the boxes are pinned now (N16) |
| N13 | NIT — `OnlineUiWorldOverlay.None` allocated a fresh list per access | **Fixed**: one `static readonly` empty list, so two suppressed frames cannot read as two different shapes |
| N14 | NIT — the pool's reuse protocol is held only by textual checks | **Recorded** as a limit (§5 and the ticket): nothing in this tree can build the labels it drives |
| N15 | NIT — `Marker`'s constructor takes a `Typography` it never uses | **No change**: it carries the FONT asset into `CreateLabel`; the size is written per role afterwards |
| N16 | NIT — the arrow/ping label sizes were un-pinned literals | **Fixed**: `ArrowLabelWidth/Height`, `PingLabelWidth/Height`, `PingLabelOffsetY`, `HudWidth/HudRowHeight` are pinned by the boxes fact, with a mutation row |
| N17 | NIT — "`Markers` is never null" is false for a `default(OnlineUiFrame)` | **Fixed**: the doc says a default-built frame is not a supported input, and the view reads a null list as nothing to show |
| N18 | NIT — `_worldOverlay = null;` was a weak substring anchor | **Fixed**: the matcher asks `Push`'s body for the apply and `DestroySurface`'s body for the nulling, with a new "leaked past the surface" row |
| N19 | NIT — the pin harness's `runtime` tree was dead capability | **Fixed**: `TheRuntimeOwnsTheArrowMarks` pins the five codepoints and the direction mapping over `runtime/OffScreenArrowText.cs`, with a mutation row |
| N20 | NIT — the suppression pin duplicated the surface pin's frame expression | **Fixed**: this pin owns the CONDITION (the gate and console test); `OnlineUiSurfacePinTests` owns the push it guards |
| N1 | NIT — mutation row 2 (`BuildWorldOverlay` → `BuildWorldFrame`) would also break the build | **Fixed**: the row is now "the frame always pushes an empty overlay", which compiles |
| N3 | NIT — the surface's comment called the apply order "draw order" | **Fixed**: reading order, with the draw order named as the sibling index |
| N4 | NIT — `review/middle-click-location-marker.md` still described the ping as IMGUI | **Fixed** with N8 |
| N6 | NIT — the marker test used the `\u25CF` escape while production writes the literal `●`, so a plugin-side glyph change would not fail it | **Fixed**: the production spelling is pinned in the plugin's own fact, with a mutation row |
| N7 | NIT — the marker's doc says the glyph is empty for a nameplate while the view decides by `Kind` | **Fixed (wording)**: the doc names `Kind` as what decides and the factories as the only production constructors |

