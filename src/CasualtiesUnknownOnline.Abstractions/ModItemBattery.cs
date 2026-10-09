namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Battery behavior for a custom item. The values are plain data in
/// Abstractions; the Game Adapter configures the vanilla <c>BatteryItem</c>
/// component when it builds the runtime item template.
/// </summary>
public sealed class ModItemBattery
{
	/// <summary>Battery size preset; determines capacity and inserted battery type.</summary>
	public ModBatteryPreset Preset { get; set; } = ModBatteryPreset.Medium;

	/// <summary>
	/// Initial charge. Values from 0 to 1 are treated as a percentage of the
	/// preset capacity; larger values are absolute charge; below zero means full.
	/// </summary>
	public float StartCharge { get; set; } = -1f;

	/// <summary>Whether the item spawns with a battery already inserted.</summary>
	public bool SpawnWithBattery { get; set; } = true;
}
