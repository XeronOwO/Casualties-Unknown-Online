using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A guest's carried inventory with self-assigned instance ids: reported once
/// its local world generation finished (the starting supplies and any worn
/// items it gave ids to itself — ids are (counter &lt;&lt; 32) | SteamId, so the
/// guest can allocate without host round-trips). The host registers the entries
/// in the guest's transfer table — the authoritative record that makes use/slot
/// reports arbitrate normally — and the peers render the guest's clone from the
/// 1 Hz character snapshot as usual.
/// </summary>
[ProtoContract]
public sealed class CarriedInventoryMsg
{
	/// <summary>The sender's carried items (full state — condition/components/liquids/contents).</summary>
	[ProtoMember(1)]
	public List<CharacterItemMsg> Items { get; set; } = [];
}
