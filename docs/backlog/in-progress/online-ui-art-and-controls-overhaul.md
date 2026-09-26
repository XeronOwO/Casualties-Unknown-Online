# The Online UI's art and controls are placeholders

- Status: In progress
- Priority: High
- Category: Online UI / presentation and interaction
- Source: User request (2026-09-26). Three asks in the user's words: much of the Online UI is simplified and does not match this game's style, and the game's own art style should be the reference; the "dropdown" is a button that lists buttons after it is clicked, which reads wrong (is there no dropdown control?); and the colour choice is half-done — the player should be free to pick, with a palette or an RGB input, instead of a few presets. The user's own reading of the cause: the simple UI was a speed choice when the online layer was pushed forward, and it should now be done properly.
- Related: `review/online-ui-panels-request-alpha-blend-false.md` (the theme's blended frames), `review/cuo-launcher-button-obscures-the-view.md` (the launcher's idle fade), `docs/evidence/selfchecks/ui/online-ui-window-selfcheck.md`, `docs/evidence/selfchecks/ui/online-ui-polish-selfcheck.md`

## The gap

Three asks, one family: the Online UI is hand-rolled IMGUI in a theme of its own, and it ignores the
game's own visual language.

1. **Art.** `OnlineUiTheme` is a flat dark panel with a 1 px border, a system font and a palette chosen
   when the window was built. It does not read as this game, whose own screens use its sprites, fonts
   and panel shapes.
2. **Controls.** There is no dropdown: a "choice" is a `GUILayout.Button` showing the current value
   which, when clicked, lists the options as more buttons (the shape the user described). The same
   hand-rolled shape is used wherever a small choice set exists.
3. **Colour.** The player colour is a fixed preset row, so a player cannot pick an arbitrary colour.

## What done looks like

- The Online UI's surfaces read as part of the game — its panel shapes, palette, typography and control
  look — reusing the game's own UI assets wherever a mod can reach them.
- Choice controls behave like real controls: a dropdown that opens, tracks the pointer, closes on
  click-away, Escape and selection; a colour input that accepts any colour the player wants.
- No regression: the existing actions, hotkeys, layout anchors, translation keys, the launcher's idle
  fade and the blended frames keep working, and the gates that pin them stay green.

## Decision (2026-09-26, user ruling)

- **Destination: rebuild the Online UI on uGUI, reusing the game's own controls and art — staged.** The
  game is uGUI + TextMeshPro with zero IMGUI, so "reads as the game" is not reachable from the IMGUI
  shell: `GUIStyle.font` cannot take a `TMP_FontAsset`, every control would stay hand-rolled, and an
  IMGUI window is invisible to the game's `EventSystem` — the reason `OnlineMenuInputGuard` exists.
- **Colour input: a hex field plus a swatch grid.** The game has no colour picker and no player-colour
  feature at all (every `SetColor` site is a crystal or an effect; no `ColorPicker`/HSV class in the
  assembly), so rule 8's "reuse the native UI" cannot be satisfied on this surface — this is the recorded
  blocker, and the chosen form follows the game's own hex idiom
  (`ConsoleSettings.hexBackgroundColor/hexTextColor` parsed by `ColorUtility.TryParseHtmlString`,
  `ConsoleScript.cs:1635-1643`). A swatch grid sits beside it for speed.
- **Scope call (technical, recorded rather than asked):** the non-goal below means the game save and the
  session, not a local UI preference. An arbitrary colour is not an index, so the colour needs a new
  local config entry, and that entry rides the Preferences profiles like every other preference key; the
  wire is untouched because `NetColorRgbaMsg` already carries four floats.
- **The pins are a design input, not cleanup:** the stage that moves a pinned surface re-pins it in the
  same change (the launcher's rect and idle-fade contract, the theme's draw census, the blended-frame
  census), and the pin set stays green throughout.

## Reconnaissance (2026-09-26)

The fact base the overhaul is built on (full report `%TEMP%/cuo-ui-recon.md`, session artifact):

- **The "dropdown" is confirmed**: `OnlineUiPreferencesDrawer.DrawDropdown` renders a `GUILayout.Button`
  showing the current value and, when open, one more button per option **inline in the page's flow** — no
  overlay, no click-away dismissal, no keyboard, no max height, no own scroll. Three call sites (log
  level, language, player colour) and three independent open/closed booleans on `OnlineUiWindowState`.
- **The colour limit is local only**: the picker offers eight `ColorKeys` and the config stores an `int`
  index, while the wire already carries four floats (`NetColorRgbaMsg` on the handshake, join and colour
  messages; `MemberPresenceTable.SelectedColor`; `OnlineUiContext.PlayerColor`).
- **The game uses uGUI + TextMeshPro and zero IMGUI** (no `OnGUI`, `GUIStyle`, `GUILayout` or `GUI.*` in
  the whole decompiled assembly). It has a real dropdown (`TMP_Dropdown`, `SettingsMenu.cs:105-116`,
  `RunSettingDisplay.cs:108-113`) plus Slider, Toggle and `TMP_InputField`, built from `Resources.Load`
  prefabs (`Utils.cs:10-19`: `Special/SettingsMenu`,
  `Special/GameSettingFloat|Int|Dropdown|Bool|Input|Language`). Interactions play
  `PlayerCamera.PlayUISound("miniClick"/"click")`, and there is a global `PlayerCamera.uiScale` the mod
  never consults — the mod has no UI sound, no scale and no tooltip outside its console.
