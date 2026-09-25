# Declare permissions and host commands

[Documentation](../README.md) > [How-to](README.md) > Declare permissions and host commands

---

**After this page** your mod declares what it is allowed to do, and registers a command that runs on
the host. Read [Your first mod](../start/your-first-mod.md) first.

## Permissions are never implicit

The default is `ModPermission.None`: a surface you did not declare is not granted. The eight flags
and what each one gates:

| Flag | Gates |
|---|---|
| `SendNetworkMessage` | the mod message channel, **sending and receiving** |
| `RegisterCommand` | registering host commands |
| `ExecuteHostAction` | additionally required for a command marked `isHostAction: true` |
| `ReadGameState` | the read-only game-state projection |
| `WriteGameState` | the host-persistent mod state |
| `RegisterContent` | registering content the framework treats as part of the world |
| `SpawnEntity` | entity, item, tile, structure and liquid spawning |
| `AccessNativeApi` | the curated native operation registry |

A missing flag shows up as a refusal with a log line, not as an exception. The attribute is also
where the mod's **id, version and `NetworkMode`** are declared; `NetworkMode` has no default — a mod
that does not set it is rejected at discovery, and a duplicate id, a non-SemVer version or an
unsatisfiable dependency is rejected the same way, one mod at a time.

## Two command surfaces, and only one travels

- `context.Commands` — **host commands**. The handler runs on the host's copy of the mod, and a
  guest's call travels to the host. This is the surface this page is about.
- `context.ConsoleCommands` — **local console commands**. They run in the caller's own game with no
  wire relay; useful for a debug or client-side command.

## Registering a host command

```csharp
context.Commands.Register(new ModCommand(
	"heal",
	c => $"healed {string.Join(" ", c.Arguments)} for {c.RequesterSteamId}",
	description: "Heal a member",
	isHostAction: true));
```

`isHostAction: true` means only the host may execute it, and it needs `ExecuteHostAction` on the
attribute as well as `RegisterCommand`. The handler returns the text the requester sees; returning
`null` means "no output".

## What happens when a guest calls it

1. The guest's `TryExecute` sends a command request; the framework assigns a request id.
2. The host validates the request **before your handler runs**: the name is ≤64 characters, at most
   16 arguments of ≤256 characters and ≤4 KiB in total, the sender completed the
   [handshake](../reference/glossary.md), the mod and command are registered, and the permission
   flags are present. Per guest, requests are limited to 4 per second with a burst of 8.
3. Your handler validates the semantics — the framework cannot know that `alice` is a real member —
   and may authorize per guest from `c.RequesterSteamId`.
4. The host answers with a directed result that settles the caller's callback by request id.

```csharp
context.Commands.TryExecute("heal", new[] { "alice" }, result =>
	context.Logger.LogInformation("[Heal] {Name} ok={Ok} output={Output} error={Error}",
		result.Name, result.Success, result.Output, result.Error));
```

On the host the callback runs before `TryExecute` returns. On a guest it runs when the answer
arrives — or never, if the request is lost: the framework settles it after the request deadline
(10 seconds, swept once per frame) with `Success == false` and a `timed out` error, and settles every
pending request the same way when the session ends. At most 32 requests may be outstanding per mod;
one over that cap is refused at the sender (`false`, no callback).

## Traps

- **A timed-out request is not a crash, it is a result.** Always read `Success`; a host that dropped
  the request (rate limit, shape cap, not a member) and a lost answer reach you as the same timeout.
- **Do not block on a command.** The result arrives later by design; a guest's handler is a request,
  not a call.
- **An exception in your handler is not fatal.** It becomes `Success == false` with the exception
  message as `Error`.
- **Output is bounded**: 32 KiB for output, 4 KiB for the error text.

## Check that it worked

Run the command from the CUO console: the host prints the returned text, and a guest sees the
result of the host's execution, not its own. Kill the session while a guest request is pending: the
guest's callback fires with a timeout failure instead of hanging.

## Related reading

- [Send a message to the other players](send-a-network-message.md) — the channel a report travels on
- [Your first mod](../start/your-first-mod.md) — the attribute and the lifecycle
- [Permissions and what they protect](../internals/permissions-and-security.md) — what each flag enforces, and what it does not
- [Reference](../reference/README.md) — the full mod API will live there

---

[Documentation](../README.md) > [How-to](README.md) > Declare permissions and host commands
