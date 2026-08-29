using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of a convergent state-stream update.
/// </summary>
[ProtoContract]
public sealed class WireStateStream
{
	[ProtoMember(1)]
	public ulong EntityId { get; set; }

	[ProtoMember(2)]
	public ulong BaseGlobalRevision { get; set; }

	[ProtoMember(3)]
	public List<WireStreamField> Fields { get; set; } = [];

	[ProtoMember(4)]
	public List<WireItemMoveEntry> ItemMoves { get; set; } = [];

	[ProtoMember(5)]
	public List<WireWorldItemState> ItemStates { get; set; } = [];

	[ProtoMember(6)]
	public int LayerModifierIndex { get; set; }

	[ProtoMember(7)]
	public byte[]? LayerModifierRandomState { get; set; }

	[ProtoMember(8)]
	public uint Seq { get; set; }

	[ProtoMember(9)]
	public List<WirePlayerStreamState> PlayerStates { get; set; } = [];

	[ProtoMember(10)]
	public List<WireEnemyStreamState> EnemyStates { get; set; } = [];
}
