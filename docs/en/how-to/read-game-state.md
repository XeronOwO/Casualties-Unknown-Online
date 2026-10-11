# Read game state

[Documentation](../README.md) > [How-to](README.md) > Read game state

---

**After this page** your mod reads the state CUO already holds about another player — whether they
are in the world, their vitals and their carried items — follows one item by its id wherever it goes,
and knows how fresh each reading is. Read [Your first mod](../start/your-first-mod.md) first.

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

## Follow one item by its id

An instance id names one item for its whole life. Read that id and you get what the item is and where
it is now, wherever it has moved since you recorded it:

```csharp
if (context.GameState.TryGetItem(itemId, out var item))
{
	switch (item.Location)
	{
		case ModItemLocation.Applied:
			// installed on limb item.LimbIndex of player item.OwnerSteamId
			break;
		case ModItemLocation.Carried:
		case ModItemLocation.Contained:
			// with player item.OwnerSteamId, and inside item.ParentItemId when contained
			break;
		case ModItemLocation.World:
			// lying at item.X, item.Y
			break;
		case ModItemLocation.Terminal:
			// this id's life ended, and it is never issued again
			break;
	}
}
```

| `Location` | What the item is doing | Members that mean something |
|---|---|---|
| `World` | lying in the world | `X`, `Y` — plus `ParentItemId` when it sits in a world container |
| `Carried` | in a player's inventory, hands or worn | `OwnerSteamId` |
| `Contained` | inside another item | `OwnerSteamId`, `ParentItemId` |
| `Applied` | installed on somebody's limb as a splint or a tourniquet | `OwnerSteamId`, `LimbIndex` |
| `Terminal` | consumed, destroyed, cooked or crafted away | none — the id ended |

`InstanceId`, `ItemId`, `Condition` and `Favourited` are there whatever the location is; every other
member carries `0` (or `-1` for the limb), so `Location` is what says which of them to read. The one
member that can surprise you is `ParentItemId`: a contained item is inside it, and a world item can be
bound to one too — a world container's contents are recorded as world items — so a non-zero parent is not
the same fact as being contained.

Two things this read is deliberately careful about:

- **An applied item is not in anyone's `IModPlayerInventory`** — the player is not carrying it, and an
  inventory line there would say they were. It keeps its id, which is why `TryGetItem` finds it on the
  limb and finds the same item in the operator's hands once it comes off.
- **"Ended", "never existed" and "not here yet" are three different answers.** An id whose life ended is
  `true` with `Location == Terminal`; an id your client holds no fact for is `false` — and that is every
  id at once on a guest that has not yet received the join checkpoint, as well as `0`, which means "not
  yet allocated". Only `splint`, `carcasssplint` and `tourniquet` keep their id across the transform onto
  a limb; the other limb tools — the ice pack, the bandage, the suture — do not transform the item at
  all, and cooking and crafting end the source row and give the product a **new** id, so the raw steak's
  id is not the cooked steak's.

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
- **The two reads have different sources.** `TryGetPlayer` reads the 1 Hz character stream;
  `TryGetItem` reads the framework's own item fact, which is what a move commits. Every client holds
  that fact — the host's own record, and on a guest the mirror that each committed batch and the join
  checkpoint fill in — so a teammate's copy sees an item move for the same reason the host's does.

## What this surface does not cover

The local player's own character state is not part of `IModGameState`; it is available through the
native API's read-only local-player projection instead. Block and entity tables are not exposed here,
and there is no way to enumerate items: the by-id read answers for an id you already hold, which is
what makes it a follow-the-item read rather than a world dump. A world item's position is the only
world fact it carries.

## Traps

- **Do not re-read per frame for a value the game produces once a second.** The player stream is 1 Hz;
  reading it sixty times a second only burns frames. The by-id read is cheap, but an item's condition
  still only moves when something moves it.
- **This is not an authority.** It is a read model: the value you read is what CUO last heard, and
  nothing you decide from it has been arbitrated. See
  [Decide which side judges an action](decide-which-side-judges-an-action.md).
- **A `null` half is not a failure to handle later.** Branch on it now, because the first moments
  after a join legitimately have neither half.
- **A `false` from `TryGetItem` is not "it was destroyed".** Check `Location == ModItemLocation.Terminal`
  for that, and treat `false` as "this framework holds no such id".

## Check that it worked

Run a session with a second player in the world and log what you read for their Steam id once a
second: the values appear after their first character snapshot and change when they take damage, eat
or pick something up. Have them leave the world and read again — `TryGetPlayer` answers `false`.

For the item read, log the id of something you just picked up and keep logging it: it stays the same
item as you move it, and it reads `Applied` with the right limb while a splint is on somebody's leg.
Put a splint on a teammate and read that id from your own client — you see it on their limb, which no
inventory projection would show you.

## Related reading

- [Declare permissions and host commands](declare-permissions-and-commands.md) — the flag this page needs
- [Send a message to the other players](send-a-network-message.md) — what to do when what you read has to travel
- [Decide which side judges an action](decide-which-side-judges-an-action.md) — why a reading is not a verdict
- [Your first mod](../start/your-first-mod.md) — the lifecycle the reads live in
- [State streams and snapshots](../internals/state-and-snapshots.md) — where a reading comes from, and why it is not a verdict
- [Glossary](../reference/glossary.md) — projection, snapshot, state stream

---

[Documentation](../README.md) > [How-to](README.md) > Read game state
