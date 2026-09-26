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
4. **S3 — free colour.** The hex field and swatch grid on the uGUI controls, the config entry and its
   profile carry, the free-colour path through `PlayerColorValue`/`PlayerColorResolver`, and a live
   swatch. The wire is untouched.
5. **S4 — retirement pass.** Retire the scoped raycast blocker for the migrated surfaces and decide the
   two surfaces IMGUI still owns (the world-space overlays and the command console overlay).

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
  and the surface's input-blocking story belongs to the S4 retirement pass.
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

## Non-goals

- Not a mod-facing UI API and not a window framework for other mods' windows.
- Not a change to the game save, the session, the wire or any gameplay behaviour. A local UI preference
  is in scope — see the scope call above.
