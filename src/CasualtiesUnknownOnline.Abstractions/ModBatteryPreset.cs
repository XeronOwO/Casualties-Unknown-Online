namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Battery size preset for a custom battery-backed item. The values mirror the
/// vanilla <c>BatteryItem.BatteryPreset</c> enum so the Game Adapter can map
/// the DTO without a game-type dependency in Abstractions.
/// </summary>
public enum ModBatteryPreset
{
	/// <summary>Small battery (50 charge).</summary>
	Small = 0,

	/// <summary>Medium battery (100 charge).</summary>
	Medium = 1,

	/// <summary>Large battery (300 charge).</summary>
	Large = 2
}