- **The look is prefab-serialised**: fonts are TMP assets and `Image.type`/9-slice never appears in code,
  so neither can be read from the decompiled tree — they need a runtime probe or come free by
  instantiating the game's own prefabs.
- **Reuse is mechanically cheap**: `UnityEngine.UI.dll` and `UnityEngine.IMGUIModule.dll` ship in the
  game's `Managed` (and `UnityEngine.UI.dll` is already vendored in `references/`); the game-assembly
  binding gate forbids only `Assembly-CSharp`. Canvas anchors exist (`PreRunScript.instance.mainCanvas`,
  `PlayerCamera.main.mainCanvas`).
- **Our own pins are the hardest constraint**: `OnlineUiLauncherFadeTests` pins the theme and the
  launcher as source text with explicit ceilings (six `GUI.DrawTexture(` calls in the theme, five in the
  frame, a five-row surface census, the launcher's verbatim rect); `OnlineUiOverlay.cs` sits at 576 lines
  against the 600-line aggregate gate. `ui/online-ui-selfcheck.md` is historical — do not cite it.

## Stages

Each stage is a cycle of its own: red where behaviour changes → implement → gates → independent
adversarial review → one commit.

1. **S1 — facts and host.** A read-only runtime probe for the four unknowns (the active `TMP_FontAsset`,
   a live settings row's `Image.sprite` / `Image.type` / `pixelsPerUnitMultiplier`, the chrome colours,
   `PlayerCamera.uiScale`) plus a uGUI host that parents a canvas under
   `PreRunScript.instance.mainCanvas` / `PlayerCamera.main.mainCanvas` and instantiates
   `Special/GameSettingDropdown` and `Special/GameSettingInput`. Verifiable here: the pure parts by test,
   the rest by build and the gate set; the logged values come from one game run.
2. **S2a — the native surface and the launcher (landed 2026-09-26).** CUO's canvas parented under the
   game's canvas, ACTIVE and interactive, with the launcher button on the game's own control prefab and
   the idle fade applied to that control. The launcher is the slice that proves the whole mechanism —
   canvas lifecycle, a game prefab instantiated for real, uGUI input, hover, the game's own click sound,
   the intent channel — before six pages ride on it, and it is small enough for one game run to judge.
3. **S2b — the window family on the surface (landed 2026-09-26).** The window shell, tabs and page controls on the game's own
   control prefabs, drawing on the surface S2a proved. The IMGUI theme then stays only for the surfaces
   not yet migrated.
4. **S3 — free colour (landed 2026-09-26).** The hex field and swatch grid on the uGUI controls, the config entry and its
   profile carry, the free-colour path through `PlayerColorValue`/`PlayerColorResolver`, and a live
   swatch. The wire is untouched.
5. **S4 — retirement pass (landed 2026-09-26).** The blocking the migration made redundant — or harmful —
   is retired: CUO's own guard leaves CUO's own surface alone (a blocker laid over it covered the launcher
   and the window and swallowed every click on them), and both world input paths now ask ONE pointer
   census, which knows the launcher's rectangle. The IMGUI faces were then decided one by one: the command
   console overlay STAYS IMGUI (a developer surface with a text input, and the reason the modal blocker
   survives), while the quick panel and the player context menu move in S5 and the world-space overlays in
   S6.
6. **S5 — the last player-facing IMGUI panels (landed 2026-09-26).** The quick panel and the in-world player context menu onto
   the game's own controls, on the surface S2a/S2b proved. They are the last consumers of
   `SetOnlineUiScopedBlocks` / `OnlineUiBlockRect` / `OnlineScopedRaycastFilter`, so that mechanism retires
   with them (the same round, not a later one).
7. **S6 — the world-space overlays.** The nameplates, the off-screen arrows, the network HUD and the
   location pings onto TMP labels on CUO's canvas, so their typography comes from the game's own font asset
   rather than from the IMGUI skin's built-in font. They take no input, which is why they were the last
   face left: what is left to win there is the look, and no control is involved.

## What landed — S1 (2026-09-26)

The stage's premise was that the four unknowns need a runtime reading and that the host which can take
one is adapter-side, because only the adapter may reach the game's canvas. Both landed; the reading
itself needs one game run (self-check: `docs/evidence/selfchecks/ui/online-ui-native-facts-selfcheck.md`).

- **The probe.** `IOnlineUiNativeFactsQuery.Capture()` (Runtime port, adapter implementation) builds a
  CUO canvas under the game's own main canvas, instantiates `Special/GameSettingDropdown` and
  `Special/GameSettingInput` into it, reads the game's font asset, a row's `Image` sprite / type /
  pixels-per-unit multiplier / 9-slice border, a bounded census of the live canvas' image styles and
  `PlayerCamera.uiScale`, and disposes the probe the moment a reading is complete — or, at the latest,
  when the adapter is disposed; between attempts the inactive canvas and its rows stay in place, because
  two of the four facts appear at different moments and rebuilding them per retry would be waste. The
  hierarchy is inactive from its first statement, so nothing renders, takes input or joins the game's
  `EventSystem`.
- **The pure half.** The Runtime owns when to stop asking (`OnlineUiNativeFactsCapturePolicy`: 500 ms
  between attempts, a five-minute deadline, an attempt budget, four terminal states) and how a reading
  reads (`OnlineUiNativeFactsReport`, `OnlineUiNativeStyleCensus`) — 29 facts across four classes that
  need no game.
- **The boundary held.** Unity objects stay adapter-side: the port carries plain values
  (`OnlineUiNativeFacts`), the plugin resolves it optionally and prints the report once, and the plugin
  project still binds no game assembly. The adapter's port census is 13 ports / 17 members.
- **Deliberately not spawned: `Special/SettingsMenu`.** Its `Start` builds the whole settings screen and
  claims the static `SettingsMenu.instance` that the game's own `OpenMenu` returns early on, and its
  `Close`/`ResetToDefault` write the settings file (`SettingsMenu.cs`), so only the row prefabs are a
  safe probe target. The rule is pinned with a mutation control that adds exactly that load.
- **The reading is pending, by design.** Nothing was deployed this cycle, so the four values come from
  the first game run with the built plugin: the probe logs them once under the `CUO UI native facts`
  prefix, and logs a single warning naming what stayed missing if the deadline passes. S2 must not
  depend on the values before they land — it can be built against the host and the reading's shape,
  which is what this stage fixes.

## What landed — S2a (2026-09-26)

The mechanism first, the window second: S2a makes the game's own UI a surface CUO stands on, and proves it
with the one control that is small enough to be judged on its own. Self-check:
`docs/evidence/selfchecks/ui/online-ui-native-surface-selfcheck.md`.

- **The live surface.** `OnlineUiSurfaceHost` (GameAdapter) creates CUO's canvas as a CHILD of the game's
  own canvas — active, sorting 30000, with a `GraphicRaycaster`, the in-run canvas preferred over the
  pre-run one, rebuilt when the canvas it hung on is destroyed or deactivated, and an EventSystem created
  only when the scene has none so the game's input stack is never duplicated. A frame that arrives before
  the game has a canvas is dropped; the surface never throws at the frame callback and owns nothing that
  outlives the adapter.
- **The launcher is the game's control.** `OnlineUiLauncherView` instantiates
  `Special/GameSettingLanguage` — the same button-row prefab the game's settings screen uses for a
  language row — and stretches it into the launcher's rect (right margin 12, top margin 12, 158 by 34,
  top-right anchor): its sprite, 9-slice, font and scale come with the prefab instead of being guessed, and
  the caption comes from the Runtime's own rule. The idle fade's alpha lands on the control's
  `CanvasGroup`, so both halves fade together, and the pointer fact is POLLED from the rect (uGUI's
  enter/exit callbacks fire on movement, so a launcher appearing under a stationary pointer would never
  report the hover that keeps it opaque). A click queues the intent BEFORE the game's own `miniClick`
  plays. If the game ever moves the prefab, a plain uGUI button keeps the window reachable and a warning
  names the miss.
