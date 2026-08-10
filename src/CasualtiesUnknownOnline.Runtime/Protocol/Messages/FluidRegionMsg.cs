using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A rectangular region of the world fluid grid (FluidManager.fluid) in RLE —
/// an ABSOLUTE snapshot of the region: every cell in the rectangle is covered
/// (trailing zero runs omitted = the decoder clears the rest), so an apply is
/// idempotent and a lost message is healed by the next one (the stream is
/// unreliable with a Seq). The host is the fluid authority (#129): it simulates
/// the world grid alone and streams each member's viewport — a 10 Hz diff (the
/// changed cells' bounding box) plus a 1 Hz full-viewport snapshot (the
/// fallback: packet loss, late joiners, the bath-soiled water which is not
/// reported separately). The guest never simulates: it applies the region onto
/// its local grid and the game's own renderer (RenderFluids) draws it.
/// RLE: row-major runs of [value, count] — liquid is sparse, a full
/// 128 x 112 viewport compresses to a few KB at most.
/// </summary>
[ProtoContract]
public sealed class FluidRegionMsg
{
	[ProtoMember(1)]
	public byte Seq { get; set; }

	/// <summary>The region's bottom-left grid cell (block coordinates).</summary>
	[ProtoMember(2)]
	public int OriginX { get; set; }

	[ProtoMember(3)]
	public int OriginY { get; set; }

	[ProtoMember(4)]
	public byte Width { get; set; }

	[ProtoMember(5)]
	public byte Height { get; set; }

	/// <summary>
	/// RLE runs: [value, count, value, count, ...] in row-major order. The runs
	/// cover Width * Height cells (trailing all-zero runs may be omitted — the
	/// decoder clears the uncovered cells). Count is 1..255.
	/// </summary>
	[ProtoMember(6)]
	public byte[] Cells { get; set; } = [];
}
