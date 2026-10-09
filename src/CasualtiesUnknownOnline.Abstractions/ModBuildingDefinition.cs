using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one building entity. It is a plain data
/// object in Abstractions: no game type, no Unity type, no Runtime dependency.
/// A mod fills it in and registers it through <see cref="IModContent"/>; the
/// Runtime content binder routes it by <see cref="Kind"/> to the Game Adapter
/// provider, which reads these typed members into a runtime building prefab
/// instead of decoding a private format.
/// </summary>
public sealed class ModBuildingDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Building;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing building name.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing building description.</summary>
	public string Description { get; set; } = "";

	/// <summary>
	/// The vanilla prefab id used as the runtime template base. The Game
	/// Adapter clones this prefab and renames the clone to the registered
	/// building id.
	/// </summary>
	public string TemplateId { get; set; } = "";

	/// <summary>Optional override for the cloned building's health.</summary>
	public float? Health { get; set; }

	/// <summary>Optional override for whether the building needs ground support.</summary>
	public bool? RequireGround { get; set; }

	/// <summary>Optional override for the vanilla animal flag.</summary>
	public bool? Animal { get; set; }

	/// <summary>Optional override for the vanilla cannot-be-hit flag.</summary>
	public bool? CantHit { get; set; }

	/// <summary>Optional override for the vanilla metallic flag.</summary>
	public bool? Metallic { get; set; }

	/// <summary>Optional override for the vanilla body-optimization suppression flag.</summary>
	public bool? IgnoreBodyOptimize { get; set; }

	/// <summary>Optional override for the chance-based drop multiplier.</summary>
	public float? DropChanceMultiplier { get; set; }

	/// <summary>Optional override for the number of guaranteed category drops.</summary>
	public int? GuaranteedDropAmount { get; set; }

	/// <summary>
	/// Component type names (assembly-qualified or simple names) attached to the
	/// runtime template before it is instantiated. The Game Adapter resolves
	/// the types from loaded assemblies and refuses non-Component types.
	/// </summary>
	public List<string> SpawnComponents
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Chance-based drops spawned when the building is destroyed. Empty means no
	/// authored chance drops; the vanilla building's own drop table still applies
	/// when the base prefab carries one.
	/// </summary>
	public List<ModBuildingDrop> DropOnDestroy
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Drops always spawned when the building is destroyed, regardless of chance.
	/// These are rolled after chance-based drops and are not multiplied by
	/// <see cref="DropChanceMultiplier"/>.
	/// </summary>
	public List<ModBuildingDrop> AlwaysDrop
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Additional vanilla item-loot categories included in the building's
	/// guaranteed category drops. Used together with
	/// <see cref="GuaranteedDropAmount"/>.
	/// </summary>
	public List<string> ItemCategoriesToAdd
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Minimum automatic world-spawn attempts per chunk. Null means no automatic
	/// building distribution; a positive value enables it when
	/// <see cref="GenerationStyle"/> is not <see cref="ModBuildingGenerationStyle.None"/>.
	/// </summary>
	public float? SpawnMinPerChunk { get; set; }

	/// <summary>
	/// Maximum automatic world-spawn attempts per chunk. Null means no automatic
	/// building distribution; a positive value enables it when
	/// <see cref="GenerationStyle"/> is not <see cref="ModBuildingGenerationStyle.None"/>.
	/// </summary>
	public float? SpawnMaxPerChunk { get; set; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic building distribution.
	/// -1 means every layer; 0 disables automatic distribution. Layer N is bit
	/// N-1 (N starts at 1).
	/// </summary>
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <summary>Automatic world-generation placement style. Default None.</summary>
	public ModBuildingGenerationStyle GenerationStyle { get; set; } = ModBuildingGenerationStyle.None;

	/// <summary>Surface this building attaches to when distributed automatically.</summary>
	public ModBuildingPlacement Placement { get; set; } = ModBuildingPlacement.Floor;

	/// <summary>Allows the entity to spawn embedded in ground tiles.</summary>
	public bool SpawnInGround { get; set; }

	/// <summary>Offset from the placement surface to the rendered object.</summary>
	public float? SurfaceOffset { get; set; }

	/// <summary>Allows random horizontal sprite flipping on automatic spawn. Default true when null.</summary>
	public bool? RandomFlip { get; set; }

	/// <summary>A bitmask that allows spawning on every world layer.</summary>
	public const int AllSpawnLayers = -1;

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

	/// <summary>
	/// Whether this building is permitted to distribute automatically on a
	/// zero-based biome depth. Depth 0 is layer 1; a negative or too-large depth
	/// has no layer bit and returns false.
	/// </summary>
	public bool CanSpawnInLayer(int biomeDepth)
	{
		if (SpawnLayers == 0 || biomeDepth < 0)
		{
			return false;
		}

		if (SpawnLayers == AllSpawnLayers)
		{
			return true;
		}

		var layerNumber = biomeDepth + 1;
		return layerNumber > 0 && layerNumber <= 31 && (SpawnLayers & (1 << (layerNumber - 1))) != 0;
	}

}
