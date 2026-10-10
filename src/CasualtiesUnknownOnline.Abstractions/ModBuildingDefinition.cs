using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModBuildingDefinition"/>: a plain data
/// object in Abstractions with no game type, no Unity type and no Runtime
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
public sealed class ModBuildingDefinition : IModBuildingDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Building;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string DisplayName { get; set; } = "";

	/// <inheritdoc />
	public string Description { get; set; } = "";

	/// <inheritdoc />
	public string TemplateId { get; set; } = "";

	/// <inheritdoc />
	public float? Health { get; set; }

	/// <inheritdoc />
	public bool? RequireGround { get; set; }

	/// <inheritdoc />
	public bool? Animal { get; set; }

	/// <inheritdoc />
	public bool? CantHit { get; set; }

	/// <inheritdoc />
	public bool? Metallic { get; set; }

	/// <inheritdoc />
	public bool? IgnoreBodyOptimize { get; set; }

	/// <inheritdoc />
	public float? DropChanceMultiplier { get; set; }

	/// <inheritdoc />
	public int? GuaranteedDropAmount { get; set; }

	/// <inheritdoc />
	public List<string> SpawnComponents
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public List<IModBuildingDrop> DropOnDestroy
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public List<IModBuildingDrop> AlwaysDrop
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public List<string> ItemCategoriesToAdd
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <inheritdoc />
	public float? SpawnMinPerChunk { get; set; }

	/// <inheritdoc />
	public float? SpawnMaxPerChunk { get; set; }

	/// <inheritdoc />
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <inheritdoc />
	public ModBuildingGenerationStyle GenerationStyle { get; set; } = ModBuildingGenerationStyle.None;

	/// <inheritdoc />
	public ModBuildingPlacement Placement { get; set; } = ModBuildingPlacement.Floor;

	/// <inheritdoc />
	public bool SpawnInGround { get; set; }

	/// <inheritdoc />
	public float? SurfaceOffset { get; set; }

	/// <inheritdoc />
	public bool? RandomFlip { get; set; }

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
