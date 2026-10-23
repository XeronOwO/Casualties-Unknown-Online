# Command refusals render as successes

- Status: Todo — found by the second review of the `/gamerule` change (2026-10-24); pre-existing and
  console-wide, with the new verb's refusals as the instance.
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
