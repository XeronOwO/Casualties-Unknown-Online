# Save mod data across sessions

[Documentation](../README.md) > [How to](README.md) > Save mod data across sessions

---

**After this page** your mod keeps its own small amount of data — settings, unlocks, counters — in the
host's CUO config file (`BepInEx/config/`), shared across worlds, and reads it back on the next run.
Read
[Declare permissions and host commands](declare-permissions-and-commands.md) first: the write needs a
flag from it.

## One table, one owner

`context.State` is scoped to your mod id: you read and write your own entry and nobody else's. Values
are opaque `byte[]`, and the framework never interprets or serialises them, so the format, the
versioning and the migration are yours.

```csharp
if (context.State.CanWrite)
{
	context.State.TrySet("loadout", Encoding.UTF8.GetBytes(json));
	context.State.TrySetSchemaVersion(2);
}

if (context.State.TryGet("loadout", out var bytes))
{
	// bytes is your own format; switch on context.State.SchemaVersion if it changed
}
```

Both directions copy: mutating the array you passed, or the one you received, does not touch the
stored table until the next explicit `TrySet`.

## Only the host writes, and only with the flag

`TrySet`, `TrySetSchemaVersion`, `TryRemove` and `TryClear` require the host role **and**
`ModPermission.WriteGameState`; `CanWrite` is true only for that combination. A guest copy of a
synchronized mod sees `CanWrite == false` and cannot read the host's table either. When a guest needs
something written, it asks the host — `IModNetwork` or a host command, as in
[Send a message to the other players](send-a-network-message.md) — and the host's copy does the
write.

## When it reaches the disk

- The host writes a versioned file at `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin`, one
  atomic temp-and-replace per successful call.
- **Every write persists the whole table.** Write when the value changed, not per frame.
- The in-memory table is process-scoped and is loaded once, before mod discovery and `Bind`, so
  reading in `Bind` already returns last session's data.
- A missing file means empty state. A corrupt or unknown-version file degrades to empty with a
  warning — never a startup crash, and never a guessed migration.
- The file records the mod id, the mod version of the last writer and your declared schema version.
  `SchemaVersion` defaults to 1 until you set it.

## Entries outlive a missing mod

If your mod is not loaded on the next run, its entry is preserved untouched, so the data is still
there when the mod comes back. The same holds for a mod that was removed from the mod list for one
session.

## Limits

A key is at most 128 characters, a mod holds at most 1024 keys, and a value is at most 64 KiB. A call
that breaks a limit returns `false` and logs; nothing is silently truncated.

## Traps

- **Read the return value.** A refused write is otherwise invisible: `TrySet` answers `bool`.
- **Do not persist what you can rebuild.** This surface is for data the mod owns; world state belongs
  to the kernel, and a mod that mirrors it will disagree with the world sooner or later.
- **Keep the schema version out of your payload.** `TrySetSchemaVersion` is stored as metadata the
  framework carries for you; your own migration code reads `SchemaVersion` back on the next run.
- **A guest that writes a local file is wrong by construction.** The host's copy is the table; a guest
  coordinates with it instead of growing a second one.

## Check that it worked

On the host, write a key and set a schema version, quit the game completely, start it again and read
the key back: the value and the version are the ones you wrote. Replace the contents of the file with
garbage and start again — the log carries a warning and the table starts empty, and the game still
starts. On a guest, `CanWrite` is false and `TrySet` returns false with a log.

## Related reading

- [Send a message to the other players](send-a-network-message.md) — how a guest asks the host to write
- [Register content](register-content.md) — the other half of what a mod brings with it
- [Declare permissions and host commands](declare-permissions-and-commands.md) — `WriteGameState` and host commands
- [Your first mod](../start/your-first-mod.md) — the lifecycle the reads and writes live in
- [Glossary](../reference/glossary.md) — host, session, world

---

[Documentation](../README.md) > [How to](README.md) > Save mod data across sessions
