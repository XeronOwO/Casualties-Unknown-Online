# The Online UI's art and controls are placeholders

- Status: Todo
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

## Open design questions (the user's call, to be asked with the research in hand)

- How far the restyle goes: every Online UI surface, or the window family first.
- The colour input's form: a palette plus an RGB/hex field, or a native-style picker.
- What "the game's style" means on the surfaces the game itself has no equivalent for.

## Non-goals

- Not a mod-facing UI API and not a new window framework for other mods' windows.
- Not a change to any wire, save, session or gameplay behaviour: this is presentation and interaction.
