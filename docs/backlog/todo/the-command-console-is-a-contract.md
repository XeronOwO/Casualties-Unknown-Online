# The command console is a contract, not a set of attribute-marked methods

- Status: Todo — opened 2026-10-27 from the user's direction: "the command system probably needs a proper design; hand the interface down to mod
  authors, split local and non-local commands, delegate per-parameter completion (with presets), let completion depend on the context, and treat it as
  a ceiling on what the mod can do".
- Priority: High — this is the mod-facing surface; its shape is hard to change once third-party mods depend on it.
- Category: Console / mod contract / architecture

## What exists today (read, not remembered)

| Piece | Where | What it can express |
|---|---|---|
| built-in commands | `[ConsoleCommand(name, description, permission, usage, CommandArgumentKind)]` on methods of `CommandConsoleService`, collected by `ConsoleCommandRegistry` through reflection | a flat argument-kind list; no custom completion, no cross-argument context, no per-command state |
| mod commands | `IModConsoleCommands.Register(ModConsoleCommand)` (`Abstractions`) | LOCAL ONLY by design ("never travels over the network"), `CommandPermission`, a flat `CommandArgumentKind` list, `Func<IModConsoleCommandContext, ConsoleAnswer>` |
| completion | `CommandArgumentKind` → source switch in `CommandConsoleCompletion`; `CommandSuggestion` carries a `Hint` kind the overlay localizes | the framework's ten kinds; a mod cannot add an eleventh, cannot offer a fixed word set, cannot make argument N depend on N-1 |
| answers | `ConsoleAnswer` (text + optional catalogue key + args) | a mod's sentence travels as TEXT: the receiver's catalogue has no entry for it |

So the attribute is fine for a closed set of one-line commands, and it is exactly what does NOT scale to mod authors: the four things a mod needs
(custom completion, presets, context, non-local execution) all have no place in a method attribute or in the `CommandArgumentKind` enum.

## The shape this ticket proposes

**One definition contract, in `Abstractions`, that BOTH sides implement.** Built-ins stop being a special case: the attribute stays as SUGAR that
produces a definition, and the registry, the dispatcher and the completion speak only the definition.

```
public interface ICommandDefinition
{
    string Name { get; }
    IReadOnlyList<string> Aliases { get; }
    string Description { get; }            // the log/fallback sentence
    string? DescriptionKey { get; }        // the reader's catalogue, when the mod ships one
    string Usage { get; }
    CommandPermission Permission { get; }
    CommandScope Scope { get; }            // Local (this process) | HostRelayed (runs where the authority is)
    IReadOnlyList<ICommandParameter> Parameters { get; }
    ConsoleAnswer Invoke(ICommandInvocation invocation);
}

public interface ICommandParameter
{
    string Name { get; }
    IArgumentCompleter Completer { get; }  // presets, context, or the framework's own kinds
    IArgumentParser? Parser { get; }       // optional: typed values for the handler
    string? HintKey { get; }               // the grey explanation the completion row shows
}

public interface IArgumentCompleter
{
    // Previous = the arguments already accepted in this line (context), Partial = the token being typed.
    IReadOnlyList<CommandCompletion> Complete(ICompletionContext context);
}
```

Shipped completers a mod composes with (and the built-ins migrate onto):

- `OneOf("on", "off")` — the fixed word set the user asked for;
- `PlayerNames`, `SteamIds`, `BannedSteamIds`, `ResourceLocations`, `Selector`, `JsonTemplate`, `CommandNames` — the ten kinds, as objects;
- `Delegate(ctx => …)` — a mod's own list, with the previous arguments in hand, which is what makes argument N depend on N-1;
- `Chain(a, b)` / `Then(...)` — composition, if two sources are genuinely both needed.

**Why a definition object rather than more attributes**: an attribute cannot carry behaviour (a completer closure), cannot be registered at RUNTIME
(mods register while the game runs), and cannot be validated as a unit. An object can do all three, and it is what the mod surface already half is
(`ModConsoleCommand` is a class — this ticket unifies the two paths instead of keeping two dialects).

## The non-local half (the part with a wire decision)

`CommandScope.HostRelayed` means: the invoking client sends the line to the host, the host runs the command's handler against its own authority and
sends the answer back. That needs three things this ticket must state before any code:

