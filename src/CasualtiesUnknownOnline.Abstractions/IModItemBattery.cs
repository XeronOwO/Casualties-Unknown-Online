namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The battery behavior of a custom item. This is the contract a consumer reads:
/// <see cref="IModItemDefinition.Battery"/> is interface-typed, so a mod that
/// computes a battery value hands over its own implementation instead of filling
/// in the framework's.
///
/// <see cref="ModItemBattery"/> is the framework's ready-made implementation: use
/// it when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back
/// a filled default for the members it does not touch — never inheritance from
/// the data class, which stays <c>sealed</c>.
/// </summary>
public interface IModItemBattery
{
	/// <summary>Battery size preset; determines capacity and inserted battery type.</summary>
	ModBatteryPreset Preset { get; }

	/// <summary>
	/// Initial charge. Values from 0 to 1 are treated as a percentage of the
	/// preset capacity; larger values are absolute charge; below zero means full.
	/// </summary>
	float StartCharge { get; }

	/// <summary>Whether the item spawns with a battery already inserted.</summary>
	bool SpawnWithBattery { get; }
}
