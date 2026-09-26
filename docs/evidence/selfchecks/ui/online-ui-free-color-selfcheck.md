# The Online UI's free colour picker — self-check (2026-09-26)

Ticket `online-ui-art-and-controls-overhaul`, stage **S3 — free colour**. The stage's premise is that the
player colour stops being a choice among eight presets and becomes a colour the player names: a hex field
plus a palette of blocks, a new local preference carrying the colour itself, and the same wire that
already carried four floats. This page records what the change rests on, what landed, what was audited
around it, and what only a game run can judge.

## 1. Mechanism inventory — what the picker rests on

| # | Mechanism | Evidence (quoted / cited) |
|---|---|---|
| 1 | The game names a colour in hex | `ConsoleScript.cs`: `private Color ParseColor(string s)` calls `ColorUtility.TryParseHtmlString(s, ref color)` and throws `"\" + s + "\" is not a valid color value! (#FFFFFF, etc)"`; `ConsoleSettings` carries `hexBackgroundColor` / `hexTextColor` strings beside the parsed `Color` fields — the hex string is the game's own way of writing a colour down |
| 2 | The colour already travels as four floats | `NetColorRgbaMsg` (handshake, join and colour messages), `MemberPresenceTable.SelectedColor`, `SessionService.ReportLocalPlayerColor`, `ISteamService.SetLocalPlayerColor` / `IpDirectSteamService.SetLocalPlayerColor` (through `CuoNetworkRouter.SetLocalPlayerColor`) — a free colour needs no wire change, so the protocol stays 43 |
| 3 | The preference layer carries every entry | `ConfigurationProfileStore.TrySaveCurrent` walks `_config.Keys` and writes each `entry.GetSerializedValue()`; `TryApply` writes them back through `SetSerializedValue`. The colour's entry is bound through the same `ConfigFile` the store is constructed with (`PluginDependencyRegistrar`), so a profile carries it with no wiring of its own |
| 4 | The surface's control vocabulary | `OnlineUiElementModel` (one flat record, a factory per kind), `OnlineUiElementKind`, the game's own row prefabs in `OnlineUiControlView.PrefabPathOf`, and the reconcile in `OnlineUiWindowView.Matches` (a live view is reused when the kind and the id still match) |
| 5 | The text field the picker reuses | `OnlineUiElementKind.TextField` → `Special/GameSettingInt`'s `TMP_InputField` (the only input field a mod can reach), with the content type and character limit set on the instance; the adapter never writes a focused field (`if (!input.isFocused && _value != element.Value)`) |
| 6 | The interaction channel | `OnlineUiIntent` (kind + control id + payload), `OnlineUiWindow.Apply` (the action table the page builder filled by id), `OnlineUiHost.DrainSurfaceIntents`; an id the current model no longer offers is dropped and logged |
| 7 | Where the colour is shown to players | `OnlineUiContext.PlayerColor(id)`: the local preference, else the peer's wire-carried colour, else `PlayerColorResolver.Resolve(steamId)`; consumed by the member list's and the Home page's coloured name, the nameplates and off-screen arrows (`OnlineUiOverlay.ToColor`) and the location pings. All five carry the colour's FOUR channels: the two list sites render an eight-digit `<color=#RRGGBBAA>` tag (the form the Admin and Preferences status lines already used) and the three world overlays tint their own graphics with `new(value.R, value.G, value.B, value.A)` |
| 8 | uGUI routes a click through the row's own graphic | `Button.onClick` on the graphic the row shows (`OnlineUiControlView.ButtonOn` / `SwatchImageOn`), and the adapter's listener reads the view's CURRENT id because a view outlives the element it was built for — which is why an element the model gives no id must report nothing |
| 9 | The game has no colour picker to reuse | ticket's recorded blocker: every `SetColor` site in the assembly is a crystal or an effect, and no `ColorPicker`/HSV class exists. Rule 8's "reuse the native UI" cannot be satisfied on this surface, so the form follows the game's own hex idiom instead |

## 2. What landed

