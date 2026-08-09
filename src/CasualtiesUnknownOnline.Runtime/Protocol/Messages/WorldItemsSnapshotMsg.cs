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

	/// <summary>The layer modifier the host's world rolled at generation finish
	/// (ApplyLayerModifiers, WorldGeneration.cs:3729 — an index into
	/// LayerModifier.availableModifiers, -1 = none). The modifier decision reads
	/// the random stream AFTER the darken-wait suspension, which the isolation
	/// does not restore (the suspension's real-stream draws leak into it), so
	/// every side rolls its own modifier — the host's decision is the world
	/// definition and travels with the generation snapshot; the guests apply it
	/// and skip their own local roll.</summary>
	[ProtoMember(2)]
	public int LayerModifierIndex { get; set; } = -1;

	/// <summary>The random stream state at the entry of the host's modifier
	/// decision (ApplyLayerModifiers' first draw), non-null when a modifier was
	/// rolled. The guests restore it and replay the decision draws before
	/// running the modifier's Initialize — the world effects (Flooded's liquid
	/// fills, Infested/Ionized's entity distributions) consume the SAME random
	/// stream the host's did and land in identical positions on every side.</summary>
	[ProtoMember(3)]
	public byte[]? LayerModifierRandomState { get; set; }
}
