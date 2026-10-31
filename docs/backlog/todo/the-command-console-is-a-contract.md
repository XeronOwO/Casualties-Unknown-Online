# The command console is a contract, not a set of attribute-marked methods

- Status: Todo — opened 2026-10-27 from the user's direction: "the command system probably needs a proper design; hand the interface down to mod
  authors, split local and non-local commands, delegate per-parameter completion (with presets), let completion depend on the context, and treat it as
  a ceiling on what the mod can do".
- Priority: High — this is the mod-facing surface, and the localisation contract rides on it: a refusal must be a KEY at its origin, which is the only way a player reads a Chinese shell AND a Chinese reason (the user's ruling, 2026-10-28). Its shape is hard to change once third-party mods depend on it.
- Category: Console / mod contract / architecture

## Design

`docs/design/surfaces/command-overlay.md` owns the console's player-facing surface and `docs/design/cross-cutting/localization.md` owns the rule that a
sentence a player reads is a key plus arguments — a mod command's answer has to satisfy both, which is why the answer shape is part of this ticket rather
than an afterthought. The console's own page records today's input/render behaviour; the mod contract it would gain is what this ticket designs.

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

## The answer contract is part of this redesign (user's direction, 2026-10-28)

The localisation work found the ceiling of the current shape and the user's ruling was to fold it in here rather than patch it family by family: *"use the
command-registration redesign to redo the whole command system, localisation included, so a Chinese shell AND a Chinese reason are possible from the
root."* That is the right home for it, and it changes the contract in three ways.

**1. A refusal is a KEY at its ORIGIN, never a sentence.** Today a parser or a service returns free English text for the detailed reason
(`ItemDefinitionTable`: "No item definition '{id}' is known to this game"; the state parser's "not part of an item's state"), and the console can only
print it or replace it with the generic `answer.refused_see_host_log` ("Refused — the host's log names the reason") — which is why the host sees English
and the member sees a sentence with no reason. In the contract, every refusal is a `CommandRefusal(Key, Args)` produced where the reason is KNOWN (the
parser, the definition table, the permission gate), so both the shell and the reason are catalogue entries and the generic line disappears.

**2. One answer shape for both sides.** An answer is a list of `(Key, Args)`, whether it is composed locally or arrives over the wire — `ConsoleAnswer.Parts`
and `ConsoleAnswerPartMsg` already are that shape, so the redesign keeps them and makes them the ONLY shape. The assembled English text stays as the log
line and the fallback, never as what a player reads.

**3. The catalogue is the command's own.** A command's sentences (description, usage, refusals, result lines, argument hints) live beside its definition —
built-ins in the shipped `en.json`/`zh.json`, a mod in its own catalogue — and the key namespace is derived from the command's name, not listed in a table
(the `console.cmd.<name>.desc` rule this tree already uses).

**What that means for the sweep already recorded** (the `give`/`summon`/`data`/`heal` family, `review/selectors-exclude-the-executor.md`): those are no
longer four small conversions. Each becomes "move the refusal reasons to keys at their origin", which is exactly what this ticket's contract makes
possible, and the family table there is the checklist for it. The two attempted `/give` conversions are recorded there with the reason they stopped:
without the keyed-origin rule, converting the LOCAL answer would either lose the detail (the generic line) or leave it English (shell + free reason).

**Acceptance rows added to this ticket**

| Row | The check |
|---|---|
| 12 | A refusal whose reason is technical ("no item definition", "not part of the state") reads in the reader's language on the client that typed it — judged from a frame, and the reason is translated, not the English sentence with a Chinese shell |
| 13 | The same refusal reads in the same language on a member who received it over the wire: one key, two clients, two languages, same meaning |
| 14 | `answer.refused_see_host_log` no longer exists: every refusal names its own reason |
| 15 | A mod's refusal uses the mod's own catalogue entry, and a missing entry falls back to the command's text rather than to a key |
| 16 | The give/summon/data/heal family's rows from the selector ticket are re-judged through the new shape, one row per family |
## The context a completer and a handler receive (the selector case, generalised)

The user's selector example is the case that makes this a design question rather than a plumbing detail: a selector argument must offer the `@`
forms (`@a`, `@p`, `@s`, `@r`, with their bracket filters `[limit=…]`, `[name=…]`) AND the concrete names of the players who are actually
reachable right now — which is a fact only the session holds, and only the LOCAL client's view of it. A completer that can answer that needs more
than the partial token; it needs the CUO context. Three rules make that safe.

**1. Injection is by NARROW fact interfaces, not one god object.** A completer or handler asks for what it needs; the framework hands it a view:

```
public interface ICompletionContext
{
    IReadOnlyList<string> Arguments { get; }   // already accepted in this line (context for argument N-1)
    string Partial { get; }                    // the token being typed
    string CommandName { get; }
    int ArgumentIndex { get; }
    ISessionFacts Session { get; }             // role, members, their readiness, host rules the client was told
    IWorldFacts? World { get; }                // entered? local body? nearby entities/containers (adapter-provided)
    IContentFacts Content { get; }             // item/entity definition ids, resource locations
    IModFacts Mods { get; }                    // what THIS mod may ask about (its own registered content)
}
```

- `ISessionFacts.Members` answers "who is online, and is this member reachable for this command" — the selector half that depends on CUO's own
  state; `ISessionFacts.HostRules` answers what the host announced, so a completer can refuse to offer an option the host forbids.
- `IWorldFacts` exists only where the Game Adapter is loaded; a headless or pre-world client answers `null`, and the completer says so rather than
  guessing (the same shape `ILocalCharacterCapture` already uses).
- The interface is the PERMISSION BOUNDARY: `IModFacts` is scoped to the calling mod's own registrations, so a mod cannot enumerate another mod's
  content through the completion channel. A fact a mod may not see is absent from its view, not filtered by the caller.

**2. The context is a SNAPSHOT, per request, never held.** The console builds one context object for one completion request (per keystroke) and one
for one invocation. A completer runs inside that request: it may read, it may not keep the object, and the framework hands it a copy-on-read list of
members rather than a live collection — a completer that runs while a member leaves must not observe a half-updated session, and a mod that stashes
the context for later must be unable to read a stale "who is online" as if it were current.

**3. Completing is cheap and idle-safe.** Completion runs on every keystroke while the console is open, so the framework caches the member list per
frame and a completer's cost must be O(candidates). A completer that would need the world (a nearby-item list) asks the adapter for a bounded query
(the framework passes an explicit bound, e.g. 64 candidates), never an unbounded scene walk. The existing ten kinds already pay this cost; the
contract has to keep it.

**The selector, written against that contract** (this is the shape the framework ships, not something a mod must reinvent):

```
SelectorCompleter : IArgumentCompleter
// Partial starts with '@'  -> offer the selector forms the command accepts, with their bracket filters
// Partial is a name prefix -> offer Session.Members whose state allows this command (role, readiness, host rules)
// Partial has a bracket    -> offer that selector's own filters ([limit=…] for @a/@p, [name=…] for @a)
```

The same three-way split applies to the built-ins' existing `Selector` kind, so the migration in stage 1 changes WHERE the list comes from, not what
the player sees.
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
| 8 | A selector argument offers BOTH the `@` forms with their filters and the names of the members who are actually reachable, judged from a frame |
| 9 | The list follows the session: a member who joins or leaves between two completion requests changes what the second one offers, and no completer can cache the first into the second |
| 10 | A mod's completer cannot see another mod's facts (the view is scoped to the caller), proven by a mod that asks for content it did not register |
| 11 | Completion stays cheap: a completer that needs the world receives a bounded candidate list, and the frame drawn per keystroke does not stall |

## Limits

- The ten built-in kinds stay: this is a contract change, not a new set of sources; a "one source per kind" test keeps the migration honest.
- A host-relayed command runs with the HOST's authority by design; a mod cannot raise its own permission by declaring one.
- Sandboxing/anti-cheat is out of MVP scope: a mod that ships a destructive command is the server owner's decision, and the host rules are the gate.
- The context is a SNAPSHOT, so a completer is never a source of truth for an INVOCATION: a command must re-read `ISessionFacts` inside its handler (the
  member may have left between the keystroke and the Enter), and the framework's own commands already do exactly that.

## Decisions taken (user, 2026-10-30) — and the ceiling they set

The user's direction this round answers the three questions below AND raises the ceiling: the console is to become a **mature, mod-extensible command
system**, not "a line that runs". The four answers, and what each obliges:

1. **Scope: a full mod-extensible command registration** — the most ambitious of the three options. A mod registers a command TREE (literals and arguments at
   any depth, `/give`-style branches), its own aliases and short forms, and declared cross-side execution. The console becomes a formal mod-API layer, which
   is why stage 1 (the definition contract) must be designed for the tree from the start rather than growing into it: a definition carries a name and
   parameters, and a tree is what a definition may CONTAIN (a literal node names a child set; an argument node carries its parser/completer).
2. **Mod capability: declarative PLUS a custom parser** — a mod declares the shape and MAY supply its own argument parser and completer for one parameter.
   The framework still owns the line's tokenisation, the list, the keys, the permission gate, the answer shape and the localisation; a custom parser only
   decides "does this token belong to this parameter, and what does it suggest". A parser that throws or hangs must be sandboxed by the framework (a
   per-call budget and a named refusal), because third-party code runs inside a frame.
3. **Non-local: declared `HostRelayed` behind a DOUBLE gate** — the mod declares that its command runs on the host and names the permission it needs; the
   HOST decides (its rules plus the cheat switch, exactly the `/give` authority model). The answer travels back as keys and is rendered by the member's own
   catalogue. A mod cannot raise its own permission by declaring one, and a `HostRelayed` command requires the mod on BOTH sides (the join-time mod check is
   already the gate; a member without it never sees the command).
4. **Built-ins stay attribute SUGAR over the same definition vocabulary** (the recommended option): the attribute produces a definition at registration, so
   there is one vocabulary and no second implementation, while the diff stays bounded. **`ModConsoleCommand` is replaced OUTRIGHT and deleted in the same
   change — no adapter, no deprecation window** (superseded 2026-10-30 by the standing ruling: nothing is released, the wire version does not increment, so a
   breaking change is free and a compatibility branch is debt; the earlier "adapter for one release window" answer is withdrawn, and `ModExample`, `Music`
   and `cuo.pinyinsearch` move to the definition contract in the same stage).

**Two interaction details the user asked for, beyond this ticket's own rows** (they ride the completion ticket, which is where the list and keys live):
parameter-level errors highlighted in place with a hint of what is expected, and a candidate list that can be CLICKED to complete and HOVERED to preview —
so the list is an input surface, not a caption.

## Open decisions for the user (answered above on 2026-10-30; kept for the record)

1. **Built-ins**: migrate every built-in onto the definition object now (uniform, bigger diff) or keep the attribute as sugar that produces a
   definition (recommended: smaller risk, one vocabulary anyway)? **Answered: sugar.**
2. **Mod commands and the host**: require the mod installed on BOTH sides for `HostRelayed` (recommended, matches the version check at join) or let a
   host-side mod expose a command to members who do not have it? **Answered: both sides.**
3. **Deprecation window** for `ModConsoleCommand`: keep it as an adapter (recommended) or replace it outright (pre-1.0, but mods already exist —
   `ModExample`, `Music`, `cuo.pinyinsearch`). **Answered twice: first as an adapter for one window (2026-10-30), then withdrawn the same day — the
   standing ruling is that nothing is released and no compatibility branch is kept, so it is replaced outright.**
## Stage 1 landed: the definition contract, and the old mod type is gone (2026-10-30)

`Abstractions` now carries the contract both sides speak: `ICommandDefinition` (name, aliases, description + key, usage, permission, scope, parameters,`n`Invoke`), `ICommandParameter` (name, completer, hint key, fallback kind), `IArgumentCompleter` / `ICompletionContext` / `CommandCompletion`,`n``ICommandInvocation` and `CommandScope`; plus the shipped shapes a mod builds — `ConsoleCommandDefinition`, `ConsoleCommandParameter`,`n``FixedChoiceCompleter` and `DelegateCompleter`. `IModConsoleCommands.Register` takes the interface, and `ModConsoleCommand` +`n``IModConsoleCommandContext` are DELETED in the same change (the pre-release ruling: no adapter, no deprecation window); the three published mods and the two test`nmods moved to the new shape, and the example mod shows an alias plus a delegate completer.

What the console does with a definition now:

- **completion asks the parameter first** (`CommandConsoleCompletion.FromTheParameter`): the completer runs with `ICompletionContext` — the command, the parameter
  index, the arguments already accepted, the partial token and the session snapshot — so argument N can depend on N-1; a completer that throws is caught and logged and the
  keystroke offers nothing instead of taking the overlay down;
- **the ten kinds stay**: a parameter that offers no completer answers from its declared `Kind`, so a mod composes with the framework's own sources and ranking;
- **name completion offers every ALIAS** beside its command, and the registry registers aliases as words that reach the same handler (`TryAdd`/`TryRemove` cover them);
- **a mod hint may be its own catalogue sentence**: `CommandSuggestionHint.KeyedText` carries a key plus its arguments and the overlay renders it like any framework key.

**Deliberately NOT in stage 1, and named so nobody reads it as done:** `CommandScope.HostRelayed` is declared, validated and logged, but nothing relays it yet — a
relaid command still runs locally, which is stage 3 (the wire pair, the host registry, the permission and host-rule gates). A command TREE (literals with children at any depth) is
not expressible yet either: the contract has parameters, and the tree is the shape stage 1's successor adds without changing what a definition is.
**Live check on the deployed build (`9930281d`):** typing `/music ` in the real client drew four candidate rows — `play`, `share`, `stop`, `status` — which are the`n`music` mod's own `FixedChoiceCompleter` words, not a framework kind; the driver's suggestion list read the same four. So a mod definition's completer reaches the`nplayer's list on a deployed client, which is the fact stage 1 was for.
## Stage 2 landed: a declaration composes with the framework's own sources (2026-10-31)

The completion path already asks a definition's parameter first (`CommandConsoleCompletion.FromTheParameter`) and hands the completer an `ICompletionContext` with the
command, the parameter index, the arguments already accepted and the partial token; what stage 2 still owed was proof that a MOD declaration reaches the framework's own
sources, not only that a custom completer runs. That is now pinned: the test mod declares `cselect <target>` with `kind: CommandArgumentKind.Selector` and NO completer, and
the completion answers the `@` forms with their filter hints and the reachable members — the same source a built-in selector parameter uses
(`ModConsoleCommandTests.ModConsoleCommand_DeclaredKind_GetsTheFrameworkSource`). It sits beside the existing `cresource` case (a ResourceLocation kind that inserts the
canonical mod id) and the `cmode` case (a definition's own completer, with the second parameter reading the first's value).

So a mod parameter has three ways to answer, all through one contract: its own `IArgumentCompleter`, a shipped completer (`FixedChoiceCompleter`, `DelegateCompleter`), or a
declared `Kind` that falls back to the framework source — and `ConsoleCommandParameter` documents exactly that composition.

## Stage 3's mechanism landed: a declared non-local command crosses the wire and comes back keyed (2026-10-31)

The relay already existed in the protocol (`ModCommandRequest` / `ModCommandResult`, the host's shape caps, rate limit and directed answer); what stage 3 owed was the CONSOLE
riding it.

- `CommandScope.HostRelayed` on a definition is honoured: on a member the console does not run the line — it sends a request with `SourceKind = Console` (the host must know
  which registry the name belongs to, because a mod's `IModCommands` API and its console registry are separate spaces) and prints the answer when it arrives.
- The host resolves the name in its own console registry, refuses a `HostOnly` definition to a member, and runs the definition's `RelayAnswer` with THE MEMBER as the
  requester. `ICommandInvocation.RequesterSteamId` is new, so a handler that says "you" means the player who typed, not the machine that executed.
- The answer travels as `ConsoleAnswerPartMsg` parts, so the member renders it in its own language; a plain text answer still prints.
- `CommandRelayHub` is the mediator: the console owns definitions and answers, the mod domain owns the frames, and neither references the other — the session container would
  not survive that cycle. The console's own half is `ConsoleCommandRelay`, and the console service's line writer is now `IConsoleLineSink`.

**Why not `/give`:** the ticket suggested porting `/give` first to prove the road, but `/give` ALREADY relays — its member half is a request whose answer comes back through
`PlayerItemGiveService` — so it would prove nothing new about this mechanism. The road is proven by the mod-facing half, where no such service exists.

**Judged so far, with a real host/guest pair in the tests:** a guest's relayed command runs ONLY on the host with the guest as requester and its keyed answer reaches the guest;
the host's own line runs in place; and a host that never built its console refuses the line with a keyed answer instead of dropping it. Full suite: 806 gates, 6551 tests.

**Still open in stage 3:** a SHIPPED sample command that declares `HostRelayed`, so the two-client live row can be judged (the host's log names the requester, the member
reads the answer in its own language).

## Stage 3 judged live: a member's line ran on the host and the answer came back keyed (2026-10-31)

The shipped sample now carries the road: `/examplehost <text>` declares `CommandScope.HostRelayed`. With the sample installed on both clients (it is a separate plugin the
deploy script does not install — this batch installed it by hand), the GUEST ran `/examplehost probe3`.

- The HOST executed it and logged the requester: `[Mod:cuo.example] [Example] host echo for 76561198863287957: probe3` — the id is the guest's, so the handler saw the player
  who typed, not the machine that ran it.
- The GUEST's console printed `the host answered 76561198863287957: probe3` — the answer crossed as `console.sample.hostecho` plus arguments and was rendered by the guest's
  OWN catalogue. That client's CUO language is English while the host's is Chinese, so the frame shows the READER's language, not the writer's.
- The reply is asynchronous: the command's own call returns before the answer, which is why the driver's `lastLine` right after the line still shows the previous line and the
  answer appears in the history a moment later. That is the intended shape for a non-local command.

The batch also caught a real defect in the sample: it registered console commands WITHOUT `ModPermission.RegisterLocalCommand`, so the adapter had been refusing every one of
them at bind time (`/examplehost` answered that the command was unknown). Fixed in the same cycle; the permission is part of what a sample has to demonstrate.

**Limit of this row:** the member's client was the English one. The Chinese sentence exists, and the rendering path is the generic keyed resolver already proven on the Chinese
client for the hint and answer rows, but this row did not watch a Chinese member read a relayed answer.

**Still open (stage 4):** a MOD's own language catalogue. A mod has no catalogue registration today — the census gates require every key in `src/` to be declared in the
framework tables, which is why the sample's sentence lives under `console.sample.*`. That gap is the mod-catalogue item the next stage has to close before a mod can ship its
own sentences.
## Stage 4's mechanism landed: a mod ships its own sentences (2026-10-31)

The gap the live batch exposed is closed on the MOD surface, not in the framework: the localisation service has been able to merge a mod's catalogue for a while (RegisterCatalog, additive,
never overwriting a built-in entry), but a mod had no way to reach it. IModContext.Localisation exposes it as IModLocalisation.RegisterCatalog(language, json), and the adapter guards the
namespace: every key must start with the mod's own id and a foreign key refuses the WHOLE table (a half-applied catalogue would leave a mod partly translated with nothing to see).

Two facts follow, and both are pinned: a mod's key renders in the reader's language, and a mod cannot redefine a framework sentence — the namespace guard refuses it, and the service refuses it again
on its own. 3 tests. The shipped sample now carries its own cuo.example.hostecho.answer in n and zh and answers HostRelayed with that key, so the sentence a member reads comes from the
MOD's catalogue; the framework table no longer holds it at all.

**Still open in stage 4:** the two how-to recipes (a local command with a preset completer; a non-local command with a keyed answer) and the reference row for the shipped completers.