- **The pure codec.** `PlayerColorValue.TryParseHex` / `ToHexString` — the text the field shows, the
  configuration stores and the picker compares by. Exactly two forms are accepted (`#RRGGBB`,
  `#RRGGBBAA`), the digits may be either case and surrounding space is ignored, and the three-digit
  shorthand the game's console accepts is deliberately refused so a value that is still being typed never
  parses. An opaque colour is written back as six digits, a translucent one as eight.
- **One palette, one source of truth.** `PlayerColorResolver`'s table now carries a colour and the name it
  is offered under (`("red", new(0.90f, 0.30f, 0.28f))`); `PaletteValues` and `PaletteNames` are projections
  of it, and the index path (`TryGet`) is deleted — with a free colour there is no index to look up.
- **The preference is the colour.** `[UI] PlayerColor` is a string entry: empty means automatic, otherwise
  the colour's own hex text. `PlayerColorConfigEditor` reads and writes it as a `PlayerColorValue?`
  (`CurrentColor` / `SetColor`), a no-op selection writes nothing, and the host reports an unreadable
  stored value once at startup and treats it as automatic. `PlayerColorIndex` is gone.
- **The picker.** `OnlineUiPreferencesDrawer`'s colour section is the current colour as a block beside its
  name (the palette's name when the stored colour is one of the palette's, the hex text when it is free,
  the automatic label when nothing is stored), a hex field, a line that says what is missing while the text
  is not a colour yet, the palette as one clickable block per entry, and an Auto control. What the player
  types is applied the moment it parses; the live block follows it in the same frame. While it does not
  parse yet, the typed text IS the window state's field value (`PlayerColorInput`) — the model carries it
  back, so the box and the model cannot disagree — and the window's close drops it, so a half-typed edit
  does not come back when the window does.
- **Every consumer shows all four channels.** The free colour is the first thing that can produce a
  translucent marker, so the two sites that rendered it as a three-channel tag were aligned in the same
  change: `OnlineUiMemberListDrawer.Identity` and `OnlineUiHomeDrawer.ColoredName` now write
  `ColorUtility.ToHtmlStringRGBA` — the eight-digit tag form the page's own status lines already use — so a
  colour its owner made translucent reads the same in the lists as it does on the nameplates, the arrows
  and the pings.
- **A colour block on the game's own row.** `OnlineUiElementKind.ColorSwatch` +
  `OnlineUiElementModel.ColorSwatch(id, color, width)`: the adapter instantiates the same
  `Special/GameSettingLanguage` button row the launcher uses and lays the colour over the graphic that row
  shows. A block with an EMPTY id is the preview — nothing is registered for it and its click reports
  nothing.
- **The control view split, because the colour block took it past the line gate.** The prefab half of
  `OnlineUiControlView` (which prefab draws which kind, how it is instantiated so a layout group can size
  it, how its graphic is made clickable and tintable, what stands in when the game ships none) moved into
  `OnlineUiControlFactory` — 476 + 203 lines against the 613 the one file had reached. The split also
  sharpened one fact: `UsesGamePrefab` became `MissedGamePrefab`, because its only consumer warns about a
  prefab that could not be loaded, and a label — which has no prefab of its own by design — was being
  reported as such a miss once per window.
- **The pins moved with it.** `OnlineUiWindowSurfacePinTests.EveryInteractiveControlReportsItsOwnId` now
  matches the current-id guard on the button listener (same contract, one more clause) and its two
  prefab-file pins follow the split to `OnlineUiControlFactory.cs`; the new `OnlineUiColorPickerPinTests`
  holds the nine S3 contracts with one real-source mutation each;
  `OnlineUiConsolePageRemovalPinTests` and `AdapterCapabilityPortShapeTests` are untouched and green (the
  tab row, the page switch and the port census — 14 ports / 19 members — did not change).

## 3. Family audit — what else touches this mechanism

