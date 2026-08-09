using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The full authoritative world-item table, sent to a member on its world
/// entry (late joiner / reconnect) so it sees every runtime-generated world
/// item. The receiver reconciles: spawns the missing, destroys the stale,
/// moves the moved.
/// </summary>
[ProtoContract]
public sealed class ItemSnapshotMsg
{
	[ProtoMember(1)]
	public List<ItemSnapshotEntryMsg> Entries { get; set; } = [];

	/// <summary>The current layer modifier (index into
	/// LayerModifier.availableModifiers, -1 = none) — rides the snapshot so a
	/// world entry OUTSIDE a generation (solo→lobby conversion, mid-session
	/// join) still receives the host's modifier. Idempotent on the guest: the
	/// modifier is only (re-)applied when the index changes.</summary>
	[ProtoMember(2)]
	public int LayerModifierIndex { get; set; } = -1;

	/// <summary>The random stream state at the entry of the host's modifier
	/// decision — rides alongside LayerModifierIndex so an out-of-generation
	/// world entry can replay the decision draws before the modifier's
	/// Initialize (see WorldItemsSnapshotMsg.LayerModifierRandomState).</summary>
	[ProtoMember(3)]
	public byte[]? LayerModifierRandomState { get; set; }
}
