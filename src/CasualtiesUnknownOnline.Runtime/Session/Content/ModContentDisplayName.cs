using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// Resolves the human-readable name of a mod content definition from the typed
/// definition the mod registered. The registry keeps the mod's own typed object,
/// so the console can show a display name without the Game Adapter and without
/// decoding anything. Kinds without a typed display name (recipes, or a kind a
/// mod declared itself) fall back to the registered id.
/// </summary>
internal static class ModContentDisplayName
{
	public static string Resolve(IModContentDefinition definition)
	{
		var displayName = definition switch
		{
			ModItemDefinition item => item.DisplayName,
			ModLiquidDefinition liquid => liquid.DisplayName,
			ModLiquidTileDefinition liquidTile => liquidTile.DisplayName,
			ModTileDefinition tile => tile.DisplayName,
			ModBuildingDefinition building => building.DisplayName,
			ModStructureDefinition structure => structure.DisplayName,
			ModStatusDefinition status => status.DisplayName,
			ModMoodleDefinition moodle => moodle.DisplayName,
			_ => null,
		};

		return string.IsNullOrWhiteSpace(displayName) ? definition.Id : displayName!;
	}
}
