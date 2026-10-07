# The intro cover: who skips it, and when

- Status: Todo
- Priority: Low
- Category: Presentation / join flow
- Source: the user's 2026-10-07 backlog request. Reported behaviour: launching the game through Steam's
  "join a friend" action skips the cover (the content-warning/intro screen), and after returning to the main
  menu in the same process the cover is still skipped. The user wants a clean rule instead: either the cover
  is skipped on the first launch into the main menu, or — their preferred variant — the skip becomes a
  multiplayer preference that permanently skips the cover when enabled, with a friend join still showing the
  cover while the preference is on.
- Related: `src/CasualtiesUnknownOnline.GameAdapter/Patches/PreRunScriptIntroSkipPatch.cs` (the patch that
  sets the native `didIntro` field), `src/CasualtiesUnknownOnline.Runtime/GameAdapter/IJoinFlowPresentation.cs`
  (the intent the shell declares), `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs` (the
  `_skipIntro` static behind it), `review/plugin-host-shell.md` (the stage that wants that intent on a port),
  `docs/en/reference/configuration.md`

## What is observed

- The join path declares the intent once: `IJoinFlowPresentation.PrepareForDirectJoin()` sets a process-wide
  static (`_skipIntro`), which `PreRunScriptIntroSkipPatch` reads when `PreRunScript.Start` runs, setting the
  game's own `didIntro` field so the content-warning/intro screen does not appear.
- Nothing resets that static — there is no clear on leaving the world or on returning to the main menu — so
  the reason the cover stays skipped after the first join is the process-lifetime flag, which the ticket
  records as the attributed cause rather than a guess.

## What is asked

One rule, stated once, with no surprise for a joining friend. Two candidate shapes were given, and the user
prefers the second:

1. Skip the cover on the first launch into the main menu (i.e. the cover is not shown to this installation's
   player at all), and keep the join-path skip as it is today.
2. Make it a multiplayer preference: while it is on, the cover is skipped permanently; a friend joining the
   session still sees the cover. One confirmation is owed in the cycle before implementing: whether "still
   sees the cover" means the joining player always does, or only when that player has not enabled the
   preference themselves.

## Required work

1. Replace the process-wide static with an explicit, named decision that says who is skipping and why, so
   "skipped once" and "skipped always" cannot be confused by the next reader. The shape is already wanted by
   `review/plugin-host-shell.md` (the intent on a port, not a static write from the shell).
2. Put the choice on the configuration surface with the default the user picks, and make the join path's
   behaviour read the same decision rather than a second flag.
3. Verify all four paths in one session: first launch into the main menu, a Steam friend join, a return to
   the main menu after playing, and a relaunch — each reads whether the cover appeared.

## Non-goals

- Not a redesign of the intro screen itself, and not a removal of the content warning for a player who never
  joins a session: the switch decides, and the default decides for them.
- Not a change to how a session is joined: this ticket only owns the cover's visibility.