- **The Runtime half.** `OnlineUiFrame` (caption + opacity), `OnlineUiIntent`/`OnlineUiIntentKind` (the
  click, the hover flips), `OnlineUiLauncherText` (the `▲`/`▼` marker rule) and the port
  `IOnlineUiSurface` (`Push` / `TryDequeueIntent`) — plain values, no Unity object crossing the seam, the
  14th port and the 19th member of the adapter composition.
- **The plugin's half.** `OnlineUiHost` drains the intents (the click calls the overlay's
  `ToggleWindow`, which keeps the Home → Players landing while a session runs) and pushes one frame per
  update, the rule asked with the runtime clock and the caption rebuilt only when its inputs change. The
  IMGUI launcher, its theme style and its alpha overload are deleted; the theme now draws only the
  surfaces that have not migrated (the modal window's frame, the quick panel, the context menu, the
  console overlay), and a pin scans the plugin for the IMGUI launcher's own fingerprints growing back.
- **Not yet proven, by design.** Nothing was deployed and no game ran, so the picture itself — the game's
  prefab instantiated for real, the click, the hover, the sound, and whether the launcher reads as this
  game — is the user's run. S2b does not depend on it beyond the mechanism it inherits.

### Limits recorded with S2a

- **The "no second launcher" pin is a fingerprint scan, not a proof of absence.** It looks for the theme's
  launcher style, the alpha overload the launcher used, and the launcher's GUI-space rect over the plugin
  tree; a launcher regrown at ANOTHER rect, or drawn through a different call, would slip past it — the
  rect literal is what catches the ordinary regression.
- **The surface rebuilds whenever the game's canvas is deactivated**, costing one canvas destroy and one
  instantiation of the game's prefab on the next frame; it recovers, and the churn is unmeasured (a game
  run shows whether it happens in practice).
