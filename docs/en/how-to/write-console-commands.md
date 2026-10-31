# Write a console command

[Documentation](../README.md) > [How-to](README.md) > Write a console command

---

**After this page** your mod registers an in-game console command a player can find, complete and read: one that runs in
the player's own game, and one that runs on the host. Read [Your first mod](../start/your-first-mod.md) first.

## The two recipes, and how to choose

A console command is an `ICommandDefinition` registered through `context.ConsoleCommands`. Its **scope** decides where the
handler runs:

| Scope | The handler runs in | Use it for |
|---|---|---|
| `CommandScope.Local` (default) | the client that typed the line | a command about that client's own view: a panel, a local diagnostic, a client-side toggle |
| `CommandScope.HostRelayed` | the HOST, on its copy of the mod | anything that changes the shared world or needs the host's authority |

A `HostRelayed` command needs the mod installed on **both** sides, because the host must know the definition to run it; the
handshake already refuses a session whose mod versions disagree, so "both sides have it" is checked at join time.

## Recipe 1 — a local command with a preset completer

```csharp
context.ConsoleCommands.Register(new ConsoleCommandDefinition(
	"exampleweight",
	"Set the weight the Example Dressing reports",
	invocation => SetWeight(invocation),
	parameters:
	[
		new ConsoleCommandParameter(
			"weight",
			new FixedChoiceCompleter("0.5", "1"),
			hintKey: "cuo.example.weight.hint"),
	],
	aliases: ["eweight"],
	usage: "/exampleweight <weight>"));
```

- **Parameters answer in this order:** the parameter's own `IArgumentCompleter`, then the shipped completers
  (`FixedChoiceCompleter`, `DelegateCompleter`), then the framework source its `Kind` declares (`Selector`, `ResourceLocation`,
  `PlayerOrSteamId`, …). A mod that only wants "a member name" declares the kind and writes no completion code.
- **The completer sees what was already typed:** `ICompletionContext.Arguments` holds the accepted arguments, so argument N
  may depend on argument N-1.
- **`hintKey`** is the grey sentence drawn beside the candidate; it is a key **your mod** owns (next section).

## Recipe 2 — a command that runs on the host, answered in the reader's language

```csharp
context.Localisation.RegisterCatalog("en", """{ "cuo.example.hostecho.answer": "the host answered {0}: {1}" }""");
context.Localisation.RegisterCatalog("zh", """{ "cuo.example.hostecho.answer": "<the same sentence, written in Chinese>" }""");

context.ConsoleCommands.Register(new ConsoleCommandDefinition(
	"examplehost",
	"Ask the HOST to echo a line",
	invocation => ConsoleAnswer.Ok(
		$"host echo for {invocation.RequesterSteamId}: {string.Join(" ", invocation.Arguments)}",
		"cuo.example.hostecho.answer",
		invocation.RequesterSteamId.ToString(),
		string.Join(" ", invocation.Arguments)),
	scope: CommandScope.HostRelayed,
	parameters: [new ConsoleCommandParameter("text")],
	usage: "/examplehost <text>"));
```

- **The answer is a key plus arguments, and the TEXT is the fallback.** The member's client renders the key from the mod's
  catalogue, so a Chinese member reads Chinese after an English author typed the sentence once. The text stays for the log and
  for a client whose catalogue lacks the entry.
- **`RequesterSteamId` is the player who typed**, even though the host ran the handler — a message that says "you" means the
  player, not the machine.
- **The reply is asynchronous.** The member's call returns before the answer; it prints when the answer arrives. Do not expect
  the line in `Lines` on the same frame.
- **Your catalogue must live under your own id.** Every key starts with the mod id plus a dot; a catalogue with one foreign key
  is refused **as a whole**, and a mod can never overwrite a framework sentence.

## Ship the sentences as data

The sample embeds one file per language beside the code and registers them at bind time, so a translator edits JSON instead of
C#:

```text
Localization/en.json   { "cuo.example.hostecho.answer": "the host answered {0}: {1}" }
Localization/zh.json   { "cuo.example.hostecho.answer": "<the same sentence, written in Chinese>" }
```

Add them to the project as embedded resources and read them in `Bind`:

```csharp
using var stream = typeof(MyMod).Assembly.GetManifestResourceStream("My.Mod.Localization.en.json");
using var reader = new StreamReader(stream!);
context.Localisation.RegisterCatalog("en", reader.ReadToEnd());
```

## Traps

- **A refused registration is logged, not thrown.** A malformed table or a foreign key leaves the mod running with the sentence
  falling back to its text — check the log after the first run.
- **Do not print player-visible text from the handler directly.** Return `ConsoleAnswer.Ok(text, key, args)`; `Refused` works
  the same way and prints in the error colour.
- **A parameter's `Kind` is the fallback, not the promise.** If the completion is wrong for your argument, give the parameter its
  own completer instead of overriding the framework's.
- **Aliases claim words too.** A duplicate name or alias is refused at registration with the reason.

## Check that it worked

Open the console (`/`), type the command's name and a space: the list shows what the parameter offers, and Tab completes it.
Run it from a member's client for the `HostRelayed` recipe: the HOST's log names the requester, and the member's console shows
the answer rendered from the mod's catalogue in the member's own language.

## Related reading

- [Declare permissions and host commands](declare-permissions-and-commands.md) — the flags, and the older host-command surface
- [The sample mod](../start/your-first-mod.md) — the code these recipes are cut from
- [Reference: the mod API](../reference/mod-api.md) — `ConsoleCommands`, `Localisation`, and every member of each
- [Terminology](../reference/glossary.md) — host-relayed, definition, completer

---

[Documentation](../README.md) > [How-to](README.md) > Write a console command
