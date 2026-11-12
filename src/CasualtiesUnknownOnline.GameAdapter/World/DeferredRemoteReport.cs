using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>The relay a whole world could not take yet: the message's fields exactly as they arrived, held in arrival order until the world finishes generating.</summary>
internal readonly struct DeferredRemoteReport(
	ulong sender, int x, int y, float dmg, bool metalBonus,
	IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
{
	public ulong Sender { get; } = sender;
	public int X { get; } = x;
	public int Y { get; } = y;
	public float Dmg { get; } = dmg;
	public bool MetalBonus { get; } = metalBonus;
	public IReadOnlyList<BlockDropEntryMsg>? Drops { get; } = drops;
	public IReadOnlyList<TrapDropEntryMsg>? BuildingDrops { get; } = buildingDrops;
}
