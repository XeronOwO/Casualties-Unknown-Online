using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A block was placed / air-written: guest → host as a report (the host
/// arbitrates — a placement must land on air, an air write on something —
/// then applies and relays), host → guest as the arbitrated answer: the
/// accepted relay reaches EVERY member including the reporter (its echo is the
/// acknowledgement), and a refused report gets the host's current cell as a
/// targeted correction. Block-space integer coordinates + the block id.
/// </summary>
[ProtoContract]
public sealed class BlockPlacedMsg
{
	[ProtoMember(1)]
	public int X { get; set; }

	[ProtoMember(2)]
	public int Y { get; set; }

	[ProtoMember(3)]
	public ushort Block { get; set; }

	/// <summary>
	/// The world/layer generation this report belongs to (protocol 29). The cell
	/// key is LAYER-RELATIVE, so the receiver compares the stamp with its own
	/// generation before the report counts as evidence: a stale air write must not
	/// clear a freshly generated block. Null = the sender has no committed run
	/// baseline yet, which the receiver compares as UNKNOWN and never as fresh.
	/// </summary>
	[ProtoMember(4)]
	public WorldGenerationMsg? Generation { get; set; }
}
