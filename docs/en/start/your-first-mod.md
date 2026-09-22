# Your first mod

[Documentation](../../README.md) > [Start](README.md) > Your first mod

**After this page** you have read a working CUO mod end to end — the two files it takes, what the
framework hands you, and how to watch it run. The code is
`src/CasualtiesUnknownOnline.ModExample/`, and it is meant to be copied as a starting point.

## Two files, two jobs

A CUO mod is two types in one assembly, and the split is deliberate.

**The shell** exists only so BepInEx loads the assembly. It derives from `BaseUnityPlugin`, carries
`[BepInPlugin(...)]`, and stays **empty**:

```csharp
[BepInPlugin("CasualtiesUnknownOnline.ModExample", "CUO Mod Example", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
}
```

BepInEx instantiates that type itself. CUO instantiates the mod class separately, in its own first
frame, after every plugin's `Awake`. One type playing both roles would give you two objects with
independent state — the double-instance trap — so the two-file layout is not a style choice.

**The mod class** is the mod: an `ICuoMod` marked with `[CuoMod(...)]`, created by CUO and never by
BepInEx. All logic lives there, and it references `CUO.Abstractions` only.

## The declaration

```csharp
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand
		| ModPermission.ExecuteHostAction | ModPermission.RegisterContent)]
public sealed class ExampleMod : ICuoMod
```

- the **id** (`cuo.example`) is what other mods depend on and what the host sees;
- **`NetworkMode.Synchronized`** means both sides must run the same version: a member that does not
  have the mod is refused at the join [handshake](../reference/glossary.md) instead of failing later;
- **`Permissions`** declares what the mod may do. What is not declared is not granted, so the list
  doubles as the mod's public surface.

## What the framework hands you

CUO calls `Bind(IModContext context)` once, and everything arrives through that context:

```csharp
context.Content.TryRegister("example.recipe", "recipe", [0x01, 0x02, 0x03]);
context.Network.MessageReceived += (sender, payload) => { /* … */ };
context.Commands.Register(new ModCommand("echo", c => $"echo:{string.Join(" ", c.Arguments)}"));
context.Ui.Register("example", "CUO Example", window => window.Label("hello"));
context.PlayerJoined += id => context.Logger.LogInformation("[Example] player {Id} joined.", id);
```

| Piece | What it is for |
|---|---|
| `context.Logger` | your lines land in the same logs as CUO's |
| `context.Content` | register content the framework then treats as part of the world |
| `context.Network` | send and receive your own messages |
| `context.Commands` | console commands; `isHostAction: true` marks one only the host may run |
| `context.Ui` | a panel inside the CUO interface |
| `context.Session` | whether a session is active, and whether you are the host |
| events | `PlayerJoined`, `PlayerLeft`, `SessionEnded` |

## The lifecycle

```
discovery frame:  Bind → Initialize → Start → Update
every frame:      Update
leaving:          Stop → Dispose
```

`Bind` runs in CUO's first update frame — not in the shell's `Awake`, which is why a mod loaded after
CUO still works. From then on, `Update` runs once per frame.

## Watch it work

1. Build and deploy your build — [Set up a development checkout](set-up-dev-environment.md).
2. Start the game: the log carries `[Example] bound (session active: …, host: …)`.
3. Open the CUO console: the mod's commands are listed as `/echo` and `/whoami`.
4. Run `/echo hello` — the mod answers `echo:hello`. `/whoami` is a host action: only the host may
   run it, and it answers with the requester's Steam id.
5. When another member's mod sends a message, the receiving side logs it and the host broadcasts it
   back to every member as `echo:…`.

## Traps

- **Never touch CUO or the game from the shell.** Its `Awake` may run before or after CUO's, and the
  mod class is the only object that holds state.
- **Reference `CUO.Abstractions` for logic and `BepInEx.Core` for the shell** — never the game
  assemblies, never `CUO.Runtime`.
- **Declare before you use.** A permission missing from the attribute is refused when you use it.

## Related reading

- [Set up a development checkout](set-up-dev-environment.md) — build and deploy what you just read
- [Install and play](install-and-play.md) — run it as a player
- [Glossary](../reference/glossary.md) — mod, patch, adapter, handshake
- [Reference](../reference/README.md) — where the full mod API will live

[Documentation](../../README.md) > [Start](README.md) > Your first mod
