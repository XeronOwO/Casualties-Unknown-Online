using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// Resolves the human-readable name of a mod content definition from its
/// well-known typed DTO. Content payloads stay opaque to the framework, but the
/// typed DTOs in Abstractions are the framework's own contract, so the console
/// can show a display name without the Game Adapter. Kinds without a typed
/// display name (for example recipes) fall back to the registered id.
/// </summary>
internal static class ModContentDisplayName
{
	public static string Resolve(ModContentDefinition definition)
	{
		var displayName = definition.Kind switch
		{
			ModContentKind.Item => ModItemDefinition.FromPayload(definition.Data)?.DisplayName,
			ModContentKind.Liquid => ModLiquidDefinition.FromPayload(definition.Data)?.DisplayName,
			ModContentKind.LiquidTile => ModLiquidTileDefinition.FromPayload(definition.Data)?.DisplayName,
			ModContentKind.Tile => ModTileDefinition.FromPayload(definition.Data)?.DisplayName,
			ModContentKind.Building => ModBuildingDefinition.FromPayload(definition.Data)?.DisplayName,
			ModContentKind.Structure => ModStructureDefinition.FromPayload(definition.Data)?.DisplayName,
			ModContentKind.Status => ModStatusDefinition.FromPayload(definition.Data)?.DisplayName,
			ModContentKind.Moodle => ModMoodleDefinition.FromPayload(definition.Data)?.DisplayName,
			_ => null,
		};

		return string.IsNullOrWhiteSpace(displayName) ? definition.Id : displayName!;
	}
}
