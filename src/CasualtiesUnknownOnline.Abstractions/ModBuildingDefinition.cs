using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one building-entity content
/// definition. It is a plain data object in Abstractions: no game type, no
/// Unity type, no Runtime dependency. The payload is still registered through
/// the opaque <see cref="IModContent"/> channel; this type is the well-known
/// schema that the Runtime content binder and a Game Adapter provider can
/// decode into a runtime building prefab without inventing a private format.
/// </summary>
[DataContract]
public sealed class ModBuildingDefinition
{
	/// <summary>Player-facing building name.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing building description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>
	/// The vanilla prefab id used as the runtime template base. The Game
	/// Adapter clones this prefab and renames the clone to the registered
	/// building id.
	/// </summary>
	[DataMember(Order = 3)]
	public string TemplateId { get; set; } = "";

	/// <summary>Optional override for the cloned building's health.</summary>
	[DataMember(Order = 4)]
	public float? Health { get; set; }

	/// <summary>Optional override for whether the building needs ground support.</summary>
	[DataMember(Order = 5)]
	public bool? RequireGround { get; set; }

	/// <summary>Optional override for the vanilla animal flag.</summary>
	[DataMember(Order = 6)]
	public bool? Animal { get; set; }

	/// <summary>Optional override for the vanilla cannot-be-hit flag.</summary>
	[DataMember(Order = 7)]
	public bool? CantHit { get; set; }

	/// <summary>Optional override for the vanilla metallic flag.</summary>
	[DataMember(Order = 8)]
	public bool? Metallic { get; set; }

	/// <summary>Optional override for the vanilla body-optimization suppression flag.</summary>
	[DataMember(Order = 9)]
	public bool? IgnoreBodyOptimize { get; set; }

	/// <summary>Optional override for the chance-based drop multiplier.</summary>
	[DataMember(Order = 10)]
	public float? DropChanceMultiplier { get; set; }

	/// <summary>Optional override for the number of guaranteed category drops.</summary>
	[DataMember(Order = 11)]
	public int? GuaranteedDropAmount { get; set; }

	/// <summary>
	/// Component type names (assembly-qualified or simple names) attached to the
	/// runtime template before it is instantiated. The Game Adapter resolves
	/// the types from loaded assemblies and refuses non-Component types.
	/// </summary>
	[DataMember(Order = 12)]
	public List<string> SpawnComponents { get; set; } = [];

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 13)]
	public Dictionary<string, string> CustomData { get; set; } = [];

	/// <summary>
	/// Chance-based drops spawned when the building is destroyed. Empty means no
	/// authored chance drops; the vanilla building's own drop table still applies
	/// when the base prefab carries one.
	/// </summary>
	[DataMember(Order = 14)]
	public List<ModBuildingDrop> DropOnDestroy { get; set; } = [];

	/// <summary>
	/// Drops always spawned when the building is destroyed, regardless of chance.
	/// These are rolled after chance-based drops and are not multiplied by
	/// <see cref="DropChanceMultiplier"/>.
	/// </summary>
	[DataMember(Order = 15)]
	public List<ModBuildingDrop> AlwaysDrop { get; set; } = [];

	/// <summary>
	/// Additional vanilla item-loot categories included in the building's
	/// guaranteed category drops. Used together with
	/// <see cref="GuaranteedDropAmount"/>.
	/// </summary>
	[DataMember(Order = 16)]
	public List<string> ItemCategoriesToAdd { get; set; } = [];

	/// <summary>
	/// Minimum automatic world-spawn attempts per chunk. Null means no automatic
	/// building distribution; a positive value enables it when
	/// <see cref="GenerationStyle"/> is not <see cref="ModBuildingGenerationStyle.None"/>.
	/// </summary>
	[DataMember(Order = 17)]
	public float? SpawnMinPerChunk { get; set; }

	/// <summary>
	/// Maximum automatic world-spawn attempts per chunk. Null means no automatic
	/// building distribution; a positive value enables it when
	/// <see cref="GenerationStyle"/> is not <see cref="ModBuildingGenerationStyle.None"/>.
	/// </summary>
	[DataMember(Order = 18)]
	public float? SpawnMaxPerChunk { get; set; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic building distribution.
	/// -1 means every layer; 0 disables automatic distribution. Layer N is bit
	/// N-1 (N starts at 1).
	/// </summary>
	[DataMember(Order = 19)]
	public int SpawnLayers { get; set; } = AllSpawnLayers;

	/// <summary>Automatic world-generation placement style. Default None.</summary>
	[DataMember(Order = 20)]
	public ModBuildingGenerationStyle GenerationStyle { get; set; } = ModBuildingGenerationStyle.None;

	/// <summary>Surface this building attaches to when distributed automatically.</summary>
	[DataMember(Order = 21)]
	public ModBuildingPlacement Placement { get; set; } = ModBuildingPlacement.Floor;

	/// <summary>Allows the entity to spawn embedded in ground tiles.</summary>
	[DataMember(Order = 22)]
	public bool SpawnInGround { get; set; }

	/// <summary>Offset from the placement surface to the rendered object.</summary>
	[DataMember(Order = 23)]
	public float? SurfaceOffset { get; set; }

	/// <summary>Allows random horizontal sprite flipping on automatic spawn. Default true when null.</summary>
	[DataMember(Order = 24)]
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

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModBuildingDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a building definition payload. Returns null when the payload is not a valid building definition.</summary>
	public static ModBuildingDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModBuildingDefinition));
			return serializer.ReadObject(stream) as ModBuildingDefinition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid
			// building definition under the current contract; the binder should
			// refuse it rather than fail the whole mod discovery.
			return null;
		}
	}
}
