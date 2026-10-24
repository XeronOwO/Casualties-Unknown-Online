# The intro cover: who skips it, and when

- Status: Review — one named decision with the player's own live preference (default off) and the join rule
  stated in it (2026-10-24, deployed `0.1.0+c7a2f1fb`), and all four launch paths plus the join row read from a
  single deployed artifact (the batch section below). No code is left.
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

## Decision (user, 2026-10-24): default off, and a simulated join call counts

The default is **off** (a fresh install keeps the content warning), and the friend-join row is judged from a
**simulated intent call** — the shell's own port method driven in-process — with the record saying so.

## Landed (2026-10-24, deployed `0.1.0+c7a2f1fb`)

- `IntroCoverDecision` replaces the process-wide skip flag: it weighs the player's live preference against the join
  launch, answers its reason in one sentence, and the patch writes the game's own `didIntro` only when the decision
  says so. It is a named object the patch reads (`GameAdapter.IntroCover`), not a bare bool.
- The preference is `[Session] SkipIntroCover`, default `false`, read LIVE through
  `IJoinFlowPresentation.ConfigureIntroCoverPreference(Func<bool>)`, so a config edit applies to the next launch
  without a second flag. The join path calls `PrepareForDirectJoin()`, which now only NOTES the launch instead of
  forcing a skip.
- Both configuration pages carry the row (plus the two spectator rows that were missing), and five cases pin the
  four combinations, the live read, the unwired default and the join rule.

## Batch `20261024-in` — the four launch paths, read from one deployed artifact

Every row is the patch's own line at `PreRunScript.Start` (a low-frequency edge), read from the host log:

1. **Ordinary launch, preference off (default)** — `[Intro] cover left to the game: the local preference
   (Session/SkipIntroCover) is off`. The content warning is the game's own.
2. **Ordinary launch, preference on** — after flipping the config entry and relaunching:
   `[Intro] cover skipped: the local preference is on and this is an ordinary launch`.
3. **Return to the main menu after playing, then a new run** — the decision re-evaluates at each run start and
   printed the same `cover skipped` line again (a second occurrence in the log), so the rule is per launch, not a
   one-shot flag.
4. **Relaunch** — reading 2 came from a fresh process after a config edit, which is that row.
5. **A launch through Steam's join action (SIMULATED, per the user's acceptance)** — the probe called the shell's own
   port method (`IJoinFlowPresentation.PrepareForDirectJoin()`, answering `noted: true`), then a run start read
   `[Intro] cover left to the game: this launch came from the join action, which never skips the cover`, with the
   preference still ON in that process — so the join clause overrides the preference, which is the row's claim.

The patch's wording is deliberate: it reports the ACTION it took ("skipped" / "left to the game"), never that the
cover appeared, because the game's own `didIntro` is sticky for a process and an earlier skip can keep the cover
hidden after the preference is turned off mid-session.
