# `/heal` answers `Sent` for requests it silently drops

- Status: Todo
- Priority: Medium
- Category: Commands / player interaction
- Source: batch `20261018-t`'s heal step, and the user's question about it (2026-10-18). The run's `/heal @s`
  answered `Sent heal request to 1 player(s)` and nothing happened; the log has no `[Heal]` line for it.
- Related: `review/heal-command-limb-and-hollow-restore.md` (the game's own `heal` command's limb/hollow
  extension — the LOCAL reset, a different surface), `review/heal-restores-severed-limbs.md`,
  `done/in-game-command-console-interactive.md` (the console the command lives in),
  `docs/evidence/acceptance/20261018-t-scope.md` (the run's heal timeline)

## What was observed

Batch `20261018-t`'s prelude (after the long UI work) tried to restore the host with CUO's own `/heal`:

```text
[2026-10-18 11:20:01.445] [INF] [..PlayerInteractionService] [Heal] refused: <host> is not conscious/alive and cannot heal.
[2026-10-18 11:20:01.445] [INF] [..CommandConsoleService] [Command] /heal resolved @a to 1 player(s): <member>.
[2026-10-18 11:20:09.666] [INF] [..CommandConsoleService] [Command] /heal resolved @s to 1 player(s): <host>.
[2026-10-18 11:20:11.978] [INF] [..PlayerInteractionService] [Heal] refused: <host> is not conscious/alive and cannot heal.
[2026-10-18 11:20:11.978] [INF] [..CommandConsoleService] [Command] /heal resolved @p to 1 player(s): <member>.
```

- `/heal @a` (the member): the host-side handler refused because the HEALER (the host) was not conscious/alive;
  the command still answered `Sent heal request to 1 player(s)`.
- `/heal @s` (the host itself): no `[Heal]` line at all — the request is dropped before any check.
- `/heal @p` (nearest, the member): refused again, same answer.

The heal attempt was also too late: the host had already been left standing for the long UI/console work and
was unconscious when the heals ran; an unconscious healer is refused by design.

## The gap, from the code

- `CommandConsoleService.Heal` (the `[ConsoleCommand("heal", ...)]` method) resolves the selector, calls
  `SendHealRequest(steamId)` for every target, and then ALWAYS returns
  `Sent heal request to N player(s): …` — it never reads a synchronous refusal.
- `PlayerHealService.SendHealRequest` routes the HOST's own request through
  `HandleHealRequest(_session.LocalSteamId, msg)`; with a target that IS the host, `HandleHealRequest` hits
  `if (healer == target || healer == 0 || target == 0) { return; }` — a silent return, before the
  conscious/alive, item and limb checks. So a host `/heal @s` (or `@p` when it picks the host) can never do
  anything, and the console still says it was sent.
- The other synchronous refusals (`healer` not conscious/alive, no usable medical item, target not in
  world/limb) are logged at Information/Warning but never reach the command's answer. Only the guest's
  request travels the wire, where the kernel result is the answer; the host's own path has a synchronous
  result available and throws it away.

## What done looks like

1. A selector that resolves to the LOCAL player is refused by name: the answer says this command heals
   ANOTHER player, and names the local reset (the game's own `heal` console command) instead of `Sent`.
2. Every synchronous host-side refusal (not conscious/alive, no medical item, target not in world, no
   healable limb) is reflected in the command's answer, and the run's log line stays as the audit half.
3. The cross-player item heal still works for a conscious healer and another player, and the host's own
   reset stays the game's own `heal` command (the one `ConsoleScriptHealHollowPatch` extends), not a new
   local path in `/heal`.
4. Red tests: the self-target branch, the unconscious-healer branch, the no-item branch — each asserting the
   answer, not only the log.

## Non-goals

- Not turning `/heal` into a local full-reset command: the game's own `heal` command is that surface.
- Not CPR, and not a change to the heal profiles, the item consumption or the wire.
- Not the acceptance prelude itself: that rule (heal early, use the game's own `heal`, read the vitals back)
  is in `docs/acceptance/workflow.md` §5.

## Evidence the fix starts from

- The command: `CommandConsoleService.Heal` — the `<selector>` resolution and the unconditional answer.
- The service: `PlayerHealService.HandleHealRequest` — the `healer == target` silent return, and the
  conscious/alive check after it.
- The run: the four lines above, in `docs/evidence/acceptance/20261018-t-scope.md`'s heal paragraph.