1. **A wire message** (`ModCommandInvocationMsg`: command name (namespaced), argument list, requester, request id; the answer travels back as a
   `ConsoleAnswer`-shaped reply with the same request id). The existing handshake already validates mod versions, so "the host has the mod" is checked
   at join time rather than guessed per command.
2. **A host-side registry** of mod commands that declared `HostRelayed`, gated by `ModPermission.RegisterCommand` (the permission that already exists
   for HOST command registration), plus the existing host rules (cheat switch, roster) when the command asks for `CommandPermission.Cheat`.
3. **Answer text and the reader's language**: a mod's answer is a key from the MOD's own catalogue plus arguments — the mod already registers
   language catalogues (see `docs/en/start/your-first-mod.md`), so the reply carries `(key, args)` and the receiver renders it through that mod's
   catalogue, falling back to the text. A plain text answer stays legal (a mod with no catalogue) but is marked as such, so the localisation census can
   tell the two apart.

**Alternatives costed, so the decision is not re-opened from scratch**:

| Shape | Cost | Why not now |
|---|---|---|
| host-relayed through a wire message (above) | one message pair, one host registry, permissions already exist | — (recommended) |
| the mod's client command calls a mod's own network path | nothing for CUO to build | every mod reinvents routing, permissions and idempotency; the console's own refusal surface cannot speak for it |
| no non-local mod commands at all | zero | the user named it as a requirement, and `/give`-shaped mod commands are the obvious first thing a mod wants |

## Staging (each stage ships and is verified on its own)

1. **The contract** — definitions, parameters, completers, parsers in `Abstractions`; the framework's ten kinds become completer objects; built-ins
   migrate behind the attribute (no behaviour change); `IModConsoleCommands.Register(ICommandDefinition)` replaces `ModConsoleCommand` (the old type
   stays as an obsolete adapter for one release window).
2. **Completion honours the contract** — the console asks the definition's parameter for its suggestions with the arguments already typed, so a mod
   can offer presets and context-dependent lists; the framework's preset catalogue is covered by tests.
3. **Non-local commands** — the wire pair, the host registry, the permission and host-rule gates, the answer keys; one built-in command is ported to
   `HostRelayed` first to prove the road (recommended `/give`, because its authority model is already host-side).
4. **Mod-side documents** — the how-to page gains the two recipes (a local command with a preset completer; a host-relayed command with a keyed
   answer), and the reference lists the shipped completers.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A mod registers a LOCAL command with a `OneOf` completer; typing its first argument offers exactly those words, judged from a captured frame |
| 2 | A mod's completer sees the arguments already typed: with `a` given, the second parameter offers only what `a` allows, and the frame shows it |
| 3 | A built-in command that used a `CommandArgumentKind` offers the same suggestions as before the migration (frame plus the suggestion list) |
| 4 | A `HostRelayed` mod command run by a member lands on the HOST (the host's log names the requester), and its answer reaches the member in the member's language |
| 5 | A member without the grant, or with the cheat switch off, is refused locally and the host sees no invocation |
| 6 | A mod answer with a key renders from the mod's own catalogue on both clients; a text-only answer still prints |
| 7 | The old `ModConsoleCommand` adapter still registers and runs (the deprecation window is real, not a claim) |

## Limits

- The ten built-in kinds stay: this is a contract change, not a new set of sources; a "one source per kind" test keeps the migration honest.
- A host-relayed command runs with the HOST's authority by design; a mod cannot raise its own permission by declaring one.
- Sandboxing/anti-cheat is out of MVP scope: a mod that ships a destructive command is the server owner's decision, and the host rules are the gate.

## Open decisions for the user

1. **Built-ins**: migrate every built-in onto the definition object now (uniform, bigger diff) or keep the attribute as sugar that produces a
   definition (recommended: smaller risk, one vocabulary anyway)?
2. **Mod commands and the host**: require the mod installed on BOTH sides for `HostRelayed` (recommended, matches the version check at join) or let a
   host-side mod expose a command to members who do not have it?
3. **Deprecation window** for `ModConsoleCommand`: keep it as an adapter (recommended) or replace it outright (pre-1.0, but mods already exist —
   `ModExample`, `Music`, `cuo.pinyinsearch`).
