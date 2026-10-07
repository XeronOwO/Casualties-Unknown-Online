# The intro cover: who skips it, and when

- Status: Todo
- Priority: Low
- Category: Presentation / join flow
- Source: the user's 2026-10-07 backlog request. Reported behaviour: launching the game through Steam's
  "join a friend" action skips the cover (the content-warning/intro screen), and after returning to the main
  menu in the same process the cover is still skipped. The user wants a clean rule instead: either the cover
  is skipped on the first launch into the main menu, or — their preferred variant — the skip becomes a local
  multiplayer preference that permanently skips the cover when it is on, while a launch through the
  friend-join action still shows the cover. "Joining a friend" here is one of this player's own launch paths,
  never an instruction about what another player's screen shows (the user restated this on 2026-10-07).
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

The decision is **local and one-sided**: it is this player's own preference and it decides what this player
sees on this machine. Nothing about it is carried to, shown to, or enforced on anyone else — "joining a
friend" is one of this player's own launch paths (the game started through Steam's invite/join action), not a
second party the rule acts upon. The user stated this explicitly on 2026-10-07, because an earlier draft of
this ticket read "friend join" as "the friend who joins" and invented a confirmation that does not exist.

Two candidate shapes were given, and the user prefers the second:

1. Skip the cover on the first launch into the main menu (the cover is not shown to this installation's player
   at all), and leave the launch-path handling as it is today.
2. Make it a **local multiplayer preference**: with it on, the cover is skipped permanently; with it off,
   nothing skips it automatically. The user adds one clause for the join path: launching the game through the
   friend-join action still shows the cover, i.e. the preference governs the ordinary launch into the main
   menu, and the join path keeps its own behaviour instead of inheriting the preference.

Either shape also removes today's defect: the skip currently rides a process-wide flag that a join sets and
nothing clears, so the cover stays gone for the rest of the process.

## Required work

1. Replace the process-wide static with an explicit, named decision that says what is skipping and why, so
   "skipped once" and "skipped always" cannot be confused by the next reader. The shape is already wanted by
   `review/plugin-host-shell.md` (the intent on a port, not a static write from the shell).
2. Put the choice on the configuration surface, and make every launch path read that same decision instead of
   a second flag, with the join path's own rule (show the cover) stated where the decision is made. The
   preference's default is not stated in so many words: "turn it on to skip permanently" reads as off by
   default, while the first candidate shape (the cover skipped on the first launch) reads as on. The cycle
   settles that one word when it starts, rather than assuming either.
3. Verify all four local paths in one session: an ordinary launch into the main menu, a launch through the
   friend-join action, a return to the main menu after playing, and a relaunch — each reads whether the cover
   appeared on this machine, with the preference on and off.

## Non-goals

- Not a redesign of the intro screen itself, and not a removal of the content warning for a player who never
  joins a session: the switch decides, and the default decides for them.
- Not a change to how a session is joined: this ticket only owns the cover's visibility.
