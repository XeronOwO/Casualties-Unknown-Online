using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Battery behavior for a custom item. The values are plain data in
/// Abstractions; the Game Adapter configures the vanilla <c>BatteryItem</c>
/// component when it builds the runtime item template.
/// </summary>
[DataContract]
public sealed class ModItemBattery
{
	/// <summary>Battery size preset; determines capacity and inserted battery type.</summary>
	[DataMember(Order = 1)]
	public ModBatteryPreset Preset { get; set; } = ModBatteryPreset.Medium;

	/// <summary>
	/// Initial charge. Values from 0 to 1 are treated as a percentage of the
	/// preset capacity; larger values are absolute charge; below zero means full.
	/// </summary>
	[DataMember(Order = 2)]
	public float StartCharge { get; set; } = -1f;

	/// <summary>Whether the item spawns with a battery already inserted.</summary>
	[DataMember(Order = 3)]
	public bool SpawnWithBattery { get; set; } = true;
}