- **The launcher's rect is on no input-blocking census** (`IsPointerOverUi` and the scoped blocks cover the
  IMGUI surfaces only), so a middle-click over the launcher still pings: pre-existing launcher behaviour,
  and the surface's input-blocking story belongs to the S4 retirement pass. **(Closed by S4: the launcher's
  polled rectangle is a fact of the pointer census both world input paths ask, and the guard no longer
  covers CUO's own canvas at all.)**
- **The EventSystem rule pins "no ENABLED one"** — which is what `EventSystem.current` reports; the game
  dereferences it unguarded from its pointer-over-UI path, so the distinction is expected to be
  unobservable in practice.

## What landed — S2b (2026-09-26)

The window that S2a made reachable stopped being an IMGUI panel of its own. It is now a display list the
game's own settings rows render, driven through the same surface, the same frames and the same intent
channel the launcher proved. Self-check:
`docs/evidence/selfchecks/ui/online-ui-window-family-selfcheck.md`.

- **The model (Runtime, pure).** `OnlineUiElementKind` (label, button, text field, toggle, dropdown,
  slider), `OnlineUiTextStyle`, `OnlineUiElementModel` (one flat record, a factory per kind),
  `OnlineUiRowModel`, `OnlineUiWindowModel` and `OnlineUiControlIds`; `OnlineUiFrame` carries the window
  (null = closed), `OnlineUiIntent` carries the control id and the payload, and
  `OnlineUiRowLayout.LineOf` is the pure wrap rule — whether a member's twelve buttons form two lines or
  three is decided, and tested, without a Unity runtime.
- **The controls are the game's.** The adapter instantiates the settings screen's own rows:
  `Special/GameSettingLanguage` for every button and the tab row, `GameSettingBool` for a checkbox,
  `GameSettingDropdown` for the three choices, `GameSettingFloat` for a slider (child 2 shows the value),
  `GameSettingInt`'s `TMP_InputField` for a text field. The window's frame reuses that prefab's sprite,
  `Image.type` and pixels-per-unit multiplier, and every label takes the game's font and size from it — so
  the only thing CUO still chooses is the tint.
- **The shell.** `OnlineUiWindowView` owns the frame, the draggable title bar, the close control, the tab
  row and the scrolling page, and reconciles one model per frame: a control is reused when its kind and id
  still match, written only where it changed, and destroyed when the model drops it. The frame is a raycast
  target, so a click inside the window can no longer reach the world behind it, and the window's own rect
  is polled into the fact that keeps an in-world right-click out of the world menu.
- **The plugin builds, it does not draw.** `OnlineUiPageBuilder` is what a page writes to; every control
  registers the action its id carries, so the model goes out as a value and the intent comes back to the
  same registration (an id the current model no longer offers is dropped and logged). `OnlineUiWindow`
  keeps the tab row and the `switch (_state.Page)` dispatch; the six drawers became `Build(ctx, page)`.
- **The user's three asks, where S2b touches them.** The "dropdown" that listed buttons inline is gone:
  log level, language, player colour and the native-binding parity level are the game's own
  `TMP_Dropdown`, which opens, tracks the pointer and closes on click-away, Escape and selection. The
  hand-rolled toolbar and the `Toggle`-as-button shape are gone with it. (The free colour input is S3.)
- **Deleted in the same round.** The IMGUI window and its `GUI.Window`, the theme's window/title/label
  styles, `OnlineUiWindowState.Scroll` and the three dropdown booleans, and the overlay's 21-parameter
  draw call (it now takes the frame's context). The theme now draws only the quick panel, the context menu
  and the console overlay.
- **The controls carry the prefabs' own size.** The adapter seeds each view's `LayoutElement` with the
  game's row prefab `sizeDelta`, because the game places those rows by hand and a layout group reads
  nothing off a rect — without that seed a row whose prefab carries no layout value would lay out at zero
  height. A model width hint still wins over the seed.
- **The pins moved with it, in the same change.** `OnlineUiLauncherFadeTests`' theme census lost the
  window's row; `OnlineUiConsolePageRemovalPinTests` re-anchored the tab row and the page dispatch onto the
  model build (same contract: six tabs, `tab.<page>` keys read from source, one case per page);
  `OnlineUiSurfacePinTests` pins the new frame shape and gained a "pushes no window" mutation;
  `OnlineUiWindowSurfacePinTests` is new (14 pins + 16 real-source mutation rows) and
  `OnlineUiRowLayoutTests` covers the wrap rule including the line spacing. No port was added (14 ports /
  19 members).

### Limits recorded with S2b

- **The look, the layout and the input are the user's run.** No test can instantiate a `GameObject`, so
  whether the game's row prefabs behave when instantiated ACTIVE, whether the layout reproduces the IMGUI
  window's shape, whether the wheel/typing/click reach the controls, and whether the result reads as this
  game are all game-run observations. The S1 reading that could align the chrome colours is still pending.
- **The window depends on the native surface.** A composition without an adapter has no canvas and
  therefore no window — the trade S2a already made for the launcher, and the reason S4 owns the remaining
  IMGUI surfaces.
- **The pointer-over-window fact is one frame old** (it is polled and reported as a flip), so a world
  right-click in the frame the window appears or disappears is judged against the previous frame.
- **The free-text field borrows the integer row's prefab** — the only input field a mod can reach — and
  sets its content type and character limit on the instance.
- **Layout hints are hints, and the sizes are seeded.** A width that does not match what a prefab actually
  prefers shows up as a row wider or narrower than intended; the prefab's own `sizeDelta` is seeded into
  each control's layout so nothing collapses, but whether the authored size reads well here is a run
  observation.

## What landed — S3 (2026-09-26)

The player colour stopped being a choice among eight presets. A colour the player names is not an index,
so the preference now carries the colour itself and the picker is a hex field plus a palette of blocks;
the surface S2a/S2b proved carries both. Self-check:
`docs/evidence/selfchecks/ui/online-ui-free-color-selfcheck.md`.

- **The codec is the Runtime's, and it is pure.** `PlayerColorValue.TryParseHex` / `ToHexString` read and
  write the game's own hex idiom in exactly two forms (`#RRGGBB`, `#RRGGBBAA`; either case, surrounding
  space ignored) and refuse everything else — including the three-digit shorthand, so a value that is
  still being typed never parses. That is what lets the rule be tested without Unity, and what lets the
  page tell a valid colour from a half-typed one.
