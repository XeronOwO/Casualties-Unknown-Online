# The `/heal` console command duplicates the game's own heal

- Status: Todo
- Priority: Medium
- Category: Commands / player interaction
- Source: the user's correction of 2026-10-18, after batch `20261018-t`: the `/heal` command was NOT designed
  as "a conscious player uses a carried medical item on another player". The heal the user asked for is the
  GAME'S OWN `heal` console command, item-free and on the local body; the two heal tickets carry that work.
- Related: `review/heal-command-limb-and-hollow-restore.md` (the surface decision: the vanilla `heal`
  command, item-free), `review/heal-restores-severed-limbs.md` (the limb half on the same surface),
  `done/in-game-command-console-interactive.md` (the console redo that added `/heal`),
  `docs/evidence/acceptance/20261018-t-scope.md` (the run that read the duplicate's behaviour)

## What was invented, and where it is stated

The console redo added `/heal <selector>` to CUO's own console and wired it to
`PlayerHealService.SendHealRequest` — the remote-medical item path:

- `CommandConsoleService` registers it with the help text "Use a carried medical item on the selected
  player(s)." and calls `_playerInteraction.SendHealRequest(steamId)` for every target;
- `PlayerHealService`'s own summary calls itself "the cross-player heal operation": the healer must be
  conscious/alive, must carry a medical item, and the target must be another in-world player (`healer ==
  target` returns silently).

**That description is the implementation's own claim, not the user's design.** The user's 2026-10-07 request
names the game's own `heal` command ("Fully heals the player character", item-free, writes
`PlayerCamera.main.body`), which is exactly the surface
`review/heal-command-limb-and-hollow-restore.md` settles and `ConsoleScriptHealHollowPatch` extends with
severed limbs and hollow. The item-based cross-player operation is the remote-medical UI's action
(`OnlineUiActions`), a separate family; it never needed a console command named `heal`.

## What the run showed

Batch `20261018-t` used CUO's `/heal` as the prelude heal and read only the answer, not the outcome. The
host's logs (verbatim in `docs/evidence/acceptance/20261018-t-scope.md`):

- `/heal @a` and `/heal @p` (the member): `[Heal] refused: <host> is not conscious/alive and cannot heal.`
  while the command answered `Sent heal request to 1 player(s)`;
- `/heal @s` (the host): no `[Heal]` line at all — the self-target guard returns silently — while the command
  answered `Sent heal request to 1 player(s)`.

So the duplicate command both answers a success it never achieved and cannot heal the one body a host most
often wants to reset: its own. That behaviour is a consequence of the wrong design, not a branch to patch in
place.

## What done looks like

1. `/heal` is REMOVED from CUO's console (the command registration, its help text and the console tests that
   name it). CUO's console no longer offers a second heal.
2. The heal surface is the game's own `heal` console command — item-free, on the local body, with CUO's
   limb/hollow extension — and its remaining acceptance rows stay with the two heal tickets.
3. The item-based cross-player treatment keeps its home in the Online UI's remote-medical actions
   (`PlayerHealService` through `OnlineUiActions`), not behind a console command.
4. A pin: CUO's command table contains no `heal`; the native console's `heal` is untouched; the remote-medical
   item path's tests stay green.
5. The `done/in-game-command-console-interactive.md` claim that "`/heal <selector>` resolves player selectors
   and sends a host-authoritative heal request" is recorded as superseded by this ticket in the ticket that
   removes it (the done ticket is a point-in-time record; it is not rewritten).

## Non-goals

- Not a rework of the remote-medical item path; it is the UI's action and keeps its behaviour.
- Not the game's own heal's limb/hollow extension (those are the two heal tickets' subject).
- Not CPR, and not a new selector-based admin heal: if a player-selector heal is wanted later, it is its own
  ticket, designed from the user's words rather than from this command's help text.

## The process lesson this carries

The design was read from the implementation's own doc comment and help string, which the previous agent
cycle wrote. A help text is a claim the run verifies, never the requirement; the requirement is the user's
ticket or the decision record. `docs/acceptance/lessons.md` carries the lesson for the next acceptance run.

## Evidence the fix starts from

- The command: `CommandConsoleService`'s `[ConsoleCommand("heal", …)]` method and its unconditional
  `Sent heal request to N player(s)` answer.
- The service: `PlayerHealService`'s cross-player summary and its `healer == target` silent return.
- The native surface: `ConsoleScript`'s `heal` command ("Fully heals the player character"), and
  `ConsoleScriptHealHollowPatch` (CUO's limb/hollow extension).
- The run: the four log lines in `docs/evidence/acceptance/20261018-t-scope.md`.
