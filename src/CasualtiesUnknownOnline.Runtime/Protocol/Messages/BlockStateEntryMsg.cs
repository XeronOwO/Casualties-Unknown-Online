using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One changed block: integer block-space coordinates + the current block id.</summary>
[ProtoContract]
public sealed class BlockStateEntryMsg
{
	[ProtoMember(1)]
	public int X { get; set; }

	[ProtoMember(2)]
	public int Y { get; set; }

	[ProtoMember(3)]
	public ushort Block { get; set; }

	/// <summary>
	/// True when this cell's air transition has ALREADY had its building support
	/// loss settled — a row that came back from the world archive rather than a
	/// live write. The receiver must apply the cell but must NOT re-run the
	/// support-loss settlement: the building that stood there already died (or
	/// survived) in the world that wrote the row, and re-running it would kill a
	/// building the authority still holds.
	/// </summary>
	[ProtoMember(4)]
	public bool SupportLossSettled { get; set; }
}
