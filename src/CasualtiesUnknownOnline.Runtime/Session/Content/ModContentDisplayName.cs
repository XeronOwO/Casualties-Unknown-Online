using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// Resolves the human-readable name of a mod content definition from the
/// declaration the mod registered. The registry keeps the mod's own object, so
/// the console can show a display name without the Game Adapter and without
/// decoding anything. The arm is the KIND CONTRACT the declaration implements —
/// not a framework class — so a mod-authored type is named like any other;
/// kinds without a typed display name (recipes, or a kind a mod declared
/// itself) fall back to the registered id.
/// </summary>
internal static class ModContentDisplayName
{
	public static string Resolve(IModContentDefinition definition)
	{
		var displayName = definition switch
		{
			IModItemDefinition item => item.DisplayName,
			IModLiquidDefinition liquid => liquid.DisplayName,
			IModLiquidTileDefinition liquidTile => liquidTile.DisplayName,
			IModTileDefinition tile => tile.DisplayName,
			IModBuildingDefinition building => building.DisplayName,
			IModStructureDefinition structure => structure.DisplayName,
			IModStatusDefinition status => status.DisplayName,
			IModMoodleDefinition moodle => moodle.DisplayName,
			_ => null,
		};

		return string.IsNullOrWhiteSpace(displayName) ? definition.Id : displayName!;
	}
}
