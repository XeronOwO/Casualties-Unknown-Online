namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The one rule behind every "may this declaration distribute itself on this
/// biome depth" question. A declaration owns the FACT (its
/// <c>SpawnLayers</c> bitmask); the rule that reads it is the framework's, so
/// it lives here once instead of being restated by every implementation — the
/// three declarations that distribute themselves across layers (tile, building,
/// liquid tile) answer it through the same body, and a mod that computes its own
/// mask gets the rule without writing it.
///
/// <see cref="AllSpawnLayers"/> is the sentinel's single home; the declaration
/// types expose it under their own names for the mods that build a mask by hand.
/// </summary>
public static class ModLayerSpawnRule
{
	/// <summary>Bitmask that allows spawning on every world layer.</summary>
	public const int AllSpawnLayers = -1;

	extension(IModTileDefinition definition)
	{
		/// <summary>
		/// Whether this tile is permitted to spawn automatically on a zero-based
		/// biome depth. Depth 0 is layer 1; a negative or too-large depth has no
		/// layer bit and returns false.
		/// </summary>
		public bool CanSpawnInLayer(int biomeDepth) => AllowsLayer(definition.SpawnLayers, biomeDepth);
	}

	extension(IModBuildingDefinition definition)
	{
		/// <summary>
		/// Whether this building is permitted to distribute automatically on a
		/// zero-based biome depth. Depth 0 is layer 1; a negative or too-large
		/// depth has no layer bit and returns false.
		/// </summary>
		public bool CanSpawnInLayer(int biomeDepth) => AllowsLayer(definition.SpawnLayers, biomeDepth);
	}

	extension(IModLiquidTileDefinition definition)
	{
		/// <summary>
		/// Whether this liquid tile is permitted to spawn automatically on a
		/// zero-based biome depth. Depth 0 is layer 1; a negative or too-large
		/// depth has no layer bit and returns false.
		/// </summary>
		public bool CanSpawnInLayer(int biomeDepth) => AllowsLayer(definition.SpawnLayers, biomeDepth);
	}

	/// <summary>
	/// The rule itself: a zero mask permits no layer, the all-layers sentinel
	/// permits every layer, and any other mask is read as "layer N is bit N-1".
	/// </summary>
	private static bool AllowsLayer(int spawnLayers, int biomeDepth)
	{
		if (spawnLayers == 0 || biomeDepth < 0)
		{
			return false;
		}

		if (spawnLayers == AllSpawnLayers)
		{
			return true;
		}

		var layerNumber = biomeDepth + 1;
		return layerNumber > 0 && layerNumber <= 31 && (spawnLayers & (1 << (layerNumber - 1))) != 0;
	}
}
