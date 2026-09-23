# Read game state

[Documentation](../README.md) > [How to](README.md) > Read game state

---

**After this page** your mod reads the state CUO already holds about another player — whether they
are in the world, their vitals and their carried items — and knows how fresh that reading is. Read
[Your first mod](../start/your-first-mod.md) first.

## The permission is the switch

Reading is gated by `ModPermission.ReadGameState`; the flag itself is explained in
[Declare permissions and host commands](declare-permissions-and-commands.md).

```csharp
if (context.GameState.CanRead && context.GameState.TryGetPlayer(steamId, out var player))
{
	var health = player.Vitals?.BrainHealth;
	var items = player.Inventory?.Items;
}
```

`CanRead` is true when this copy of the mod declared the flag. Every `TryGetPlayer` call enforces it
again and answers `false` with a log line instead of throwing, so a mod that forgot the flag shows up
as "no data", never as a crash.

## What one player looks like

| Member | What it holds |
|---|---|
| `SteamId` | whose state this is |
| `InWorld` | whether this side's session currently knows that player is in the world |
| `Vitals` | `IModPlayerVitals`, or `null` before the first health block arrives |
| `Inventory` | `IModPlayerInventory`, or `null` before the first item snapshot arrives |

`Vitals` carries `BrainHealth`, `Hunger`, `Thirst`, `Stamina`, `Energy`, `Temperature` and the
derived `Alive` and `Conscious` flags. `Inventory` holds the top-level `Items`, the raw `HandSlot`
value and `Count`; each `IModInventoryEntry` gives `InstanceId`, `ItemId`, `SlotIndex`, `Condition`,
`Favourited` and a recursive `Contents` list, so a container's contents are walked as a tree. A
negative `SlotIndex` is a worn item — that is the game's own encoding, not a CUO invention.

## The reading is a copy, and it is up to a second old

The projection holds what already arrives on the 1 Hz character stream — the same source the built-in
online UI draws from. Nothing here is pushed at you:

- **Read on your own cadence.** Each `TryGetPlayer` returns the facts cached at that moment; the
  returned objects are copies and stay valid after the call, for example across frames.
- **A missing half is normal.** Vitals and inventory arrive in separate snapshots, so one can be
  `null` while the other is populated. Treat `null` as "not yet", never as zero.
- **The cache is session state.** A player leaving the world, or the session ending, clears it and
  `TryGetPlayer` starts answering `false`; it also answers `false` while neither half has arrived
  yet, so `true` is never a promise that it stays true.

## What this surface does not cover

The local player's own character state is not part of `IModGameState`; it is available through the
native API's read-only local-player projection instead. World, item, block and entity tables are not
exposed here yet — the same projection pattern is the path the framework follows when they are.

## Traps

- **Do not re-read per frame for a value the game produces once a second.** The stream is 1 Hz;
  reading it sixty times a second only burns frames.
- **This is not an authority.** It is a read model: the value you read is what CUO last heard, and
  nothing you decide from it has been arbitrated. See
  [Decide which side judges an action](decide-which-side-judges-an-action.md).
- **A `null` half is not a failure to handle later.** Branch on it now, because the first moments
  after a join legitimately have neither half.

## Check that it worked

Run a session with a second player in the world and log what you read for their Steam id once a
second: the values appear after their first character snapshot and change when they take damage, eat
or pick something up. Have them leave the world and read again — `TryGetPlayer` answers `false`.

## Related reading

- [Declare permissions and host commands](declare-permissions-and-commands.md) — the flag this page needs
- [Send a message to the other players](send-a-network-message.md) — what to do when what you read has to travel
- [Decide which side judges an action](decide-which-side-judges-an-action.md) — why a reading is not a verdict
- [Your first mod](../start/your-first-mod.md) — the lifecycle the reads live in
- [State streams and snapshots](../internals/state-and-snapshots.md) — where a reading comes from, and why it is not a verdict
- [Glossary](../reference/glossary.md) — projection, snapshot, state stream

---

[Documentation](../README.md) > [How to](README.md) > Read game state
