using System;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One authored drop entry for a custom tile. It is a plain data contract in
/// Abstractions: no game type, no Unity type, no Runtime dependency. The Game
/// Adapter resolves the item id through the existing custom-item/prefab seam and
/// spawns the drop with the local break report.
/// </summary>
[DataContract]
public sealed class ModTileDrop
{
	/// <summary>Item content id or vanilla item id spawned when the tile breaks.</summary>
	[DataMember(Order = 1)]
	public string ItemId { get; set; } = "";

	/// <summary>Probability that this drop is spawned (0..1).</summary>
	[DataMember(Order = 2)]
	public float Chance { get; set; } = 1f;

	/// <summary>Minimum spawned item condition (0..1).</summary>
	[DataMember(Order = 3)]
	public float MinCondition { get; set; }

	/// <summary>Maximum spawned item condition (0..1).</summary>
	[DataMember(Order = 4)]
	public float MaxCondition { get; set; } = 1f;

	/// <summary>Clamp a condition roll into the authored range.</summary>
	public float RollCondition(float value)
	{
		var min = Math.Max(0f, Math.Min(MinCondition, 1f));
		var max = Math.Max(min, Math.Min(MaxCondition, 1f));
		return Math.Max(min, Math.Min(value, max));
	}
}