- **The preference is the colour.** `[UI] PlayerColor` replaces `[UI] PlayerColorIndex`: empty = the
  automatic per-SteamId palette, otherwise the colour's own text. `PlayerColorConfigEditor` reads and
  writes it as a `PlayerColorValue?`, a selection that changes nothing writes nothing, and an unreadable
  stored value is logged once at startup and read as automatic. The entry is bound through the same
  `ConfigFile` the profile store walks, so a configuration profile carries it like every other entry.
- **The picker is one row of the page's own vocabulary.** The current colour is a block beside its name
  (the palette's name, the hex text, or the automatic label), then the hex field, then the palette as one
  clickable block per Runtime palette entry, then Auto. What the player types is applied the moment it
  parses, so the live block and every marker move with it; text that is not a colour yet leaves what the
  player had and says so. One new `OnlineUiElementKind.ColorSwatch` carries a block — the same game button
  row the launcher uses, with the colour laid over the graphic it shows — and a block with no id is the
  preview, which reports nothing.
- **The palette has one source of truth.** `PlayerColorResolver`'s table now holds a colour and the name it
  is offered under; `PaletteValues` / `PaletteNames` are projections of it, and the index lookup
  (`TryGet`) is gone with the index. The picker reads both from the Runtime, and a test holds every name to
  its catalogue key (`prefs.color.<name>`) in both languages.
- **A translucent choice reads the same everywhere.** The free colour is the first thing that can produce
  an alpha below 1, so the two consumers that rendered the colour as a three-channel tag were aligned in
  the same change: the member list's identity line and the Home page's coloured name now write the
  eight-digit tag form the page's own status lines already use.
- **An edit belongs to the window it was typed in.** The field's text lives in the window state while it is
  being typed (the model carries it back, so box and model cannot disagree), the "not a colour yet" line is
  derived from that text rather than kept as a flag, and closing the window drops the edit.
- **Deleted in the same round.** The palette-index entry and its range validator, `ColorKeys` in the
  drawer, the colour dropdown and the `Action<int>` colour delegate (now `Action<PlayerColorValue?>`).
- **The pins moved with it, in the same change.** `OnlineUiColorPickerPinTests` is new (7 pins + 7
  real-source mutation rows, registered in its matcher); `OnlineUiWindowSurfacePinTests`'
  `EveryInteractiveControlReportsItsOwnId` now matches the current-id guard on the button listener;
  `OnlineUiConsolePageRemovalPinTests` and `AdapterCapabilityPortShapeTests` are untouched and green (no
  port was added — 14 ports / 19 members).

### Limits recorded with S3

- **How a block reads is the user's run.** The block is the game's own button row tinted, so the sprite's
  own colour multiplies the fill — whether eight of them read as a palette at the player's canvas scale,
  and whether the tints are recognisable, is a game observation.
- **The field is the game's integer row reused.** Its content type and character limit are set on the
  instance, but whether the caret, the typing and the blur-snap-back to the stored value feel right is a
  game observation.
- **A completed entry is one commit.** A value that is still being typed is never stored, and an entry
  that parses is stored and announced at once; whether that reads as "live" (rather than as a missing
  Apply control) is the user's judgement.
- **The S1 chrome reading is still pending**, so the picker's tints come from the CUO theme rather than
  from the game's own chrome colours.
- **Alpha is carried everywhere but proven nowhere yet.** `#RRGGBBAA` parses, every consumer now passes all
  four channels on (three tint their own graphics, two write an eight-digit tag), but whether a translucent
  marker or a translucent name reads well — and whether the game's TMP renders partial alpha inside a
  `<color=#RRGGBBAA>` tag — is the user's judgement.
- **The window still needs the adapter** (no canvas, no picker) — the trade S2a recorded, and the reason
  S4 owns the remaining IMGUI surfaces.

## What landed — S4 (2026-09-26)

The retirement pass. The stage's premise was that the input blocking built for an IMGUI-only UI needs an
item-by-item verdict once the surfaces it was built for are uGUI — and the first verdict was that one of
those items was not merely redundant but harmful. Self-check:
`docs/evidence/selfchecks/ui/online-ui-input-blocking-retirement-selfcheck.md`.

- **The defect the pass found: CUO's own guard covered CUO's own surface.** `OnlineUiSurfaceHost` creates
  CUO's canvas under the game's canvas and builds the launcher and the window as its children; both
  blocker sweeps in `OnlineMenuInputGuard` then walked `Object.FindObjectsOfType<Canvas>()` with no
  exclusion, added a full-rect transparent `Image` (`raycastTarget = true`) to every active screen-space
  canvas — CUO's own included — and called `SetAsLastSibling()`. Within CUO's canvas that blocker is the
  last child, so it draws over and raycasts before the launcher and the window: the click is consumed by a
  graphic with no handler. The modal opens the frame AFTER the window does (`OnlineUiHost.Update` reads
  the window's visibility before draining the click that opened it), so this was the normal path, not a
  race: with the window open, its tabs, its page controls, the colour field, its own × and the launcher
  were all unanswerable. The pass retires exactly that: the surface marks the root it builds
  (`OnlineUiSurfaceMarker`) and every sweep asks the marker, so a blocker meant for the GAME's UI can never
  land on CUO's.
- **The launcher enters the pointer census.** S2a recorded that the launcher's rectangle was on no
  input-blocking census: a middle-click over it pinged the world and a right-click there opened the
  in-world menu. The fact was already crossing the seam — the surface polls the launcher's rectangle every
  frame for the idle fade — so the same flip now also feeds `OnlineUiPointerCensus` (Runtime, pure), and
  both world input paths ask that one rule instead of keeping a list each.
- **One rule, two questions, one rectangle source.** `BlocksWorldPing` (the middle-click ping) and
  `BlocksWorldMenu` (the in-world right-click) share the facts — pointer over the launcher, pointer over
  the window, the IMGUI panels' rectangles, and the modal flag — and differ in exactly the way the two
  paths always did: a modal CUO surface owns the SCREEN, so no ping becomes a world ping anywhere, while a
  right-click outside the window's own rectangle still targets a player. The adapter's scoped blockers and
  the census are built from one method (`OnlineUiOverlay.CollectOverlayRects`), so a click cannot be
  blocked on one path and leak on the other.
- **The console's mutual exclusion is now explicit.** With the blocker no longer covering CUO's canvas,
  the launcher is clickable while the command console overlay is open — and the console is a modal surface
  that owns the input, so `ToggleWindow` refuses and logs (at information level, because the plugin's own
  default minimum is Information) instead of letting a launcher click open the window behind it. That
  refusal used to be an accident of the blocker.
- **A pointer fact is retracted when the view that reported it dies.** The launcher and the window report a
  FLIP only and a rebuilt view starts un-hovered, so `OnlineUiSurfaceHost.DestroySurface` queues a
  hover-left for each of them before dropping them: a census left holding "the pointer is over CUO's UI" is
  global and would block every world ping and every in-world right-click until the pointer happened to
  leave the launcher again. The independent review found that hole; the fix and its pin landed in the same
  round.
- **The mechanism audit, item by item.** What the IMGUI era left behind, and what each piece is for now:
  `SetOnlineUiModal` is the patches' gate (`PlayerCameraHandleInputPatch` returns false on it,
  `PauseHandlerTogglePausePatch` reads it) and stays; its full-screen blockers and its `AdaptiveButton`
  sweep stay for the GAME's canvases and controls, narrowed by the ownership rule;
  `SetOnlineUiScopedBlocks` / `OnlineUiBlockRect` / `OnlineScopedRaycastFilter` still serve the two IMGUI
  panels, which cannot be seen by uGUI; `SetOnlineUiEscapeSurfaceVisible` still serves the quick panel's
  non-modal ESC. Nothing was deleted from that set — the redundant part was the blocking ON the migrated
  surface, and the two facts the guard must never touch again are pinned.
- **Deleted in the same round.** `OnlineUiQuickPanel.Contains` — the census asks the panel's rectangle
  through the one rect list now, so the panel's own point test had no caller left.
- **The pins moved with it, in the same change.** `OnlineUiInputBlockingPinTests` is new (7 pins + 11
  real-source mutation rows, one Matcher registry); `OnlineUiSurfacePinTests`' two launcher-hover clauses
  became the census's launcher fact (same contract, one more reader); `OnlineUiBlockRectTests`,
  `OnlineUiLauncherFadeTests`' theme census and `AdapterCapabilityPortShapeTests` are untouched and green
  (no port was added — 14 ports / 19 members).

### Limits recorded with S4

- **That the click lands is a game observation.** The retired blocker's ordering argument is read from the
  code (last sibling, transparent graphic, the same premise the guard itself was built on), and the pins
  hold the shape — but whether the game's own controls answer again, at the game's UI scale, on the first
  real run is the user's.
- **The census facts are up to two frames old on the ping path.** The middle-click is handled in
  `OnlineUiHost.Update` BEFORE the frame's intents are drained, and the surface polls the pointer at the
  END of the frame, so a middle-click within about two frames (~33 ms) of arriving on the launcher still
  pings; the leaving direction is one frame. The right-click path runs in the IMGUI pass after `Update`, so
  it sees the same frame's facts. The IMGUI panels' rectangles are read live in the same call.
- **The ownership rule is opt-in per root.** The surface marks the root it builds; an ACTIVE CUO canvas
  built elsewhere without the marker would read as the game's and be blocked. Nothing is missed today — the
  only other CUO canvas, the S1 probe's, is `root.SetActive(false)` from its first statement, so
  `FindObjectsOfType<Canvas>` never sees it.
- **Whether a CUO control could ever be an `AdaptiveButton` is not decidable from this tree** (the game's
  prefabs live outside the repository), so the third sweep's ownership clause is the rule applied uniformly
  rather than a fix for an observed object.
- **The context menu keeps its last rectangle while the console is open** (its bounds are only written while
  it is drawn), so the census can block a right-click there; the menu is not drawn in that state and its
  click path is gated on the console being closed, so it is not a reachable input path.
- **The console-versus-world-menu asymmetry is the caller's gate.** The census deliberately does not count
  a modal surface as "the world menu is blocked here" (the IMGUI window's behaviour); while the console is
  open the context menu is not drawn and the window and quick panel are closed, so the reachable states
  are the intended ones — but a future surface that drew the context menu while the console is open would
  find the census permissive outside the panels, and that gate is where it would be answered.
- **The guard pin's census counts the sweeps that exist.** It requires every current sweep to ask the
  ownership predicate; a THIRD sweep added later that never asks it would not raise the count and would
  not be caught (declared in the pin's own coverage note).
- **The ticket's goal is not reached yet.** The quick panel and the player context menu are still IMGUI
  panels of the flat theme, and the world-space overlays still draw with the IMGUI skin's font: the
  player-facing art ask stays open until S5 and S6 land, which is why this ticket stays in progress.
- **The S1 chrome reading is still pending**, so nothing in this pass used it — the retirement depends on
  no runtime reading at all.

## What landed — S5 (2026-09-26)

The last two player-facing IMGUI panels are controls of CUO's own surface, and the machinery that existed
only because they were IMGUI retired in the same round. Self-check:
`docs/evidence/selfchecks/ui/online-ui-panels-selfcheck.md`.

- **One panel view serves both.** `OnlineUiPanelView` (GameAdapter, `OnlineUi/`) is the window's shell
  without the window: the game's own row prefab gives it the frame's sprite, `Image.type` and
  pixels-per-unit multiplier and every label's font, its rows go through the same
  `OnlineUiWindowRowView` / `OnlineUiControlView` / `OnlineUiRowLayout` machinery the pages use, and it
  reports what the player does as the same intents. It is built twice on the surface — the quick panel and
  the player context menu — with the two hover kinds its caller hands it, because each panel is its own
  fact in the census. The game-row template reader it shares with the window moved into
  `OnlineUiControlFactory.ReadRowTemplate`, so the game's art is read in one place.
- **The panels are models, not draws.** `OnlineUiQuickPanel` (docked bottom-right, width 340 — the IMGUI
  panel's own rect) and `OnlineUiPlayerContextMenu` (at the click point, width 240) build
  `OnlineUiPanelModel`s; the menus, the target selectors, the eligibility rules and the two pickers
  (`QuickPanelTargetPicker`, `RemoteTargetPicker`) are unchanged. Their rows come from the SAME member card
  the Players page renders, through `OnlineUiMemberListDrawer.Build`, so eligibility is answered once.
- **One action table for three surfaces.** `OnlineUiOverlay.BuildSurfaces` clears one table, lets the
  window and both panels register into it in the same frame, and `Apply` dispatches an intent to the
  registration that produced its control (`OnlineUiWindow.Apply` moved up with it). Each panel namespaces
  its ids (`quick.`, `menu.` — `OnlineUiPageBuilder` gained the prefix), so one surface's click can never
  be applied to another's control.
- **The pointer census lost its geometry.** All four facts — launcher, window, quick panel, context menu —
  are the surface's own polls now, reported as hover flips and pushed into `OnlineUiPointerCensus`; the two
  questions are parameterless, `OnlineUiBlockRect` and the adapter's `OnlineScopedRaycastFilter` are
  deleted, and `INativeInputBlocker` lost `SetOnlineUiScopedBlocks` with them (14 ports / 18 members,
  `AdapterCapabilityPortShapeTests` re-pinned in the same change). The one exception is by design: the
  context menu's own click-away close reads the census's menu fact instead of a rectangle of its own.
- **The point-anchored panel is placed by a Runtime rule.** `OnlineUiPanelPlacement.ForPointer` clamps the
  menu's corner into the screen (the pointer offset, both edges, and a panel too large hanging from the top
  so its first rows stay visible) and is tested without Unity; the adapter lays the panel out first
  (`LayoutRebuilder.ForceRebuildLayoutImmediate`) so the rule clamps against the real height.
- **The surface finally covers the game's canvas.** A nested canvas is not resized by Unity, so CUO's
  canvas rect is now set to stretch the game's own — without it, "the launcher's top-right corner", "the
  window's centre" and the quick panel's dock were all placed against an arbitrary rect rather than the
  screen. This is the premise S2a's rects already assumed; it is stated and pinned here rather than left
  to be discovered in play.
- **Deleted in the same round.** The scoped-block mechanism (`SetOnlineUiScopedBlocks`, `OnlineUiBlockRect`,
  `OnlineScopedRaycastFilter`, the guard's scoped sweep and its `ScopedBlocksEqual`),
  `OnlineUiOverlay.CollectOverlayRects`, the panels' own rectangles (`OnlineUiPlayerContextMenu.Contains` /
  `Bounds` / `_lastRect`, `OnlineUiQuickPanel.Bounds` / `_rect` and the IMGUI panel's fixed size — the
  surface owns the rect each panel occupies now), the IMGUI member card (`BuildImgui`, `DrawImguiButton`),
  and the theme's now-unused panel frame and styles (`Panel`, `PanelLight`, `Border`, `DrawBackground`,
  `DrawFrame`, `CloseButton`, `Tab`, `Label`, `Section`) — the console overlay is the only themed IMGUI draw
  left, which is what `OnlineUiLauncherFadeTests`' census now pins (one blended rectangle, a ceiling).
- **The pins moved with it, in the same change.** `OnlineUiPanelSurfacePinTests` is new (10 pins + 20
  real-source mutation rows); `OnlineUiInputBlockingPinTests` gained the panels' fact pins and the
  "retired rectangle API stays retired" tree scan (9 pins + 16 rows); `OnlineUiPointerCensusTests` was
  rewritten for the parameterless rule; `OnlineUiPanelPlacementTests` is new (7 cases);
  `OnlineUiSurfacePinTests`, `OnlineUiWindowSurfacePinTests`, `OnlineUiLauncherFadeTests`,
  `AdapterCapabilityPortShapeTests` and `OnlineMenuInputGuardContractTests` were re-anchored (the last also
  gained a negative: the guard must not carry a scoped setter again); `OnlineUiBlockRectTests` was deleted
  with its type.
- **The independent review found one real defect in the first cut, fixed in the same round.** The placement
  clamp passed a screen-pixel pointer beside canvas-unit sizes, so it held only at a canvas scale of 1; the
  pointer is converted into the canvas once and the whole rule is asked in the canvas's own units now, with
  a mutation row that fails if the two are mixed again.

### Limits recorded with S5

- **How the panels read, and whether the click lands, is the user's run.** No test in this tree
  instantiates a `GameObject`: whether the game's own row prefabs behave inside a content-sized panel,
  whether the docked quick panel and the menu at the pointer read as this game, whether the wheel/typing/
  click reach them at the game's UI scale, and whether the menu's own corner is where the player expects
  are all game observations.
- **The canvas rect change is a premise fix, not a measured one.** Stretching CUO's canvas over the game's
  is what makes every anchored control mean what S2a/S2b/S5 say it means, and it is pinned — but whether
  the game's canvas rect (and its scale) is the one the player sees is a run observation, and the change
  lands under the already-deployed launcher and window as well.
- **The menu's height is read after a forced layout.** The clamp needs the panel's own height and a
  `ContentSizeFitter` writes it at the end of the frame, so `PlaceAtPoint` rebuilds the layout immediately
  on every frame the menu is up. That is a small synchronous cost per frame while the menu is open, chosen
  over a one-frame jump.
- **The placement rule reasons in the canvas's units, not in pixels.** The review's catch: the surface is
  scaled by the game's UI scale, so the clamp is asked with the pointer converted into the canvas once, the
  panel's own laid-out size and the canvas's bounds — which makes the margin and the pointer offset scale
  with the UI rather than staying a fixed pixel count (a small, deliberate difference from the IMGUI menu,
  which clamped in pixels).
- **The quick panel's drawn size is its content's now.** Its width and dock are the IMGUI rect's (340, 16
  from the corner), but its height follows the rows instead of the fixed 420 the IMGUI panel reserved, so a
  short panel hugs its content and a long one grows upward from the dock.
- **The panels need the adapter**, exactly as the window does: no surface, no canvas, no panels — the trade
  S2a recorded.
- **The quick panel's layout is no longer hand-wrapped.** The IMGUI panel put up to four targets on one line
  and the rest one per line; the surface wraps them by the Runtime's own rule, which is a slightly
  different (and more regular) shape for five or more candidates.
- **The S1 chrome reading is still pending**, so the panels' tints are the window's tints, not the game's
  own chrome colours.
- **The context menu keeps its own pointer fact one frame old**, like the window's: the poll runs at the end
  of the frame that pushed the model, so a menu that appears under the pointer reports the hover on the
  following frame. Its click-away close is judged against that fact.
- **The ticket's goal is not reached yet.** The world-space overlays (nameplates, off-screen arrows, the
  network HUD, the location pings) still draw with the IMGUI skin's font: S6 is what closes the art ask,
  which is why this ticket stays in progress.

## Non-goals

- Not a mod-facing UI API and not a window framework for other mods' windows.
- Not a change to the game save, the session, the wire or any gameplay behaviour. A local UI preference
  is in scope — see the scope call above.
