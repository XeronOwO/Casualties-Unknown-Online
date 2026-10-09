# Send a message to the other players

[Documentation](../README.md) > [How-to](README.md) > Send a message to the other players

---

**After this page** your mod can talk to the other machines in a session, and you know what the
framework does with a message that never arrives. Read [Your first mod](../start/your-first-mod.md)
first.

## The shape of the network

Mod messages travel in a star, never directly between guests:

```
a guest ──report──▶ the host ──broadcast──▶ every member, the host included
```

There is no automatic relay: a message sent to one member is not forwarded to anyone else, and a
guest cannot broadcast. The pattern that follows is: **a guest reports upward, the host decides, the
host broadcasts the result downward.**

## The four calls

| Call | On the host | On a guest |
|---|---|---|
| `SendToHost(value)` | nothing — the host is the destination | reports to the host's copy of the mod |
| `SendToPeer(steamId, value)` | sends to one member's copy | nothing |
| `Broadcast(value)` | sends to every member, including the host's own copy | nothing |
| `MessageReceived += (sender, value)` | a guest's report, or the host's own broadcast | a directed or broadcast frame |

Outside a session every send is a no-op, so a mod does not have to check the session first.

## Declare the permission

Sending needs `ModPermission.SendNetworkMessage` on the `[CuoMod]` attribute. A send without it is
refused at the sender with a log line, and a receive without it is dropped. If your messages vanish
without an error, check the attribute before anything else.

## A worked example

```csharp
[CuoMod("com.example.ping", "Ping", "1.0.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage)]
public sealed class PingMod : ICuoMod
{
	private IModContext? _context;

	public void Bind(IModContext context)
	{
		_context = context;

		context.Network.MessageReceived += (sender, value) =>
			context.Logger.LogInformation("[Ping] from {Sender}: {Value}", sender, value);

		context.Ui.Register("ping", "Ping", window =>
		{
			if (!window.Button("ping"))
			{
				return;
			}

			var value = ModValue.Map(("kind", ModValue.Text("ping")), ("at", ModValue.Integer(Environment.TickCount)));
			if (context.Session.IsHost)
			{
				context.Network.Broadcast(value);  // host → everyone
			}
			else
			{
				context.Network.SendToHost(value); // guest → the host's copy
			}
		});
	}
	…
}
```

The payload is a [value](../reference/glossary.md) - CUO's typed data model, never opaque bytes - so the framework validates its shape, bounds it and can log it, while what it MEANS stays yours. The `using` directives the snippet leaves out are in the example mod.

## When a message never arrives

The policy is *accepted loss*, not guaranteed delivery:

- the transport is reliable, but a frame over the per-sender burst is **dropped with a log**, never
  queued and never re-sent — 20 messages per second sustained, with a burst of 40;
- so do not build on retries: design the **next** message to carry the state, so a lost frame costs
  freshness rather than correctness;
- values are capped at **64 KiB** encoded, refused at the sender and checked again at the receiver, and a
  value the framework cannot encode is refused with one log line naming the path inside it;
- a message whose mod is unknown to the receiver is dropped with a log. With
  `NetworkMode.Synchronized` that case is prevented earlier, at the join
  [handshake](../reference/glossary.md), because a member without the mod is refused.

## Check that it worked

1. Build, deploy and start the game — see [Set up a development checkout](../start/set-up-dev-environment.md).
2. Open the CUO interface, then the mod's `Ping` window.
3. Press **ping** on the host: every member logs `[Ping] from <host steam id>: ping`.
4. Press it on a guest: the host logs the report and the other guests see nothing, because a guest's
   report goes to the host's copy only. Broadcast it back if everyone should see it.

## Traps

- **A guest calling `Broadcast` does nothing.** It is a no-op on that machine, so the message never
  leaves it.
- **`SendToHost` on the host does nothing.** The host's copy is already the destination — use
  `Broadcast` when the host wants everyone to act.
- **A missing permission is a log line, not an exception.** The send is refused; nothing is thrown
  into your mod.
- **A different mod build is a session problem, not a message problem.** With `Synchronized`, a
  member without the same version is refused at the join.

## Related reading

- [Your first mod](../start/your-first-mod.md) — the lifecycle and the context used here
- [How-to](README.md) — the other tasks
- [Reference](../reference/README.md) — where the full mod API will live
- [Who decides what happens to a player](../internals/judgment-ownership.md) — why a guest reports upward and the host answers
- [Glossary](../reference/glossary.md) — host, guest, session, handshake

---

[Documentation](../README.md) > [How-to](README.md) > Send a message to the other players
