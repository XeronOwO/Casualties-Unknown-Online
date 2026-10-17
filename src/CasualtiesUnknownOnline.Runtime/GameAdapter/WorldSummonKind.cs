namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The operator's own type word on <c>/summon</c> — the shape Minecraft's command has, where the entity type comes
/// first: <c>item</c> names a loose item entity and anything else names a world-entity (or animal) prefab. The
/// ADAPTER enforces the word against the game's own tables, so <c>/summon item beartrap</c> is refused by name
/// rather than silently creating the wrong family, and <c>/summon bandage</c> is refused with the spelling that
/// works.
/// </summary>
public enum WorldSummonKind
{
	/// <summary>A world-entity or animal prefab (<c>Utils.Create</c>; the created object must carry a <c>BuildingEntity</c> and an entity id).</summary>
	Entity,

	/// <summary>A loose item entity: the item table's definition, the operator's SNBT state bound against its own capture, the one allocator's instance id — the same fact and the same world object the give ladder's drop rung makes.</summary>
	Item,
}