| Surface | Verdict |
|---|---|
| The Preferences page's other two choices | Untouched: log level and language are still the game's own `TMP_Dropdown`. The colour is the only one that stops being a choice among presets |
| Every consumer of the colour | Aligned in this change: `OnlineUiContext.PlayerColor`, the member list's and the Home page's coloured names (now eight-digit tags, so a translucent colour reads there too), the nameplates/arrows and the location pings. They read a `PlayerColorValue`, which is what the free path produces |
| The identity and wire path | Unchanged shape: `SetLocalPlayerColor(null)` still means automatic, `ReportLocalPlayerColor` still announces the same nullable colour. No message, field or protocol number moved (stays 43) |
| The configuration and profile path | The entry is renamed and retyped in one place; the profile store is generic over the same `ConfigFile`, so the carry needed no code. Both language variants of the reference table moved in the same change |
| The pins around the surface | Re-anchored where the contract moved, untouched where it did not: `OnlineUiWindowSurfacePinTests` (one matcher + its message), `OnlineUiColorPickerPinTests` (new), `OnlineUiConsolePageRemovalPinTests` (green as is), `OnlineUiAdapterCapabilityPortShape`/`AdapterCapabilityPortShapeTests` (no port added), `OnlineUiNativeHostPinTests` (the S1 probe, untouched) |
| The still-IMGUI surfaces | Untouched: quick panel, in-world context menu, console overlay, network HUD |
| Mod UI windows, console, chat | Untouched |
| Wire, protocol, save, gameplay | None: no `NetMsg`, no save field, no protocol change |

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | The codec reads exactly the two canonical forms and refuses everything else | `PlayerColorValueTests` 21 cases (6 facts + 15 refusal rows): six round trips through the colour's own text, case/space tolerance, the shorthand refusal, and the refused shapes (null, empty, wrong digit counts, bad digits, a name, `0x…`) |
| 2 | A palette colour is still nameable after the round trip through the config | `PlayerColorResolverTests.NoTwoPaletteEntries_ShareTheirHexText` (the text is what the picker matches on) + `EveryPaletteColor_RoundTripsThroughItsOwnText` |
| 3 | Every palette name reaches both label tables | `PlayerColorResolverTests.EveryPaletteName_HasItsCatalogueLabel`, reading `LocalizationCatalog.English`/`Chinese` for `prefs.color.<name>` — the interpolated key space no gate can enumerate |
| 4 | The stored preference is the colour itself, and the index path is gone | `OnlineUiColorPickerPinTests.TheStoredPreferenceIsTheColourItself` (a `ConfigEntry<string>`, `SetColor(PlayerColorValue?)`, and no `ConfigEntry<int>`/`ColorIndex` anywhere in the editor) + its mutation row |
| 5 | The field stores only a colour it parsed, and says so while it has not | Same class: `TheFieldCommitsOnlyAColourItParsed` + its mutation row (the mutation stores whatever was typed) |
| 6 | The palette grid is the Runtime's palette, not a list of its own | Same class: `ThePaletteIsTheRuntimesOwn` + its mutation row |
| 7 | Automatic is the absent colour | Same class: `AutoIsTheAbsentColour` + its mutation row (the mutation picks a palette entry instead) |
| 8 | The live block is built from the colour the player carries now | Same class: `TheLiveSwatchShowsTheColourThePlayerCarries` + its mutation row |
| 9 | A colour block is the game's own row with the colour laid over it | Same class: `AColourBlockIsTheGamesOwnRowTinted` (the button-row prefab, the swatch image, the fill write) + its mutation row |
| 10 | A block the model gives no id reports nothing | Same class: `AnElementWithNoIdReportsNothing` + its mutation row; the window family's `EveryInteractiveControlReportsItsOwnId` matches the same guard on the button listener |
| 11 | The preference rides the configuration profiles | the store is generic over the same `ConfigFile` (`ConfigurationProfileStore.SaveCurrent_ThenApply_RestoresAllBoundEntries` already proves a string entry round-trips) and pin 4 holds that the colour is such an entry in that file. No profile code changed |
| 12 | Every place the colour is shown carries all four of its channels | `OnlineUiColorPickerPinTests.EveryColourTagCarriesAllFourChannels` + its mutation row (the two text tags write the eight-digit form), and the three world overlays tint with the alpha (`OnlineUiOverlay.ToColor`, `LocationPingOverlay.ToColor`); the picker's own preview block is built from the same `PlayerColorValue` |
| 13 | The rest of the page family is unaffected | `OnlineUiConsolePageRemovalPinTests` (21 cases: the tab row, the six-case dispatch, the `tab.`/`console.` census) and `AdapterCapabilityPortShapeTests` (20 cases, 14 ports / 19 members) green with no change |
| 14 | The one unreadable stored value is observable | `OnlineUiHost`'s constructor logs `the stored player colour … is not a colour — expected #RRGGBB or #RRGGBBAA` at warning level once, and the picker then shows the automatic colour |
| 15 | The rest of the surface is unaffected by the control view's split | `OnlineUiWindowSurfacePinTests` (14 pins + 16 mutation rows) green with the two prefab-file pins re-pointed at `OnlineUiControlFactory.cs`; the launcher's contract, the wrap rule, the applied-writes rule and the intent dispatch untouched |
| 16 | A half-typed edit does not outlive the window it was typed in | Same pin class: `TheColourEditDoesNotOutliveTheWindow` (the window's closed branch clears `PlayerColorInput`, which is where the edit lives) + its mutation row |

## 5. Red, the ladder and the numbers

- **The red for the picker's pins.** Every S3 pin is false on the pre-change source: all eight mutation
  anchors (`SetColor(PlayerColorValue?)`, the codec call in the field's handler, `PaletteValues` in the
  grid, `Choose(ctx, null)`, the carried-colour read, the colour block's prefab case, the id guard on the
  button listener, and the edit reset on the window's closed branch) are ABSENT from those files at HEAD
  (`git show HEAD:<path>`), so on that tree each pin's matcher is false and each mutation cannot even
  anchor — the pin set fails loudly rather than vacuously. The drawer's dropdown call sites are the
  witness in the same direction: three at HEAD (log level, language, colour), two now.
- **The ladder** (re-run after the review's findings landed; these are the post-fix numbers). build 0
  warnings / 0 errors (`%TEMP%/cuo-s3-build5.txt`); the focused filter
  `FullyQualifiedName~OnlineUi|~AdapterCapabilityPortShape|~PlayerColor|~ConfigurationProfile` 285/285
  (`cuo-s3-focus4.txt`); `dotnet format` exit 0 (`cuo-s3-format3.txt`); the full suite WITH build
  4286 + 287 exit 0 (`cuo-s3-full4.txt`); the normative gates 288/288 (`cuo-s3-gates6.txt`). The full
  evidence run carries the documented `FullyQualifiedName!~DeliveryChecklist` filter, and the gate run
  that followed the checklist's last box is what shows 288/288 — the case is excluded from that run, not
  waived inside it.
- **The size review.** Largest touched sources: `OnlineUiOverlay.cs` 530, `OnlineUiWindowView.cs` 505,
  `OnlineUiControlView.cs` 476, `OnlineUiHost.cs` 448, `LocalizationCatalog.cs` 419 — the colour block had
  pushed the control view to 613, which is why the prefab half moved into `OnlineUiControlFactory.cs`
  (203 lines) in the same round instead of the doc comments being trimmed to fit.
- **Dead mechanisms deleted, not parked.** `[UI] PlayerColorIndex` and its range validator,
  `PlayerColorConfigEditor.ColorIndex` / `SetColorIndex`, `PlayerColorResolver.TryGet`, the drawer's
  `ColorKeys` array and its colour dropdown, the `Action<int>` colour delegate; and the control view's
  `UsesGamePrefab` was replaced by the narrower `MissedGamePrefab` its only consumer needed.

## 6. Independent adversarial review and dispositions

The review ran in a fresh context against the frozen working tree before the commit. Its own context
carried read-only tools, so it verified by reading every cited file and could NOT execute the ladder — the
numbers in §5 are the parent's own runs, in the logged files §5 names. Findings, and what happened to
each:

| # | Severity | Finding | Disposition |
|---|---|---|---|
| M1 | major | the hex field's own label is dropped: the row's id-less label and the text field would share one slot, and the label would never be built | NOT a defect, and the reconciler is what refutes it: `OnlineUiWindowView.Matches` requires the SAME KIND before it reuses a view, so a `Label` slot can only ever be re-taken by a `Label` — a text field cannot land in it — and the element's text is non-empty (`ctx.T("prefs.player_color_hex")`), which `OnlineUiControlView.ApplyText`'s Label branch writes. The row's shape is S2b's own (`prefs.profile_name` is built exactly the same way) |
| M2 | major | "every one of them already alpha-aware" is false for two of the five consumers: the member list and the Home page render the colour through `ColorUtility.ToHtmlStringRGB`, which drops the alpha | landed: both sites now write the eight-digit `ToHtmlStringRGBA` tag — the form the Admin and Preferences status lines already used — so a translucent colour reads the same in the lists as it does in the world; §1 row 7, §2 and §3 say exactly that instead of claiming it was already so, and §4 row 12 pins it |
| m1 | minor | the "not a colour yet" line survived closing and reopening the window | landed: the complaint is no longer a sticky boolean but a fact derived from the field's text (`PlayerColorInputIsInvalid`), so it cannot outlive the text that caused it |
| m2 | minor | after a blur the field could keep text the model did not know about — the model carried the stored colour, so nothing ever rewrote the box | landed: the window state carries the field's text (`PlayerColorInput`, null = the stored colour), the model carries it back so box and model agree while the page is open, and the window's closed branch drops it so a half-typed edit does not come back with the window. Pin `TheColourEditDoesNotOutliveTheWindow` + its mutation row |
| m3 | minor | the checklist's "§4 (13 rows)" and its enumeration did not add up with the table | landed: the table is 16 rows and the checklist's item 3 and item 4 evidence were re-anchored to what they count |
| m4 | minor | the codec suite was described as 16 cases where 6 facts + 15 refusal rows are 21 | landed: §4 row 1 says 21 cases |
| n1 | nit | the self-check had no §6 | landed: this section |
| n2 | nit | §5's checklist-gate sentence read as if a failing case had been waived | landed: §5 now says the case is EXCLUDED from the evidence run by the documented filter and confirmed by the 288/288 gate run that followed the checklist's last box |
| n3 | nit | two older evidence records still describe the deleted index mechanism | landed in part: the MANIFEST row for `players/player-color-and-head-tags-selfcheck.md` now says its colour half is superseded by S3 while its head-tag half holds. The records' own bodies stay as written — they are dated records of their cycles, and the manifest is the index that has to be true |
| n4 | nit | `AdapterCapabilityPortShapeTests` carries the Integration trait, so the fast ladder could not be trusted for the port count, and its "20 cases" did not decompose | verified by running it: the focused filter names the class explicitly (so the trait does not exclude it) and it reports 20 cases with 14 ports / 19 members; `OnlineUiColorPickerPinTests` reports 18 (9 pins + 9 mutation rows) |

## 7. Limits — what this cycle does not prove

- **How a block reads is the user's run.** The block is the game's own button row tinted, so the sprite's
  own colour multiplies the fill: whether eight of them read as a palette at the player's canvas scale,
  and whether the tints are recognisable against the window's dark frame, is a game observation.
- **The field is the game's integer row reused.** Its content type and character limit are set on the
  instance, but whether it takes the typing, where the caret sits, and how the blur-snap back to the
  stored value feels are game observations.
- **A completed entry is one commit per entry.** A value that is still being typed is never stored (the
  three-digit shorthand cannot parse), and an entry that parses is stored and announced at once; whether
  that reads as "live" rather than as a missing Apply control is the user's judgement.
- **The S1 chrome reading is still pending**, so the picker's tints come from the CUO theme rather than
  from the game's own chrome colours.
- **Alpha is carried everywhere but proven nowhere yet.** `#RRGGBBAA` parses, every consumer now passes all
  four channels on (three tint their own graphics, two write an eight-digit tag), but whether a translucent
  marker or a translucent name reads well — and whether the game's TMP renders partial alpha inside a
  `<color=#RRGGBBAA>` tag — is the user's judgement.
- **The window still needs the adapter** (no canvas, no picker) — the trade S2a recorded, and the reason
  S4 owns the remaining IMGUI surfaces.
- **The palette's eight blocks share one row by arithmetic, not by observation.** Eight 88-unit blocks with
  4-unit gaps sum to 732 against the window's 764-unit content width, and the Runtime's wrap rule is what
  would move them onto a second line if that changed; what the game's own row prefab really measures is an
  S1 fact that has not been read yet.
