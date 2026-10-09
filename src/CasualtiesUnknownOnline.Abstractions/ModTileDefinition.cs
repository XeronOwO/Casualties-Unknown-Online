using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModTileDefinition"/>: a plain data
/// object in Abstractions with no Unity type, no game type and no Runtime
/// dependency, which is also why it cannot compute a member. Use it when every
/// value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// would deserialize into this concrete type.
///
/// The two static mask builders and the layer sentinel stay here as the
/// mod-facing helpers a declaration is authored with; the rule that reads the
/// mask is <see cref="ModLayerSpawnRule"/>.
/// </summary>
public sealed class ModTileDefinition : IModTileDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Tile;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public int? TemplateTileIndex { get; set; }

	/// <inheritdoc />
	public string SpritePath { get; set; } = "";

	/// <inheritdoc />
	public string TileName { get; set; } = "";

	/// <inheritdoc />
	public float Health { get; set; } = 100f;

	/// <inheritdoc />
	public string HitSound { get; set; } = "rock";

	/// <inheritdoc />
	public string StepSound { get; set; } = "Rock";

	/// <inheritdoc />
	public ModTileSleepQuality SleepQuality { get; set; } = ModTileSleepQuality.Bad;

	/// <inheritdoc />
	public bool NoVariation { get; set; }

	/// <inheritdoc />
	public bool Metallic { get; set; }

	/// <inheritdoc />
	public float Toxicity { get; set; }

	/// <inheritdoc />
	public bool Slippery { get; set; }

	/// <inheritdoc />
	public float ColorR { get; set; } = 1f;

	/// <inheritdoc />
	public float ColorG { get; set; } = 1f;

	/// <inheritdoc />
	public float ColorB { get; set; } = 1f;

	/// <inheritdoc />
	public float ColorA { get; set; } = 1f;

	/// <inheritdoc />
	public ModTileColliderType ColliderType { get; set; } = ModTileColliderType.Grid;

	/// <inheritdoc />
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public float SpawnAmount { get; set; }

	/// <inheritdoc />
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <inheritdoc />
	public ModTileGenerationStyle GenerationStyle { get; set; } = ModTileGenerationStyle.Vein;

	/// <inheritdoc />
	public List<ModTileDrop> Drops
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Bitmask that allows spawning on every world layer; one home, see <see cref="ModLayerSpawnRule.AllSpawnLayers"/>.</summary>
	public const int AllSpawnLayers = ModLayerSpawnRule.AllSpawnLayers;

	/// <summary>Build a layer bitmask from one-based layer numbers.</summary>
	public static int LayersToMask(params int[] layerNumbers)
	{
		if (layerNumbers is null)
		{
			return 0;
		}

		var mask = 0;
		foreach (var layer in layerNumbers)
		{
			if (layer > 0 && layer <= 31)
			{
				mask |= 1 << (layer - 1);
			}
		}

		return mask;
	}

	/// <summary>Build an all-layers bitmask excluding one-based layer numbers.</summary>
	public static int AllLayersExcept(params int[] excludedLayerNumbers)
	{
		if (excludedLayerNumbers is null || excludedLayerNumbers.Length == 0)
		{
			return AllSpawnLayers;
		}

		var mask = AllSpawnLayers;
		foreach (var layer in excludedLayerNumbers)
		{
			if (layer > 0 && layer <= 31)
			{
				mask &= ~(1 << (layer - 1));
			}
		}

		return mask;
	}
}
