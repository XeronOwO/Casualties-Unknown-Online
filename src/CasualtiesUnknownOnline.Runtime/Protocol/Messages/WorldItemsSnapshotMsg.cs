using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The generation-time world items (ground items + the starting supplies),
/// broadcast host → guests when a world generation finishes. The host assigns
/// every item a host-allocated instance id and distributes the full set in ONE
/// reliable message — a hundred per-item spawns would flood the channel, and a
/// per-item race would let two sides allocate two ids for the same object
/// (the pickup race: "generated item picked up by two players — duplicate
/// copies"). The receiver binds its local copies to the host's ids
/// (ItemSnapshotEntryMsg.SlotIndex &gt;= 0 = a backpack-slot carried item,
/// bound by slot — the starting supplies) or materializes the host's version;
/// local copies the host does not know (per-side random spawns, e.g. the
/// corpse-loot rolls that run on the real stream) are destroyed. World-gen
/// determinism keeps the ground layout identical on every side — the ids are
/// the only thing this message distributes; a divergent side converges by
/// replacement.
/// </summary>
[ProtoContract]
public sealed class WorldItemsSnapshotMsg
{
	[ProtoMember(1)]
	public List<ItemSnapshotEntryMsg> Items { get; set; } = [];
}
