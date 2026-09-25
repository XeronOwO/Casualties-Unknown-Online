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

	/// <summary>
	/// The air write is the block-removal half of a DAMAGE ROLL (protocol 40): the
	/// source side computed the break inside the game's own <c>DamageBlock</c>, so
	/// it played the native break presentation there (the broken block's hit and
	/// step sounds, its break particles) — and the receiving side must present the
	/// same break when it applies this write, because the air write arrives BEFORE
	/// the drops-carrying break report and the report therefore never runs its own
	/// native roll on that cell. False for a placement, for an
	/// earthquake/environment air write (<c>SetBlock</c> inside
	/// <c>WorldGeneration.Update</c>, silent on the side that ran it), for a
	/// state snapshot and for a correction: their source played no presentation,
	/// so none may be invented on the receiving side.
	/// </summary>
	[ProtoMember(5)]
	public bool PlayerBreak { get; set; }
}
