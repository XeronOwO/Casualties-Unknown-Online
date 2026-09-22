# Register content

[Documentation](../../README.md) > [How to](README.md) > Register content

**After this page** your mod registers content of its own — an item, a recipe, a tile, a status — and
the framework treats it as part of the world. Read [Your first mod](../start/your-first-mod.md) and
[Declare permissions and host commands](declare-permissions-and-commands.md) first.

## Content travels with the mod, not over the wire

The bytes you register are part of your mod assembly; the registry is process-local and nothing about
it is sent to anyone. The consistency boundary is the mod handshake — mod id, version, permissions
and network mode — so every player who can receive an instance of your content must run the same mod
version. That is why the runtime binder only routes content from modes that guarantee a matching copy
on every receiver, `Synchronized`, `Authoritative` and `RequiresAllPlayers`, and never binds
`HostOnly`, `ClientOnly` or `Cosmetic` content into shared world state.

## Declare your namespace first

A content id is canonical `namespace:path`, and the namespace comes from the mod declaration:

```csharp
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Namespace = "example",
	Permissions = ModPermission.RegisterContent)]
public sealed class ExampleMod : ICuoMod
```

With `Namespace = "example"`, your item is addressed as `example:wooden.sword`, while built-in game
content stays `cu:<item id>` — `cu:fentanyl`, for example. The namespace grammar is
`[a-z][a-z0-9_]{0,31}` and the path-segment grammar is `[a-z0-9][a-z0-9_.-]{0,94}` — a leading
character and up to 94 more, 95 in all. `ContentId` parses and formats both, and normalises case for
external input; registration itself requires the canonical lower-case form. A mod without a declared
namespace keeps the bare id and stays mod-scoped; the bare id is also the key the game tables are
filled under, so two mods registering the same bare id for the same kind are a conflict even in
different namespaces — the canonical addresses both resolve, but only one game entry can exist, and
the console's resource-id completion skips such an ambiguous bare id entirely rather than offer an
address the game cannot honour.

## Register in `Bind`

```csharp
if (context.Content.CanRegister)
{
	var sword = new ModItemDefinition
	{
		DisplayName = "Wooden Sword",
		Description = "A simple wooden sword.",
		Weight = 1f,
		Value = 5,
		Usable = true,
		Tags = "weapon",
		TemplateId = "stone"
	}.ToPayload();

	context.Content.TryRegister("wooden.sword", ModContentKind.Item, sword);
	context.Content.TryRegister("healing.recipe", ModContentKind.Recipe, recipeBytes, schemaVersion: 2);
}
```

`TryRegister` takes the bare id, the kind and an opaque payload. `Definitions` returns a snapshot
whose payloads are copied on read, `IsRegistered` answers for one id, and `TryUnregister` removes a
definition. Registration belongs in `Bind`: the registry is loaded once before discovery and `Bind`,
so a definition registered later is a race you own.

## The kinds with a typed payload

Registration itself only understands `id`, `kind` and `bytes`. For the kinds the framework binds into
the game today, `CUO.Abstractions` also ships a DTO you fill in and turn into the payload with
`ToPayload()`:

| Kind | DTO | What the adapter does with it |
|---|---|---|
| `item` | `ModItemDefinition` | registers the item and builds a runtime template from `TemplateId` |
| `recipe` | `ModRecipeDefinition` | injects the recipe into the recipe table |
| `liquid`, `liquidtile` | `ModLiquidDefinition`, `ModLiquidTileDefinition` | fills the liquid registry and the world fluid grid |
| `building` | `ModBuildingDefinition` | builds the building template and can feed world generation |
| `tile`, `structure` | `ModTileDefinition`, `ModStructureDefinition` | allocates a block index and can feed world generation |
| `status`, `moodle` | `ModStatusDefinition`, `ModMoodleDefinition` | stores the static descriptor the projection reads |

Most of these DTOs also carry a `CustomData` dictionary for the fields a well-known kind does not name
yet — `ModRecipeDefinition` and `ModLiquidDefinition` do not — and the framework still stores
whatever you registered as opaque bytes.

## Asking who owns a definition

```csharp
if (context.ContentOwners.TryGetOwner(ModContentKind.Item, "example:wooden.sword", out var owner))
{
	context.Logger.LogInformation("[Example] {Id} belongs to {Owner}", "example:wooden.sword", owner);
}
```

The query is read-only, needs no permission, and follows the same duplicate policy as the runtime
catalog: an ambiguous kind + id answers `false` rather than guessing.

## What is refused

- The `RegisterContent` flag missing: `CanRegister` is false and every call returns false with a log.
- A duplicate id inside the same mod, an empty id or an empty kind.
- A payload over 64 KiB, a non-positive schema version, or more than 1024 definitions per mod.
- An id that is not a canonical lower-case path segment: upper case, whitespace, an embedded `:`, or
  a path over 95 characters.
- Two mods registering the same kind and the same bare id.

A refusal is a `false` plus a log line; nothing is silently truncated, and nothing throws.

## Traps

- **Do not register while you play.** Registration is content, not runtime state; the registry
  belongs in `Bind` and there is no per-frame path into it.
- **You own the schema and its migrations.** The framework stores your bytes and your `schemaVersion`
  verbatim and never converts between versions.
- **The game table not being ready is not your problem.** The adapter providers wait for the table
  they fill; your definition is read from the registry when it is.
- **Registering content is not synchronizing it.** A registered item appears nowhere by itself;
  placing one is a spawn, which is a separate permission and a separate page.

## Check that it worked

Load the mod and log `context.Content.IsRegistered("wooden.sword")` — `true` means the registry took
the definition, and the log carries no refusal for it. The console's resource-id completion (the
`ResourceLocation` vocabulary) then offers your content under its canonical id — a legacy
registration with no namespace has no canonical id and is deliberately skipped — and an item
registered with a `TemplateId` can be spawned through the item spawn surface. A mod that declared the
wrong `NetworkMode` shows up as content that exists locally and never binds into a shared world.

## Related reading

- [Declare permissions and host commands](declare-permissions-and-commands.md) — the flag and the declaration
- [Save mod data across sessions](save-mod-data-across-sessions.md) — the other half of what a mod persists
- [Read game state](read-game-state.md) — what you can read back about a player
- [Your first mod](../start/your-first-mod.md) — the `Bind` lifecycle
- [Glossary](../reference/glossary.md) — mod, adapter, handshake

[Documentation](../../README.md) > [How to](README.md) > Register content
