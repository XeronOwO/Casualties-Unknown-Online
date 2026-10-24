# Command refusals render as successes

- Status: Done — the user approved the typed-answer contract (2026-10-24) and it is landed: `ConsoleAnswer` in
  `Abstractions`, every built-in and bundled mod migrated, two tests pinning the KIND, and machine readings for
  `/gamerule`, `/help` and `/data` (deployed `0.1.0+01ea0eec`). Found by the second review of the `/gamerule`
  change; pre-existing and console-wide, with that verb's refusals as the instance.
- Priority: Low-Medium
- Category: Console / output kinds / player feedback
- Source: the review read `CommandConsoleService.ExecuteCommand`: every non-empty handler return is added as
  `ConsoleLineKind.Success`, and `CommandConsoleOverlay` maps Success to `OnlineUiTheme.Positive`. The
  `/gamerule` refusals (unknown rule, invalid value, out of range, the editor's refusal) are returned
  strings, so they render green. Since the verb joined the op family (2026-10-24) its PERMISSION refusal is
  a handler return too, so a member the host did not grant now reads it in the positive colour as well —
  the pre-handler host-only line that used to colour that case as an error no longer applies to this verb.
  `/help <hidden>` is a second new instance (2026-10-24): its permission answer is a handler return, so a
  hidden command's refusal is green too. The verb's tests assert the text because the handler has no way to
  return a kind.
- Related: `done/gamerule-host-rule-command.md`, the console's own record
  (`done/in-game-command-console-interactive.md`, `docs/evidence/selfchecks/ui/command-console-selfcheck.md`),
  and the mod-facing console command (`ModConsoleCommand.Handler` returns `string?`).

## Problem

A command handler's signature carries text, not a verdict. Every verb that answers a refusal on a normal
return — `/gamerule` (its permission refusal included, since the verb became `Anyone` + gated), and before
it `/hostrules`, `/kick`'s unknown member, an item verb's "this client holds no such item" — is printed with
the same `Success` kind as a completed action, so the player reads a red-worthy refusal in the positive
colour. Only a refusal the console itself makes before the handler (a host-only gate, an unknown command) is
an error line. The `/gamerule` tests pin the refusal text and cannot pin the kind, because there is no kind
to pin.

## The question

How does a handler signal a refusal, without inventing a second registration API for mods?

1. **A typed result** — the handler returns the text plus a kind (a small record), and the console adds the
   line with it. Every built-in and mod handler signature changes, and `ModConsoleCommand.Handler` (public
   Abstractions) changes with it; that is a contract decision, not a console detail.
2. **A console-side convention** — the handler keeps returning `string?`, and the command declares which of
   its answers are refusals (a predicate, a prefix, or a per-command "error prefix" list). Smaller, but a
   convention the next verb can forget.
3. **A per-command kind table** — each `[ConsoleCommand]` declares the kinds its handler can answer; the
   console maps the return to the declared kind by position. Too coarse for a verb with several answers.

The answer must cover the mod-facing shape (option 1 touches `Abstractions`, so the modification policy
applies) and the console's own tests: the accepted shape has to let a test assert the KIND of a refusal, not
just its text.

## Acceptance

- A refused `/gamerule` (the cheat gate's permission sentence, unknown rule, invalid value, out of range, the
  editor's refusal) and a refused `/help <hidden command>` render as error lines; a successful set/query and
  a visible command's help render as success lines, read from the console's own output.
- The chosen mechanism covers mod console commands and is recorded in the console selfcheck; a test pins the
  kind of a refusal and of a success for at least one built-in and one mod command.
- No verb is left with a refusal that still renders as success.

## Decision (user, 2026-10-24): option 1, the typed result

The user approved the contract change and the recommendation: a `ConsoleAnswer` (text + `ConsoleAnswerKind`) that
every handler returns, built-ins and mods together.

## Landed (2026-10-24, deployed `0.1.0+cd826ca0`)

- `Abstractions` gains `ConsoleAnswer` (a plain struct — net48 has no `IsExternalInit`, so not a record) and
  `ConsoleAnswerKind`; `ModConsoleCommand.Handler` returns it; the registry REFUSES a built-in handler that does not
  (the reflection check is what makes the shape unforgeable); `CommandConsoleService` colours the line by the kind.
- Every built-in handler migrated: `/gamemode`, `/spectate`, `/gamerule` (its permission refusal included), `/heal`,
  `/give`, `/summon`, `/save`, `/op`, `/deop`, `/kick`, `/ban`, `/unban`, `/traderreport`, `/help`, `/clear`,
  `/players`, `/rtt`, `/whoami`; the three bundled mods and the test mods follow.
- The `Abstractions` baseline records the two new types, the replaced constructor and its tombstone.
- Two tests pin the KIND: `/gamemode`'s unknown mode is an error line and its successful switch a success line; a
  mod command that answers `Refused` prints an error line.
- Machine reading, one session: `[probe] /gamerule nonsense` → line kind **Error** ("Unknown game rule 'nonsense'");
  `/help` → kind **Success**.

## /data landed too (2026-10-24, deployed `0.1.0+01ea0eec`)

The item-data family was the last verb. `ItemDataCommands`' read path (`Get`, `ReadOne`, `ReadCursor`, `ReadSlot`,
`ReadBodies`) and `ItemDataWriteCommands`' write path (`Merge`, `Modify`, `Remove`, `Write`) now answer
`ConsoleAnswer`, with the refusal kind on: a read that finds no item, a selector naming several bodies, a path that
does not resolve, the no-language build, and every write refusal (the cheat gate, an unresolved target, an item this
client does not hold, the local scene's own refusal). The renderers answer `string?` where null is the missing
language, which each caller turns into that same refusal.

Machine reading, one session: `/data get id 999999999` → line kind **Error** ("No item #999999999 is recorded in this
session."); `/data modify nope` → **Error** ("'nope' is not a target…"). With the earlier `/gamerule nonsense` →
Error and `/help` → Success, every verb the ticket names answers with its own kind.

## Acceptance judged

- **A refused command renders as an error line, a successful one as a success line** — read for `/gamerule` (refused
  and successful), `/help` (success), `/data` (two refusals), and pinned by tests for `/gamemode` and a mod command.
- **The mechanism covers mod commands and the selfcheck records it** — `ModConsoleCommand.Handler` returns
  `ConsoleAnswer`; the console colours by `ConsoleAnswerKind`; the two tests assert the kind, not the text.
- **No verb is left with a refusal printed as success** — the built-ins, the three bundled mods and the test mods are
  migrated; the registry refuses a handler whose signature cannot answer with a kind.

