using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One building-entity declaration. This is the contract a consumer reads: the
/// Runtime content binder routes it by <see cref="IModContentDefinition.Kind"/>
/// to the Game Adapter provider, which reads these members into a runtime
/// building prefab.
///
/// <see cref="ModBuildingDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModBuildingDefinition : IModContentDefinition
{
	/// <summary>Player-facing building name.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing building description.</summary>
	string Description { get; }

	/// <summary>
	/// The vanilla prefab id used as the runtime template base. The Game
	/// Adapter clones this prefab and renames the clone to the registered
	/// building id.
	/// </summary>
	string TemplateId { get; }

	/// <summary>Optional override for the cloned building's health.</summary>
	float? Health { get; }

	/// <summary>Optional override for whether the building needs ground support.</summary>
	bool? RequireGround { get; }

	/// <summary>Optional override for the vanilla animal flag.</summary>
	bool? Animal { get; }

	/// <summary>Optional override for the vanilla cannot-be-hit flag.</summary>
	bool? CantHit { get; }

	/// <summary>Optional override for the vanilla metallic flag.</summary>
	bool? Metallic { get; }

	/// <summary>Optional override for the vanilla body-optimization suppression flag.</summary>
	bool? IgnoreBodyOptimize { get; }

	/// <summary>Optional override for the chance-based drop multiplier.</summary>
	float? DropChanceMultiplier { get; }

	/// <summary>Optional override for the number of guaranteed category drops.</summary>
	int? GuaranteedDropAmount { get; }

	/// <summary>
	/// Component type names (assembly-qualified or simple names) attached to the
	/// runtime template before it is instantiated. The Game Adapter resolves
	/// the types from loaded assemblies and refuses non-Component types. Null
	/// means none.
	/// </summary>
	List<string> SpawnComponents { get; }

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	Dictionary<string, string> CustomData { get; }

	/// <summary>
	/// Chance-based drops spawned when the building is destroyed. Null means no
	/// authored chance drops; the vanilla building's own drop table still applies
	/// when the base prefab carries one.
	/// </summary>
	List<IModBuildingDrop> DropOnDestroy { get; }

	/// <summary>
	/// Drops always spawned when the building is destroyed, regardless of chance.
	/// These are rolled after chance-based drops and are not multiplied by
	/// <see cref="DropChanceMultiplier"/>.
	/// </summary>
	List<IModBuildingDrop> AlwaysDrop { get; }

	/// <summary>
	/// Additional vanilla item-loot categories included in the building's
	/// guaranteed category drops. Used together with
	/// <see cref="GuaranteedDropAmount"/>.
	/// </summary>
	List<string> ItemCategoriesToAdd { get; }

	/// <summary>
	/// Minimum automatic world-spawn attempts per chunk. Null means no automatic
	/// building distribution; a positive value enables it when
	/// <see cref="GenerationStyle"/> is not <see cref="ModBuildingGenerationStyle.None"/>.
	/// </summary>
	float? SpawnMinPerChunk { get; }

	/// <summary>
	/// Maximum automatic world-spawn attempts per chunk. Null means no automatic
	/// building distribution; a positive value enables it when
	/// <see cref="GenerationStyle"/> is not <see cref="ModBuildingGenerationStyle.None"/>.
	/// </summary>
	float? SpawnMaxPerChunk { get; }

	/// <summary>
	/// Bitmask of allowed world layers for automatic building distribution.
	/// -1 means every layer; 0 disables automatic distribution. Layer N is bit
	/// N-1 (N starts at 1).
	/// </summary>
	int SpawnLayers { get; }

	/// <summary>Automatic world-generation placement style. Default None.</summary>
	ModBuildingGenerationStyle GenerationStyle { get; }

	/// <summary>Surface this building attaches to when distributed automatically.</summary>
	ModBuildingPlacement Placement { get; }

	/// <summary>Allows the entity to spawn embedded in ground tiles.</summary>
	bool SpawnInGround { get; }

	/// <summary>Offset from the placement surface to the rendered object.</summary>
	float? SurfaceOffset { get; }

	/// <summary>Allows random horizontal sprite flipping on automatic spawn. Default true when null.</summary>
	bool? RandomFlip { get; }
}
